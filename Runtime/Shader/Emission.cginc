#pragma once

// Include after Protector.cginc. Settings.x: 0 = off, 1 = RGBA, 2 = DXT1,
// 3 = DXT5. Format and dimensions are independent of the main texture.
// The encrypted words are the texels of a data texture in the Blocks slot: the encrypted RGBA texture (the same one as the
// main slot), or the endpoint texture (one texel per DXT block). It uses the main RGBA32 layout (EmissionEncryption): ChaCha
// encrypts 4x4 data texels with one keystream block, XXTEA encrypts pairs of texels. The slot is mixed into key word 0, the
// map's random domain (Wrap.w, see EmissionEncryption.NewDomain) into key word 1 and the mip level into key word 3. Every
// size is a power of two.
#define SHELL_EMISSION_SLOT(n) Texture2D _ShellEmission##n; Texture2D _ShellEmission##n##Blocks; float4 _ShellEmission##n##Settings; float4 _ShellEmission##n##Wrap;
SHELL_EMISSION_SLOT(0)
SHELL_EMISSION_SLOT(1)
SHELL_EMISSION_SLOT(2)
SHELL_EMISSION_SLOT(3)

#ifdef _SHELL_PROTECTOR_CHACHA
    #define SHELL_EMISSION_DATA_LENGTH 16
#else
    #define SHELL_EMISSION_DATA_LENGTH 2
#endif

// IsDecrypted() is a 16-round hash. The shader that decrypts the main texture stores the result its vertex stage already
// computed here (0 or 1); a pass that doesn't leaves -1 and computes it.
static int _ShellDecryptedState = -1;

bool ShellIsDecrypted()
{
    if(_ShellDecryptedState >= 0)
        return _ShellDecryptedState != 0;
    return IsDecrypted();
}

// Wrap modes of TextureWrapMode. The sizes are powers of two, so wrapping masks instead of using an integer modulo, which
// GPUs emulate with dozens of instructions.
int ShellEmissionCoord(int p, int size, int mode)
{
    if(mode == 1) return clamp(p, 0, size - 1); // Clamp
    if(mode == 3) return clamp(p < 0 ? -p - 1 : p, 0, size - 1); // MirrorOnce
    if(mode == 2) // Mirror
    {
        const int q = p & (2 * size - 1);
        return q >= size ? 2 * size - 1 - q : q;
    }
    return p & (size - 1); // Repeat
}

uint ShellEmissionPack(float4 c)
{
    uint4 b = (uint4)round(c * 255.0);
    return b.x | (b.y << 8) | (b.z << 16) | (b.w << 24);
}

uint ShellEmissionLoad(Texture2D data, int2 d, int mip)
{
    return ShellEmissionPack(data.Load(int3(d, mip)));
}

// Unity's GammaToLinearSpace polynomial: within 0.3% of the sRGB curve, without pow per channel and tap.
float3 ShellSRGBToLinear(float3 c)
{
    return c * (c * (c * 0.305306011 + 0.682171111) + 0.012522878);
}

// The encryption unit of data texel d, and the texel's word k in it.
uint ShellEmissionUnit(int2 d, int width, out int k)
{
#ifdef _SHELL_PROTECTOR_CHACHA
    k = (d.y & 3) * 4 + (d.x & 3);
    return (d.y >> 2) * ((width + 3) >> 2) + (d.x >> 2);
#else
    uint index = d.y * width + d.x;
    k = index & 1;
    return index >> 1;
#endif
}

