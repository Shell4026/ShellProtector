//----------------------------------------------------------------------------------------------------------------------
// Macro

// Custom variables
//#define LIL_CUSTOM_PROPERTIES \
//    float _CustomVariable;
#define LIL_CUSTOM_PROPERTIES \
	float4 _MainTex_TexelSize;

// Custom textures
#define LIL_CUSTOM_TEXTURES \
	SAMPLER(_MipTex); \
	TEXTURE2D(_EncryptTex);
// Add vertex shader input
//#define LIL_REQUIRE_APP_POSITION
//#define LIL_REQUIRE_APP_TEXCOORD0
//#define LIL_REQUIRE_APP_TEXCOORD1
//#define LIL_REQUIRE_APP_TEXCOORD2
//#define LIL_REQUIRE_APP_TEXCOORD3
//#define LIL_REQUIRE_APP_TEXCOORD4
//#define LIL_REQUIRE_APP_TEXCOORD5
//#define LIL_REQUIRE_APP_TEXCOORD6
//#define LIL_REQUIRE_APP_TEXCOORD7
//#define LIL_REQUIRE_APP_COLOR
//#define LIL_REQUIRE_APP_NORMAL
//#define LIL_REQUIRE_APP_TANGENT
//#define LIL_REQUIRE_APP_VERTEXID

// Add vertex shader output
//#define LIL_V2F_FORCE_TEXCOORD0
//#define LIL_V2F_FORCE_TEXCOORD1
//#define LIL_V2F_FORCE_POSITION_OS
//#define LIL_V2F_FORCE_POSITION_WS
//#define LIL_V2F_FORCE_POSITION_SS
//#define LIL_V2F_FORCE_NORMAL
//#define LIL_V2F_FORCE_TANGENT
//#define LIL_V2F_FORCE_BITANGENT
//#define LIL_CUSTOM_V2F_MEMBER(id0,id1,id2,id3,id4,id5,id6,id7)

// Add vertex copy
#define LIL_CUSTOM_VERT_COPY

// Inserting a process into the vertex shader
//#define LIL_CUSTOM_VERTEX_OS
//#define LIL_CUSTOM_VERTEX_WS

