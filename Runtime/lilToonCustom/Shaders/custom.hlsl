//----------------------------------------------------------------------------------------------------------------------
// Macro

// Custom variables
//#define LIL_CUSTOM_PROPERTIES \
//    half _CustomVariable;
#define LIL_CUSTOM_PROPERTIES \
	half4 _MainTex_TexelSize;

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
	half4 mip_texture = tex2D(_MipTex, fd.uvMain);\
	\
	half2 uv_unit = _MainTex_TexelSize.xy;\
	const int code = 0;\
	\
	if(code == 0)\
	{\
		half2 uv_bilinear = fd.uvMain - 0.5 * uv_unit;\
		int mip = round(mip_texture.r * 255 / 10);\
		int m[13] = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 10, 10 };\
		\
		half4 c00 = DecryptTextureXXTEA(uv_bilinear + half2(uv_unit.x * 0, uv_unit.y * 0), m[mip]);\
		half4 c10 = DecryptTextureXXTEA(uv_bilinear + half2(uv_unit.x * 1, uv_unit.y * 0), m[mip]);\
		half4 c01 = DecryptTextureXXTEA(uv_bilinear + half2(uv_unit.x * 0, uv_unit.y * 1), m[mip]);\
		half4 c11 = DecryptTextureXXTEA(uv_bilinear + half2(uv_unit.x * 1, uv_unit.y * 1), m[mip]);\
		\
		half2 f = frac(uv_bilinear * _MainTex_TexelSize.zw);\
		\
		half4 c0 = lerp(c00, c10, f.x);\
		half4 c1 = lerp(c01, c11, f.x);\
		\
		half4 bilinear = lerp(c0, c1, f.y);\
		\
		fd.col = bilinear;\
	}\
	else if(code == 1)\
	{\
		half2 uv_bilinear = fd.uvMain - 0.5 * uv_unit;\
		int mip = round(mip_texture.r * 255 / 10);\
		int m[13] = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 10, 10 };\
		\
		half4 c00 = DecryptTextureXXTEARGBA(uv_bilinear + half2(uv_unit.x * 0, uv_unit.y * 0), m[mip]);\
		half4 c10 = DecryptTextureXXTEARGBA(uv_bilinear + half2(uv_unit.x * 1, uv_unit.y * 0), m[mip]);\
		half4 c01 = DecryptTextureXXTEARGBA(uv_bilinear + half2(uv_unit.x * 0, uv_unit.y * 1), m[mip]);\
		half4 c11 = DecryptTextureXXTEARGBA(uv_bilinear + half2(uv_unit.x * 1, uv_unit.y * 1), m[mip]);\
		\
		half2 f = frac(uv_bilinear * _MainTex_TexelSize.zw);\
		\
		half4 c0 = lerp(c00, c10, f.x);\
		half4 c1 = lerp(c01, c11, f.x);\
		\
		half4 bilinear = lerp(c0, c1, f.y);\
		\
		fd.col = bilinear;\
	}\
	else if(code == 2)\
	{\
		half2 uv = fd.uvMain;\
		int mip = round(mip_texture.r * 255 / 10);\
		int m[13] = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 10, 10 };\
		\
		half4 c00 = DecryptTextureXXTEA(uv, m[mip]);\
		fd.col = c00;\
	}\
	else if(code == 3)\
	{\
		half2 uv = fd.uvMain;\
		int mip = round(mip_texture.r * 255 / 10);\
		int m[13] = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 10, 10 };\
		\
		half4 c00 = DecryptTextureXXTEARGBA(uv, m[mip]);\
		fd.col = c00;\
	}\
	else if(code == 4)\
	{\
		half2 uv_bilinear = fd.uvMain - 0.5 * uv_unit;\
		int mip = round(mip_texture.r * 255 / 10);\
		int m[13] = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 10, 10 };\
		\
		half4 c00 = DecryptTextureXXTEADXT(uv_bilinear + half2(uv_unit.x * 0, uv_unit.y * 0), m[mip]);\
		half4 c10 = DecryptTextureXXTEADXT(uv_bilinear + half2(uv_unit.x * 1, uv_unit.y * 0), m[mip]);\
		half4 c01 = DecryptTextureXXTEADXT(uv_bilinear + half2(uv_unit.x * 0, uv_unit.y * 1), m[mip]);\
		half4 c11 = DecryptTextureXXTEADXT(uv_bilinear + half2(uv_unit.x * 1, uv_unit.y * 1), m[mip]);\
		\
		half2 f = frac(uv_bilinear * _MainTex_TexelSize.zw);\
		\
		half4 c0 = lerp(c00, c10, f.x);\
		half4 c1 = lerp(c01, c11, f.x);\
		\
		half4 bilinear = lerp(c0, c1, f.y);\
		\
		fd.col = bilinear;\
	}\
	else if(code == 5)\
	{\
		half2 uv = fd.uvMain;\
		int mip = round(mip_texture.r * 255 / 10);\
		int m[13] = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 10, 10 };\
		\
		half4 c00 = DecryptTextureXXTEADXT(uv, m[mip]);\
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
		half4 mip_texture = tex2D(_MipTex, fd.uvMain);\
		half2 uv = fd.uv0;\
		int mip = round(mip_texture.r * 255 / 10);\
		int m[13] = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 10, 10 };\
		\
		half4 c00 = DecryptTextureXXTEADXT(uv, m[mip]);\
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
				half4 rimColor = _RimColor;\
				half4 rimIndirColor = _RimIndirColor;
			#define A2\
				half4 rimColorTex = fd.col;\
				rimColor *= rimColorTex;\
				rimIndirColor *= rimColorTex;
			#define A3 \
				rimColor.rgb = lerp(rimColor.rgb, rimColor.rgb * fd.albedo, _RimMainStrength);\
				\
				half3 V = lilBlendVRParallax(fd.headV, fd.V, _RimVRParallaxStrength);\
				\
				half3 N = fd.N;
			#if defined(LIL_FEATURE_NORMAL_1ST) || defined(LIL_FEATURE_NORMAL_2ND)
				#define A4 \
					N = lerp(fd.origN, fd.N, _RimNormalStrength);
			#else
				#define A4 true;
			#endif
			#define A5 \
				half nvabs = abs(dot(N,V));\
				half lnRaw = dot(fd.L, N) * 0.5 + 0.5;\
				half lnDir = saturate((lnRaw + _RimDirRange) / (1.0 + _RimDirRange));\
				half lnIndir = saturate((1.0-lnRaw + _RimIndirRange) / (1.0 + _RimIndirRange));\
				half rim = pow(saturate(1.0 - nvabs), _RimFresnelPower);\
				rim = fd.facing < (_RimBackfaceMask-1.0) ? 0.0 : rim;\
				half rimDir = lerp(rim, rim*lnDir, _RimDirStrength);\
				half rimIndir = rim * lnIndir * _RimDirStrength;\
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
					half3 rimLightMul = 1 - _RimEnableLighting + fd.lightColor * _RimEnableLighting;
			#else
				#define A7 \
					half3 rimLightMul = _RimBlendMode < 3 ? fd.lightColor * _RimEnableLighting : 1;
			#endif
			#define A8 \
				fd.col.rgb = lilBlendColor(fd.col.rgb, rimColor.rgb * rimLightMul, rimDir * rimColor.a, _RimBlendMode);\
				fd.col.rgb = lilBlendColor(fd.col.rgb, rimIndirColor.rgb * rimLightMul, rimIndir * rimIndirColor.a, _RimBlendMode);
			#define A9 true;
		#else
			#define A1\
				half4 rimColor = _RimColor;
			#define A2 \
				rimColor *= fd.col;
			#define A3 \
				rimColor.rgb = lerp(rimColor.rgb, rimColor.rgb * fd.albedo, _RimMainStrength);\
				half3 N = fd.N;
			#if defined(LIL_FEATURE_NORMAL_1ST) || defined(LIL_FEATURE_NORMAL_2ND)
				#define A4 \
					N = lerp(fd.origN, fd.N, _RimNormalStrength);
			#else
				#define A4 true;
			#endif
			#define A5 \
				half nvabs = abs(dot(N,fd.V));\
				half rim = pow(saturate(1.0 - nvabs), _RimFresnelPower);\
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
// half4   input.positionOS        POSITION
// half2   input.uv0               TEXCOORD0
// half2   input.uv1               TEXCOORD1
// half2   input.uv2               TEXCOORD2
// half2   input.uv3               TEXCOORD3
// half2   input.uv4               TEXCOORD4
// half2   input.uv5               TEXCOORD5
// half2   input.uv6               TEXCOORD6
// half2   input.uv7               TEXCOORD7
// half4   input.color             COLOR
// half3   input.normalOS          NORMAL
// half4   input.tangentOS         TANGENT
// uint     vertexID                SV_VertexID