// ChaCha leaves the keystream in words (ShellEmissionWord XORs each texel), XXTEA the decrypted pair.
void ShellEmissionDecryptUnit(Texture2D data, uint unit, int width, int mip, uint slot, uint domain, out uint words[SHELL_EMISSION_DATA_LENGTH])
{
#ifdef _SHELL_PROTECTOR_CHACHA
    const uint first = unit;
    [unroll]
    for(int i = 0; i < 16; ++i)
        words[i] = 0;
#else
    const uint first = unit * 2;
    const uint shift = firstbitlow((uint)width);
    words[0] = ShellEmissionLoad(data, int2(first & (width - 1), first >> shift), mip);
    words[1] = ShellEmissionLoad(data, int2((first + 1) & (width - 1), (first + 1) >> shift), mip);
#endif
    const uint key[4] = {
        _SHELL_PROTECTOR_KEY_MASK0 ^ ((uint)round(_Key0) | ((uint)round(_Key1) << 8) | ((uint)round(_Key2) << 16) | ((uint)round(_Key3) << 24)) ^ (0x53450000u + slot),
        _SHELL_PROTECTOR_KEY_MASK1 ^ ((uint)round(_Key4) | ((uint)round(_Key5) << 8) | ((uint)round(_Key6) << 16) | ((uint)round(_Key7) << 24)) ^ domain,
        _SHELL_PROTECTOR_KEY_MASK2 ^ ((uint)round(_Key8) | ((uint)round(_Key9) << 8) | ((uint)round(_Key10) << 16) | ((uint)round(_Key11) << 24)),
        _SHELL_PROTECTOR_KEY_MASK3 ^ ((uint)round(_Key12) | ((uint)round(_Key13) << 8) | ((uint)round(_Key14) << 16) | ((uint)round(_Key15) << 24)) ^ first ^ ((uint)mip << 24)
    };
    Decrypt(words, key);
}

uint ShellEmissionWord(Texture2D data, int2 d, int mip, const uint words[SHELL_EMISSION_DATA_LENGTH], int k)
{
#ifdef _SHELL_PROTECTOR_CHACHA
    return ShellEmissionLoad(data, d, mip) ^ SelectWord(words, k);
#else
    return SelectWord(words, k);
#endif
}

// p: the texel at this mip, c: its decrypted word (RGBA, or the block's RGB565 endpoints).
float4 ShellEmissionDecode(Texture2D tex, uint c, int2 p, int mip, float4 settings)
{
    float4 color = float4(c & 255u, (c >> 8) & 255u, (c >> 16) & 255u, c >> 24) / 255.0;
    UNITY_BRANCH
    if(settings.x > 1.5)
    {
        float4 selector = tex.Load(int3(p, mip));
        color = float4(ShellDXTColors(c, selector.rgb), selector.a);
        // DXT5 always uses four RGB colors; DXT1 can use a three-color palette
        // with transparent index 3. The stored white/black palette encodes the
        // original index as 1, 0, 2/3 or 1/3.
        if(settings.x < 2.5 && (c & 65535u) <= (c >> 16))
        {
            if(selector.r > 0.5 && selector.r < 0.9) color.rgb = ShellDXTColors(c, 0.5);
            if(selector.r > 0.1 && selector.r < 0.5) color = 0;
        }
    }
    // Per tap, like sRGB sampling, which converts before filtering.
    #ifndef UNITY_COLORSPACE_GAMMA
    if(settings.y > 0.5)
        color.rgb = ShellSRGBToLinear(color.rgb);
    #endif
    return color;
}