// Inserting a process into pixel shader
//#define BEFORE_xx
#define OVERRIDE_MAIN\
	LIL_GET_MAIN_TEX\
	float4 mip_texture = tex2D(_MipTex, fd.uvMain);\
	\
	float2 uv_unit = _MainTex_TexelSize.xy;\
	const int code = 0;\
	\
	if(code == 0)\
	{\
		float2 uv_bilinear = fd.uvMain - 0.5 * uv_unit;\
		int mip = round(mip_texture.r * 255 / 10);\
		int m[13] = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 10, 10 };\
		\
		float4 c00 = DecryptTextureXXTEA(uv_bilinear + float2(uv_unit.x * 0, uv_unit.y * 0), m[mip]);\
		float4 c10 = DecryptTextureXXTEA(uv_bilinear + float2(uv_unit.x * 1, uv_unit.y * 0), m[mip]);\
		float4 c01 = DecryptTextureXXTEA(uv_bilinear + float2(uv_unit.x * 0, uv_unit.y * 1), m[mip]);\
		float4 c11 = DecryptTextureXXTEA(uv_bilinear + float2(uv_unit.x * 1, uv_unit.y * 1), m[mip]);\
		\
		float2 f = frac(uv_bilinear * _MainTex_TexelSize.zw);\
		\
		float4 c0 = lerp(c00, c10, f.x);\
		float4 c1 = lerp(c01, c11, f.x);\
		\
		float4 bilinear = lerp(c0, c1, f.y);\
		\
		fd.col = bilinear;\
	}\
	else if(code == 1)\
	{\
		float2 uv_bilinear = fd.uvMain - 0.5 * uv_unit;\
		int mip = round(mip_texture.r * 255 / 10);\
		int m[13] = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 10, 10 };\
		\
		float4 c00 = DecryptTextureXXTEARGBA(uv_bilinear + float2(uv_unit.x * 0, uv_unit.y * 0), m[mip]);\
		float4 c10 = DecryptTextureXXTEARGBA(uv_bilinear + float2(uv_unit.x * 1, uv_unit.y * 0), m[mip]);\
		float4 c01 = DecryptTextureXXTEARGBA(uv_bilinear + float2(uv_unit.x * 0, uv_unit.y * 1), m[mip]);\
		float4 c11 = DecryptTextureXXTEARGBA(uv_bilinear + float2(uv_unit.x * 1, uv_unit.y * 1), m[mip]);\
		\
		float2 f = frac(uv_bilinear * _MainTex_TexelSize.zw);\
		\
		float4 c0 = lerp(c00, c10, f.x);\
		float4 c1 = lerp(c01, c11, f.x);\
		\
		float4 bilinear = lerp(c0, c1, f.y);\
		\
		fd.col = bilinear;\
	}\
	else if(code == 2)\
	{\
		float2 uv = fd.uvMain;\
		int mip = round(mip_texture.r * 255 / 10);\
		int m[13] = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 10, 10 };\
		\
		float4 c00 = DecryptTextureXXTEA(uv, m[mip]);\
		fd.col = c00;\
	}\
	else if(code == 3)\
	{\
		float2 uv = fd.uvMain;\
		int mip = round(mip_texture.r * 255 / 10);\
		int m[13] = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 10, 10 };\
		\
		float4 c00 = DecryptTextureXXTEARGBA(uv, m[mip]);\
		fd.col = c00;\
	}\
	else if(code == 4)\
	{\
		float2 uv_bilinear = fd.uvMain - 0.5 * uv_unit;\
		int mip = round(mip_texture.r * 255 / 10);\
		int m[13] = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 10, 10 };\
		\
		float4 c00 = DecryptTextureXXTEADXT(uv_bilinear + float2(uv_unit.x * 0, uv_unit.y * 0), m[mip]);\
		float4 c10 = DecryptTextureXXTEADXT(uv_bilinear + float2(uv_unit.x * 1, uv_unit.y * 0), m[mip]);\
		float4 c01 = DecryptTextureXXTEADXT(uv_bilinear + float2(uv_unit.x * 0, uv_unit.y * 1), m[mip]);\
		float4 c11 = DecryptTextureXXTEADXT(uv_bilinear + float2(uv_unit.x * 1, uv_unit.y * 1), m[mip]);\
		\
		float2 f = frac(uv_bilinear * _MainTex_TexelSize.zw);\
		\
		float4 c0 = lerp(c00, c10, f.x);\
		float4 c1 = lerp(c01, c11, f.x);\
		\
		float4 bilinear = lerp(c0, c1, f.y);\
		\
		fd.col = bilinear;\
	}\
	else if(code == 5)\
	{\
		float2 uv = fd.uvMain;\
		int mip = round(mip_texture.r * 255 / 10);\
		int m[13] = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 10, 10 };\
		\
		float4 c00 = DecryptTextureXXTEADXT(uv, m[mip]);\
		fd.col = c00;\
	}\
	\
	LIL_APPLY_MAIN_TONECORRECTION\
	fd.col *= _Color;
	
#define OVERRIDE_MATCAP \
	lilGetMatCap(fd, _MipTex);

#if defined(OUTLINE_ENCRYPTED)
	#define OVERRIDE_OUTLINE_COLOR \
		LIL_GET_OUTLINE_TEX \
		fd.col *= 0.0001;\
		float4 mip_texture = tex2D(_MipTex, fd.uvMain);\
		float2 uv = fd.uv0;\
		int mip = round(mip_texture.r * 255 / 10);\
		int m[13] = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 10, 10 };\
		\
		float4 c00 = DecryptTextureXXTEADXT(uv, m[mip]);\
		fd.col += c00;\
		LIL_APPLY_OUTLINE_TONECORRECTION \
		LIL_APPLY_OUTLINE_COLOR
#endif
	