//----------------------------------------------------------------------------------------------------------------------
// Vertex shader outputs or pixel shader inputs (v2f structure)
//
// The structure depends on the pass.
// Please check lil_pass_xx.hlsl for details.
//
// Type     Name                    Description
// -------- ----------------------- --------------------------------------------------------------------
// half4   output.positionCS       SV_POSITION
// half2   output.uv01             TEXCOORD0 TEXCOORD1
// half2   output.uv23             TEXCOORD2 TEXCOORD3
// half3   output.positionOS       object space position
// half3   output.positionWS       world space position
// half3   output.normalWS         world space normal
// half4   output.tangentWS        world space tangent

//----------------------------------------------------------------------------------------------------------------------
// Variables commonly used in the forward pass
//
// These are members of `lilFragData fd`
//
// Type     Name                    Description
// -------- ----------------------- --------------------------------------------------------------------
// half4   col                     lit color
// half3   albedo                  unlit color
// half3   emissionColor           color of emission
// -------- ----------------------- --------------------------------------------------------------------
// half3   lightColor              color of light
// half3   indLightColor           color of indirectional light
// half3   addLightColor           color of additional light
// half    attenuation             attenuation of light
// half3   invLighting             saturate((1.0 - lightColor) * sqrt(lightColor));
// -------- ----------------------- --------------------------------------------------------------------
// half2   uv0                     TEXCOORD0
// half2   uv1                     TEXCOORD1
// half2   uv2                     TEXCOORD2
// half2   uv3                     TEXCOORD3
// half2   uvMain                  Main UV
// half2   uvMat                   MatCap UV
// half2   uvRim                   Rim Light UV
// half2   uvPanorama              Panorama UV
// half2   uvScn                   Screen UV
// bool     isRightHand             input.tangentWS.w > 0.0;
// -------- ----------------------- --------------------------------------------------------------------
// half3   positionOS              object space position
// half3   positionWS              world space position
// half4   positionCS              clip space position
// half4   positionSS              screen space position
// half    depth                   distance from camera
// -------- ----------------------- --------------------------------------------------------------------
// half3x3 TBN                     tangent / bitangent / normal matrix
// half3   T                       tangent direction
// half3   B                       bitangent direction
// half3   N                       normal direction
// half3   V                       view direction
// half3   L                       light direction
// half3   origN                   normal direction without normal map
// half3   origL                   light direction without sh light
// half3   headV                   middle view direction of 2 cameras
// half3   reflectionN             normal direction for reflection
// half3   matcapN                 normal direction for reflection for MatCap
// half3   matcap2ndN              normal direction for reflection for MatCap 2nd
// half    facing                  VFACE
// -------- ----------------------- --------------------------------------------------------------------
// half    vl                      dot(viewDirection, lightDirection);
// half    hl                      dot(headDirection, lightDirection);
// half    ln                      dot(lightDirection, normalDirection);
// half    nv                      saturate(dot(normalDirection, viewDirection));
// half    nvabs                   abs(dot(normalDirection, viewDirection));
// -------- ----------------------- --------------------------------------------------------------------
// half4   triMask                 TriMask (for lite version)
// half3   parallaxViewDirection   mul(tbnWS, viewDirection);
// half2   parallaxOffset          parallaxViewDirection.xy / (parallaxViewDirection.z+0.5);
// half    anisotropy              strength of anisotropy
// half    smoothness              smoothness
// half    roughness               roughness
// half    perceptualRoughness     perceptual roughness
// half    shadowmix               this variable is 0 in the shadow area
// half    audioLinkValue          volume acquired by AudioLink
// -------- ----------------------- --------------------------------------------------------------------
// uint     renderingLayers         light layer of object (for URP / HDRP)
// uint     featureFlags            feature flags (for HDRP)
// uint2    tileIndex               tile index (for HDRP)