#include "../../Shader/Protector.cginc"
#include "UnityCG.cginc"
#include "../../Shader/Emission.cginc"

#if defined(LIL_FEATURE_EMISSION_1ST) || defined(LIL_LITE)
float4 ShellSample_EmissionMap(float2 uv)
{
    UNITY_BRANCH
    if(_ShellEmission0Settings.x > 0.5)
        return SHELL_EMISSION_SAMPLE(0, uv, ShellIsDecrypted());
    return LIL_SAMPLE_2D(_EmissionMap, sampler_EmissionMap, uv);
}
#endif

#if defined(LIL_FEATURE_EMISSION_2ND) && !defined(LIL_LITE)
float4 ShellSample_Emission2ndMap(float2 uv)
{
    UNITY_BRANCH
    if(_ShellEmission1Settings.x > 0.5)
        return SHELL_EMISSION_SAMPLE(1, uv, ShellIsDecrypted());
    return LIL_SAMPLE_2D(_Emission2ndMap, sampler_Emission2ndMap, uv);
}
#endif

#undef LIL_GET_EMITEX
#if defined(LIL_BAKER) || defined(LIL_WITHOUT_ANIMATION)
    #define LIL_GET_EMITEX(tex,uv) ShellSample##tex(lilCalcUVWithoutAnimation(uv, tex##_ST, tex##_ScrollRotate))
#else
    #define LIL_GET_EMITEX(tex,uv) ShellSample##tex(lilCalcUV(uv, tex##_ST, tex##_ScrollRotate))
#endif