#if defined(LIMLIGHT_ENCRYPTED)
	#if defined(LIL_LITE)
		#define OVERRIDE_RIMLIGHT \
			lilGetRim(fd);
	#else
		#if !defined(LIL_FEATURE_RIMLIGHT_DIRECTION)
			#define A1\
				float4 rimColor = _RimColor;\
				float4 rimIndirColor = _RimIndirColor;
			#define A2\
				float4 rimColorTex = fd.col;\
				rimColor *= rimColorTex;\
				rimIndirColor *= rimColorTex;
			#define A3 \
				rimColor.rgb = lerp(rimColor.rgb, rimColor.rgb * fd.albedo, _RimMainStrength);\
				\
				float3 V = lilBlendVRParallax(fd.headV, fd.V, _RimVRParallaxStrength);\
				\
				float3 N = fd.N;
			#if defined(LIL_FEATURE_NORMAL_1ST) || defined(LIL_FEATURE_NORMAL_2ND)
				#define A4 \
					N = lerp(fd.origN, fd.N, _RimNormalStrength);
			#else
				#define A4 true;
			#endif
			#define A5 \
				float nvabs = abs(dot(N,V));\
				float lnRaw = dot(fd.L, N) * 0.5 + 0.5;\
				float lnDir = saturate((lnRaw + _RimDirRange) / (1.0 + _RimDirRange));\
				float lnIndir = saturate((1.0-lnRaw + _RimIndirRange) / (1.0 + _RimIndirRange));\
				float rim = pow(saturate(1.0 - nvabs), _RimFresnelPower);\
				rim = fd.facing < (_RimBackfaceMask-1.0) ? 0.0 : rim;\
				float rimDir = lerp(rim, rim*lnDir, _RimDirStrength);\
				float rimIndir = rim * lnIndir * _RimDirStrength;\
				rimDir = lilTooningScale(_AAStrength, rimDir, _RimBorder, _RimBlur);\
				rimIndir = lilTooningScale(_AAStrength, rimIndir, _RimIndirBorder, _RimIndirBlur);\
				rimDir = lerp(rimDir, rimDir * fd.shadowmix, _RimShadowMask);\
				rimIndir = lerp(rimIndir, rimIndir * fd.shadowmix, _RimShadowMask);
			#if LIL_RENDER == 2 && !defined(LIL_REFRACTION)
				#define A6 \
					if(_RimApplyTransparency)\
					{\
						rimDir *= fd.col.a;\
						rimIndir *= fd.col.a;\
					}
			#else
				#define A6 true;
			#endif
			#if !defined(LIL_PASS_FORWARDADD)
				#define A7 \
					float3 rimLightMul = 1 - _RimEnableLighting + fd.lightColor * _RimEnableLighting;
			#else
				#define A7 \
					float3 rimLightMul = _RimBlendMode < 3 ? fd.lightColor * _RimEnableLighting : 1;
			#endif
			#define A8 \
				fd.col.rgb = lilBlendColor(fd.col.rgb, rimColor.rgb * rimLightMul, rimDir * rimColor.a, _RimBlendMode);\
				fd.col.rgb = lilBlendColor(fd.col.rgb, rimIndirColor.rgb * rimLightMul, rimIndir * rimIndirColor.a, _RimBlendMode);
			#define A9 true;
		#else
			#define A1\
				float4 rimColor = _RimColor;
			#define A2 \
				rimColor *= fd.col;
			#define A3 \
				rimColor.rgb = lerp(rimColor.rgb, rimColor.rgb * fd.albedo, _RimMainStrength);\
				float3 N = fd.N;
			#if defined(LIL_FEATURE_NORMAL_1ST) || defined(LIL_FEATURE_NORMAL_2ND)
				#define A4 \
					N = lerp(fd.origN, fd.N, _RimNormalStrength);
			#else
				#define A4 true;
			#endif
			#define A5 \
				float nvabs = abs(dot(N,fd.V));\
				float rim = pow(saturate(1.0 - nvabs), _RimFresnelPower);\
				rim = fd.facing < (_RimBackfaceMask-1.0) ? 0.0 : rim;\
				rim = lilTooningScale(_AAStrength, rim, _RimBorder, _RimBlur);
			#if LIL_RENDER == 2 && !defined(LIL_REFRACTION)
				#define A6 \
					if(_RimApplyTransparency) rim *= fd.col.a;
			#else
				#define A6 true;
			#endif
			#define A7 \
				rim = lerp(rim, rim * fd.shadowmix, _RimShadowMask);
			#if !defined(LIL_PASS_FORWARDADD)
				#define A8 \
					rimColor.rgb = lerp(rimColor.rgb, rimColor.rgb * fd.lightColor, _RimEnableLighting);
			#else
				#define A8 \
					if(_RimBlendMode < 3) rimColor.rgb *= fd.lightColor * _RimEnableLighting;
			#endif
			#define A9 \
				fd.col.rgb = lilBlendColor(fd.col.rgb, rimColor.rgb, rim * rimColor.a, _RimBlendMode);
		#endif
	
		#define OVERRIDE_RIMLIGHT \
			if(_UseRim) {\
				A1\
				A2\
				A3\
				A4\
				A5\
				A6\
				A7\
				A8\
				A9\
			}
	#endif
#endif
	
//----------------------------------------------------------------------------------------------------------------------
// Information about variables
//----------------------------------------------------------------------------------------------------------------------

