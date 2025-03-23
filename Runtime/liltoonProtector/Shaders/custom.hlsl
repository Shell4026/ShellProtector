//----------------------------------------------------------------------------------------------------------------------
// Macro

#define LIL_CUSTOM_PROPERTIES\
	half4 _EncryptTex0_TexelSize; \
	fixed _fallback; \
	float4 _Decrypt_complete_key;

// Custom textures
#define LIL_CUSTOM_TEXTURES\
	SAMPLER(_MipTex); \
	TEXTURE2D(_EncryptTex0); \
	SAMPLER(sampler_EncryptTex0); \
	TEXTURE2D(_EncryptTex1);
	
#ifndef _FORMAT0
	#ifndef _FORMAT1
		#define DECRYPT DecryptTextureDXT
	#else
		#define DECRYPT DecryptTextureRGBA
	#endif
#else
	#ifndef _FORMAT1
		#define DECRYPT DecryptTexture
	#endif
#endif
	
#ifdef _POINT
	#define CODE\
		half4 mip_texture = tex2D(_MipTex, fd.uvMain);\
		int mip = round(mip_texture.r * 255 / 10);\
		int m[13] = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 10, 10 };\
		\
		half4 c00 = DECRYPT(fd.uvMain, m[mip]);\
		\
		fd.col = c00;
#else
	#define CODE\
		half4 mip_texture = tex2D(_MipTex, fd.uvMain);\
		\
		half2 uv_unit = _EncryptTex0_TexelSize.xy;\
		half2 uv_bilinear = fd.uvMain - 0.5 * uv_unit;\
		int mip = round(mip_texture.r * 255 / 10);\
		static const int m[13] = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 10, 10 };\
		\
		half4 c00 = DECRYPT(uv_bilinear + half2(uv_unit.x * 0, uv_unit.y * 0), m[mip]);\
		half4 c10 = DECRYPT(uv_bilinear + half2(uv_unit.x * 1, uv_unit.y * 0), m[mip]);\
		half4 c01 = DECRYPT(uv_bilinear + half2(uv_unit.x * 0, uv_unit.y * 1), m[mip]);\
		half4 c11 = DECRYPT(uv_bilinear + half2(uv_unit.x * 1, uv_unit.y * 1), m[mip]);\
		\
		half2 f = frac(uv_bilinear * _EncryptTex0_TexelSize.zw);\
		\
		half4 c0 = lerp(c00, c10, f.x);\
		half4 c1 = lerp(c01, c11, f.x);\
		\
		half4 bilinear = lerp(c0, c1, f.y);\
		\
		fd.col = bilinear;\
		LIL_APPLY_MAIN_TONECORRECTION\
		fd.col *= _Color;
#endif

#define DECRYPT_CHECK_FUNC \
	(all(abs(_Decrypt_complete_key - HashFloat4(\
		float4(_Key0, _Key1, _Key2, _Key3), \
		float4(_Key4, _Key5, _Key6, _Key7), \
		float4(_Key8, _Key9, _Key10, _Key11), \
		float4(_Key12, _Key13, _Key14, _Key15) \
	)) < 1e-1))

#define OVERRIDE_MAIN\
	LIL_GET_MAIN_TEX\
	UNITY_BRANCH\
	if(DECRYPT_CHECK_FUNC == false)\
	{\
		LIL_APPLY_MAIN_TONECORRECTION\
		fd.col *= _Color;\
	}\
	else\
	{\
		CODE\
	}
	
#define OVERRIDE_MATCAP \
	lilGetMatCap(fd, _MipTex);
	
#if defined(_LIMLIGHT_ENCRYPTED)
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