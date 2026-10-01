Shader "Hidden/ShellProtector/BC7Test"
{
    Properties
    {
        _MainTex ("Main", 2D) = "white" {}
        _MipTex ("Mip", 2D) = "black" {}
        _EncryptTex0 ("Encrypted0", 2D) = "white" {}
        _EncryptTex1 ("Encrypted1", 2D) = "white" {}
        _Key0 ("key0", Float) = 0
        _Key1 ("key1", Float) = 0
        _Key2 ("key2", Float) = 0
        _Key3 ("key3", Float) = 0
        _Key4 ("key4", Float) = 0
        _Key5 ("key5", Float) = 0
        _Key6 ("key6", Float) = 0
        _Key7 ("key7", Float) = 0
        _Key8 ("key8", Float) = 0
        _Key9 ("key9", Float) = 0
        _Key10 ("key10", Float) = 0
        _Key11 ("key11", Float) = 0
        _Key12 ("key12", Float) = 0
        _Key13 ("key13", Float) = 0
        _Key14 ("key14", Float) = 0
        _Key15 ("key15", Float) = 0
        _Woffset ("Woffset", Integer) = 0
        _Hoffset ("Hoffset", Integer) = 0
        _HashMagic ("HashMagic", Integer) = 0
        _PasswordHash ("PasswordHash", Integer) = 0
        _Nonce0 ("Nonce0", Integer) = 0
        _Nonce1 ("Nonce1", Integer) = 0
        _Nonce2 ("Nonce2", Integer) = 0
        _Rounds ("Rounds", Integer) = 0
        _Lod ("Lod", Float) = 0
        _UVTransform ("UV transform", Vector) = (1,1,0,0)
        _LinearReference ("Linear BC7 reference", 2D) = "white" {}
        _ReferenceSize ("Reference size", Vector) = (1,1,1,1)
        _ReferenceSrgb ("Reference sRGB", Integer) = 0
        _ReferenceBilinear ("Reference bilinear", Integer) = 0
        [HideInInspector] _ShellBC7LayoutVersion("BC7 layout", Integer) = 0
        [HideInInspector] _ShellSourceTexelSize("Source size", Vector) = (1,1,1,1)
        [HideInInspector] _ShellSourceSampling("Source sampling", Vector) = (1,1,0,0)
        [HideInInspector] _ShellMipOffsets0("Mip offsets 0", Vector) = (0,0,0,0)
        [HideInInspector] _ShellMipOffsets1("Mip offsets 1", Vector) = (0,0,0,0)
        [HideInInspector] _ShellMipOffsets2("Mip offsets 2", Vector) = (0,0,0,0)
        [HideInInspector] _ShellMipOffsets3("Mip offsets 3", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        CGINCLUDE
        #include "UnityCG.cginc"
        Texture2D _MainTex, _LinearReference, _EncryptTex0, _EncryptTex1, _MipTex;
        SamplerState sampler_MainTex, sampler_LinearReference, point_clamp_sampler;
        float4 _EncryptTex0_TexelSize, _UVTransform, _ReferenceSize;
        float _Lod;
        int _PasswordHash, _ReferenceSrgb, _ReferenceBilinear;
        #include "../../Runtime/Shader/Protector.cginc"

        float2 TestUV(float2 uv) { return uv * _UVTransform.xy + _UVTransform.zw; }
        float TestLinear(float c) { return c <= 0.04045 ? c / 12.92 : pow((c + 0.055) / 1.055, 2.4); }
        float4 ReferencePoint(float2 uv)
        {
            float4 c = round(_LinearReference.SampleLevel(sampler_LinearReference, uv, _Lod) * 255.0) / 255.0;
            #ifndef UNITY_COLORSPACE_GAMMA
            if (_ReferenceSrgb != 0) c.rgb = float3(TestLinear(c.r), TestLinear(c.g), TestLinear(c.b));
            #endif
            return c;
        }
        ENDCG
        Pass { Name "Native" CGPROGRAM
#pragma target 5.0
#pragma vertex vert_img
#pragma fragment frag
#pragma shader_feature_local _SHELL_PROTECTOR_XXTEA
#pragma shader_feature_local _SHELL_PROTECTOR_CHACHA
#pragma shader_feature_local _SHELL_PROTECTOR_FORMAT0
#pragma shader_feature_local _SHELL_PROTECTOR_FORMAT1
            float4 frag(v2f_img i):SV_Target { return _MainTex.SampleLevel(sampler_MainTex, TestUV(i.uv), _Lod); }
        ENDCG }
        Pass { Name "DecryptPoint" CGPROGRAM
#pragma target 5.0
#pragma vertex vert_img
#pragma fragment frag
#pragma shader_feature_local _SHELL_PROTECTOR_XXTEA
#pragma shader_feature_local _SHELL_PROTECTOR_CHACHA
#pragma shader_feature_local _SHELL_PROTECTOR_FORMAT0
#pragma shader_feature_local _SHELL_PROTECTOR_FORMAT1
            float4 frag(v2f_img i):SV_Target { return DecryptTextureBox(_EncryptTex0, _EncryptTex1, point_clamp_sampler, _EncryptTex0_TexelSize, _MipTex, point_clamp_sampler, TestUV(i.uv)); }
        ENDCG }
        Pass { Name "DecryptBilinear" CGPROGRAM
#pragma target 5.0
#pragma vertex vert_img
#pragma fragment frag
#pragma shader_feature_local _SHELL_PROTECTOR_XXTEA
#pragma shader_feature_local _SHELL_PROTECTOR_CHACHA
#pragma shader_feature_local _SHELL_PROTECTOR_FORMAT0
#pragma shader_feature_local _SHELL_PROTECTOR_FORMAT1
            float4 frag(v2f_img i):SV_Target { return DecryptTextureBilinear(_EncryptTex0, _EncryptTex1, point_clamp_sampler, _EncryptTex0_TexelSize, _MipTex, point_clamp_sampler, TestUV(i.uv)); }
        ENDCG }
        Pass { Name "ControlledReference" CGPROGRAM
#pragma target 5.0
#pragma vertex vert_img
#pragma fragment frag
#pragma shader_feature_local _SHELL_PROTECTOR_XXTEA
#pragma shader_feature_local _SHELL_PROTECTOR_CHACHA
#pragma shader_feature_local _SHELL_PROTECTOR_FORMAT0
#pragma shader_feature_local _SHELL_PROTECTOR_FORMAT1
            float4 frag(v2f_img i):SV_Target
            {
                float2 uv = TestUV(i.uv);
                if (_ReferenceBilinear == 0) return ReferencePoint(uv);
                float2 size = max(floor(_ReferenceSize.zw / exp2(_Lod)), 1.0);
                float2 position = uv * size - 0.5, p = (floor(position) + 0.5) / size;
                float4 c00 = ReferencePoint(p), c10 = ReferencePoint(p + float2(1.0 / size.x,0));
                float4 c01 = ReferencePoint(p + float2(0,1.0 / size.y)), c11 = ReferencePoint(p + 1.0 / size);
                float2 f = frac(position);
                return lerp(lerp(c00,c10,f.x),lerp(c01,c11,f.x),f.y);
            }
        ENDCG }
    }
}