//----------------------------------------------------------------------------------------------------------------------
// Vertex shader inputs (appdata structure)
//
// Type     Name                    Description
// -------- ----------------------- --------------------------------------------------------------------
// float4   input.positionOS        POSITION
// float2   input.uv0               TEXCOORD0
// float2   input.uv1               TEXCOORD1
// float2   input.uv2               TEXCOORD2
// float2   input.uv3               TEXCOORD3
// float2   input.uv4               TEXCOORD4
// float2   input.uv5               TEXCOORD5
// float2   input.uv6               TEXCOORD6
// float2   input.uv7               TEXCOORD7
// float4   input.color             COLOR
// float3   input.normalOS          NORMAL
// float4   input.tangentOS         TANGENT
// uint     vertexID                SV_VertexID

//----------------------------------------------------------------------------------------------------------------------
// Vertex shader outputs or pixel shader inputs (v2f structure)
//
// The structure depends on the pass.
// Please check lil_pass_xx.hlsl for details.
//
// Type     Name                    Description
// -------- ----------------------- --------------------------------------------------------------------
// float4   output.positionCS       SV_POSITION
// float2   output.uv01             TEXCOORD0 TEXCOORD1
// float2   output.uv23             TEXCOORD2 TEXCOORD3
// float3   output.positionOS       object space position
// float3   output.positionWS       world space position
// float3   output.normalWS         world space normal
// float4   output.tangentWS        world space tangent

//----------------------------------------------------------------------------------------------------------------------
// Variables commonly used in the forward pass
//
// These are members of `lilFragData fd`
//
// Type     Name                    Description
// -------- ----------------------- --------------------------------------------------------------------
// float4   col                     lit color
// float3   albedo                  unlit color
// float3   emissionColor           color of emission
// -------- ----------------------- --------------------------------------------------------------------
// float3   lightColor              color of light
// float3   indLightColor           color of indirectional light
// float3   addLightColor           color of additional light
// float    attenuation             attenuation of light
// float3   invLighting             saturate((1.0 - lightColor) * sqrt(lightColor));
// -------- ----------------------- --------------------------------------------------------------------
// float2   uv0                     TEXCOORD0
// float2   uv1                     TEXCOORD1
// float2   uv2                     TEXCOORD2
// float2   uv3                     TEXCOORD3
// float2   uvMain                  Main UV
// float2   uvMat                   MatCap UV
// float2   uvRim                   Rim Light UV
// float2   uvPanorama              Panorama UV
// float2   uvScn                   Screen UV
// bool     isRightHand             input.tangentWS.w > 0.0;
// -------- ----------------------- --------------------------------------------------------------------
// float3   positionOS              object space position
// float3   positionWS              world space position
// float4   positionCS              clip space position
// float4   positionSS              screen space position
// float    depth                   distance from camera
// -------- ----------------------- --------------------------------------------------------------------
// float3x3 TBN                     tangent / bitangent / normal matrix
// float3   T                       tangent direction
// float3   B                       bitangent direction
// float3   N                       normal direction
// float3   V                       view direction
// float3   L                       light direction
// float3   origN                   normal direction without normal map
// float3   origL                   light direction without sh light
// float3   headV                   middle view direction of 2 cameras
// float3   reflectionN             normal direction for reflection
// float3   matcapN                 normal direction for reflection for MatCap
// float3   matcap2ndN              normal direction for reflection for MatCap 2nd
// float    facing                  VFACE
// -------- ----------------------- --------------------------------------------------------------------
// float    vl                      dot(viewDirection, lightDirection);
// float    hl                      dot(headDirection, lightDirection);
// float    ln                      dot(lightDirection, normalDirection);
// float    nv                      saturate(dot(normalDirection, viewDirection));
// float    nvabs                   abs(dot(normalDirection, viewDirection));
// -------- ----------------------- --------------------------------------------------------------------
// float4   triMask                 TriMask (for lite version)
// float3   parallaxViewDirection   mul(tbnWS, viewDirection);
// float2   parallaxOffset          parallaxViewDirection.xy / (parallaxViewDirection.z+0.5);
// float    anisotropy              strength of anisotropy
// float    smoothness              smoothness
// float    roughness               roughness
// float    perceptualRoughness     perceptual roughness
// float    shadowmix               this variable is 0 in the shadow area
// float    audioLinkValue          volume acquired by AudioLink
// -------- ----------------------- --------------------------------------------------------------------
// uint     renderingLayers         light layer of object (for URP / HDRP)
// uint     featureFlags            feature flags (for HDRP)
// uint2    tileIndex               tile index (for HDRP)