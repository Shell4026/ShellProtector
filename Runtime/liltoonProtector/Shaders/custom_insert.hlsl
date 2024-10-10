#pragma shader_feature_local _XXTEA
#pragma shader_feature_local _FORMAT0
#pragma shader_feature_local _FORMAT1
#pragma shader_feature_local _POINT
#pragma shader_feature_local _LIMLIGHT_ENCRYPTED

//format = format1 | format0 << 1
//format 00:DXT, 01:RGB, 10:RGBA

#ifdef _XXTEA
	#include "Decrypt.cginc"
#else
	#include "DecryptChacha.cginc"
#endif
#include "UnityCG.cginc"