// One mip level. Point filtering is the bilinear path with all four taps on the same texel, so the shader holds one copy of
// the decryption code: the taps then share one unit and decrypt once.
float4 ShellEmissionLevel(Texture2D tex, Texture2D data, float2 uv, int2 dimensions, int mip, float4 settings, float4 wrap, uint slot)
{
    const int2 size = max(dimensions >> mip, 1);
    const bool compressed = settings.x > 1.5;
    const uint domain = (uint)round(wrap.w);
    const bool bilinear = settings.w > 0.5;
    const int width = compressed ? max(1, size.x >> 2) : size.x;
    const float2 position = uv * size - (bilinear ? 0.5 : 0.0);
    const int2 base = (int2)floor(position);
    const float2 f = bilinear ? frac(position) : 0;
    const int x0 = ShellEmissionCoord(base.x, size.x, (int)wrap.x);
    const int y0 = ShellEmissionCoord(base.y, size.y, (int)wrap.y);
    const int x1 = bilinear ? ShellEmissionCoord(base.x + 1, size.x, (int)wrap.x) : x0;
    const int y1 = bilinear ? ShellEmissionCoord(base.y + 1, size.y, (int)wrap.y) : y0;
    const int2 p00 = int2(x0, y0), p10 = int2(x1, y0), p01 = int2(x0, y1), p11 = int2(x1, y1);
    const int2 d00 = compressed ? p00 >> 2 : p00;
    const int2 d10 = compressed ? p10 >> 2 : p10;
    const int2 d01 = compressed ? p01 >> 2 : p01;
    const int2 d11 = compressed ? p11 >> 2 : p11;
    int k00, k10, k01, k11;
    const uint unit00 = ShellEmissionUnit(d00, width, k00);
    const uint unit10 = ShellEmissionUnit(d10, width, k10);
    const uint unit01 = ShellEmissionUnit(d01, width, k01);
    const uint unit11 = ShellEmissionUnit(d11, width, k11);

    // Same as DecryptTextureBilinear: decrypt again only for the taps that land in another unit.
    uint words[SHELL_EMISSION_DATA_LENGTH];
    ShellEmissionDecryptUnit(data, unit00, width, mip, slot, domain, words);
    const uint word00 = ShellEmissionWord(data, d00, mip, words, k00);
    uint word10 = ShellEmissionWord(data, d10, mip, words, k10);
    uint word01 = ShellEmissionWord(data, d01, mip, words, k01);
    uint word11 = ShellEmissionWord(data, d11, mip, words, k11);

    [branch]
    if(unit10 != unit00)
    {
        ShellEmissionDecryptUnit(data, unit10, width, mip, slot, domain, words);
        word10 = ShellEmissionWord(data, d10, mip, words, k10);
        if(unit11 == unit10)
            word11 = ShellEmissionWord(data, d11, mip, words, k11);
    }
    [branch]
    if(unit01 != unit00)
    {
        ShellEmissionDecryptUnit(data, unit01, width, mip, slot, domain, words);
        word01 = ShellEmissionWord(data, d01, mip, words, k01);
        if(unit11 == unit01)
            word11 = ShellEmissionWord(data, d11, mip, words, k11);
    }
    [branch]
    if(unit11 != unit00 && unit11 != unit10 && unit11 != unit01)
    {
        ShellEmissionDecryptUnit(data, unit11, width, mip, slot, domain, words);
        word11 = ShellEmissionWord(data, d11, mip, words, k11);
    }

    const float4 c00 = ShellEmissionDecode(tex, word00, p00, mip, settings);
    const float4 c10 = ShellEmissionDecode(tex, word10, p10, mip, settings);
    const float4 c01 = ShellEmissionDecode(tex, word01, p01, mip, settings);
    const float4 c11 = ShellEmissionDecode(tex, word11, p11, mip, settings);
    return lerp(lerp(c00, c10, f.x), lerp(c01, c11, f.x), f.y);
}

float4 ShellEmissionSample(Texture2D tex, Texture2D data, float2 uv, float4 settings, float4 wrap, uint slot, bool unlocked)
{
    float4 color = 0;
    UNITY_BRANCH
    if(unlocked)
    {
        uint width, height;
        tex.GetDimensions(width, height);
        float2 dx = ddx(uv * float2(width,height));
        float2 dy = ddy(uv * float2(width,height));
        float lod = clamp(0.5 * log2(max(max(dot(dx,dx), dot(dy,dy)), 1e-8)) + wrap.z, 0, settings.z);
        const bool trilinear = settings.w > 1.5;
        const int mip = trilinear ? (int)floor(lod) : (int)round(lod);
        // A loop rather than two calls, so trilinear filtering doesn't double the code.
        const int levels = trilinear ? 2 : 1;
        [loop]
        for(int level = 0; level < levels; ++level)
        {
            const float4 c = ShellEmissionLevel(tex, data, uv, int2(width,height), min(mip + level, (int)settings.z), settings, wrap, slot);
            color = level == 0 ? c : lerp(color, c, frac(lod));
        }
    }
    return color;
}

#define SHELL_EMISSION_SAMPLE(n, uv, unlocked) ShellEmissionSample(_ShellEmission##n, _ShellEmission##n##Blocks, uv, _ShellEmission##n##Settings, _ShellEmission##n##Wrap, n, unlocked)

// For Poiyomi, where the sample replaces an expression. Both sides of ?: are evaluated, so a slot that isn't encrypted
// would still decrypt; this branches instead and checks the key only for an encrypted slot.
float4 ShellEmissionSampleOr(Texture2D tex, Texture2D data, float2 uv, float4 settings, float4 wrap, uint slot, float4 original)
{
    float4 color = original;
    UNITY_BRANCH
    if(settings.x > 0.5)
        color = ShellEmissionSample(tex, data, uv, settings, wrap, slot, ShellIsDecrypted());
    return color;
}

#define SHELL_EMISSION_SAMPLE_OR(n, uv, original) ShellEmissionSampleOr(_ShellEmission##n, _ShellEmission##n##Blocks, uv, _ShellEmission##n##Settings, _ShellEmission##n##Wrap, n, original)
