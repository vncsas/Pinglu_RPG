#ifndef FURBRUSH_PIPELINE_INCLUDED
#define FURBRUSH_PIPELINE_INCLUDED

#if defined(FURBRUSH_HDRP_COMPAT)

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonLighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Random.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/PunctualLightCommon.hlsl"
#ifndef UNITY_MATRIX_M
    float4x4 unity_ObjectToWorld;
    #define UNITY_MATRIX_M unity_ObjectToWorld
#endif
#ifndef UNITY_MATRIX_I_M
    float4x4 unity_WorldToObject;
    #define UNITY_MATRIX_I_M unity_WorldToObject
#endif
#ifndef UNITY_MATRIX_V
    float4x4 unity_MatrixV;
    #define UNITY_MATRIX_V unity_MatrixV
#endif
#ifndef UNITY_MATRIX_I_V
    float4x4 unity_MatrixInvV;
    #define UNITY_MATRIX_I_V unity_MatrixInvV
#endif
#ifndef UNITY_MATRIX_P
    float4x4 glstate_matrix_projection;
    #define UNITY_MATRIX_P glstate_matrix_projection
#endif
#ifndef UNITY_MATRIX_I_P
    float4x4 unity_MatrixInvP;
    #define UNITY_MATRIX_I_P unity_MatrixInvP
#endif
#ifndef UNITY_MATRIX_VP
    float4x4 unity_MatrixVP;
    #define UNITY_MATRIX_VP unity_MatrixVP
#endif
#ifndef UNITY_MATRIX_I_VP
    float4x4 unity_MatrixInvVP;
    #define UNITY_MATRIX_I_VP unity_MatrixInvVP
#endif
#ifndef UNITY_PREV_MATRIX_M
    float4x4 unity_MatrixPreviousM;
    #define UNITY_PREV_MATRIX_M unity_MatrixPreviousM
#endif
#ifndef UNITY_PREV_MATRIX_I_M
    float4x4 unity_MatrixPreviousMI;
    #define UNITY_PREV_MATRIX_I_M unity_MatrixPreviousMI
#endif

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"

struct VertexPositionInputs
{
    float3 positionWS;
    float3 positionVS;
    float4 positionCS;
    float4 positionNDC;
};

struct VertexNormalInputs
{
    real3 tangentWS;
    real3 bitangentWS;
    float3 normalWS;
};

VertexPositionInputs GetVertexPositionInputs(float3 positionOS)
{
    VertexPositionInputs input;
    input.positionWS = TransformObjectToWorld(positionOS);
    input.positionVS = TransformWorldToView(input.positionWS);
    input.positionCS = TransformWorldToHClip(input.positionWS);
    input.positionNDC = input.positionCS * 0.5f;
    input.positionNDC.xy = float2(input.positionNDC.x, input.positionNDC.y * _ProjectionParams.x) + input.positionNDC.w;
    input.positionNDC.zw = input.positionCS.zw;
    return input;
}

VertexNormalInputs GetVertexNormalInputs(float3 normalOS, float4 tangentOS)
{
    VertexNormalInputs tbn;
    real sign = tangentOS.w * GetOddNegativeScale();
    tbn.normalWS = TransformObjectToWorldNormal(normalOS);
    tbn.tangentWS = TransformObjectToWorldDir(tangentOS.xyz);
    tbn.bitangentWS = cross(tbn.normalWS, tbn.tangentWS) * sign;
    return tbn;
}

// GetWorldSpaceViewDir is already defined by HDRP/URP libraries
/*
float3 GetWorldSpaceViewDir(float3 positionWS)
{
    return _WorldSpaceCameraPos.xyz - positionWS;
}
*/

real ComputeFogFactor(float zPositionCS)
{
    return 0.0;
}

half3 MixFog(half3 fragColor, half fogFactor)
{
    return fragColor;
}

half3 MixFog(half3 fragColor, float fogFactor)
{
    return fragColor;
}

float3 MixFog(float3 fragColor, float fogFactor)
{
    return fragColor;
}

half3 SampleSH(half3 normalWS)
{
    float3 N = normalWS;
    float3 res = float3(0.0, 0.0, 0.0);
    
    float4 vA = float4(N, 1.0);
    res.r = dot(unity_SHAr, vA);
    res.g = dot(unity_SHAg, vA);
    res.b = dot(unity_SHAb, vA);
    
    float4 vB = N.xyzz * N.yzzx;
    res.r += dot(unity_SHBr, vB);
    res.g += dot(unity_SHBg, vB);
    res.b += dot(unity_SHBb, vB);
    
    float vC = N.x * N.x - N.y * N.y;
    res += unity_SHC.rgb * vC;
    
    // Robust NaN check that fast-math compilers won't optimize out
    if (!(res.x >= -999999.0 && res.x <= 999999.0) ||
        !(res.y >= -999999.0 && res.y <= 999999.0) ||
        !(res.z >= -999999.0 && res.z <= 999999.0))
    {
        res = float3(0.18, 0.18, 0.18);
    }
    
    res = max(float3(0.0, 0.0, 0.0), res);

    // HDRP stores probe lighting in scene-referred physical units, while the
    // camera color buffer is pre-exposed. Without this conversion the probe
    // alone can turn the whole coat white even when the direct light is sane.
    float exposure = GetCurrentExposureMultiplier();
    if (exposure > 0.99f)
    {
        // When scene lighting is toggled off in the editor, exposure is 1.0,
        // but SH coefficients still contain the unexposed physical values (e.g. thousands of Lux).
        // Clamp the ambient to standard LDR levels (max 0.25) to prevent a blinding white glow.
        float maxChan = max(res.r, max(res.g, res.b));
        if (maxChan > 0.25f)
        {
            res *= (0.25f / maxChan);
        }
    }
    else
    {
        res *= exposure;
    }

    return res;
}

half3 GlossyEnvironmentReflection(half3 reflectVector, half perceptualRoughness, half occlusion)
{
    return half3(0.0, 0.0, 0.0);
}

float4 TransformWorldToShadowCoord(float3 positionWS)
{
    return float4(0.0, 0.0, 0.0, 1.0);
}

float3 ApplyShadowBias(float3 positionWS, float3 normalWS, float3 lightDirectionWS)
{
    return positionWS + normalWS * 0.001;
}

struct Light
{
    half3 direction;
    half3 color;
    half distanceAttenuation;
    half shadowAttenuation;
};

Light GetMainLight(float4 shadowCoord)
{
    Light light = (Light)0;
    if (_DirectionalLightCount > 0u)
    {
        uint lightIndex = 0u;
        if (_DirectionalShadowIndex >= 0 && (uint)_DirectionalShadowIndex < _DirectionalLightCount)
            lightIndex = (uint)_DirectionalShadowIndex;

        DirectionalLightData hdLight = _DirectionalLightDatas[lightIndex];
        light.direction = -normalize(hdLight.forward);
        
        float3 lightColor = hdLight.color * hdLight.diffuseDimmer;
        float exposure = GetCurrentExposureMultiplier();
        if (exposure > 0.99f)
        {
            // When scene lighting is toggled off in the editor, exposure is 1.0,
            // but hdLight.color still contains the physical intensity (e.g. 10,000 Lux).
            // Clamp it to a standard LDR intensity to prevent blinding white glow.
            float maxChan = max(lightColor.r, max(lightColor.g, lightColor.b));
            if (maxChan > 1.0f)
            {
                lightColor /= maxChan;
            }
            light.color = lightColor * INV_PI;
        }
        else
        {
            light.color = lightColor * exposure * INV_PI;
        }
        
        light.distanceAttenuation = 1.0;
        light.shadowAttenuation = 1.0;
    }
    return light;
}

uint GetAdditionalLightsCount()
{
    return _PunctualLightCount;
}

Light GetAdditionalLight(uint lightIndex, float3 positionWS)
{
    Light light = (Light)0;
    if (lightIndex < _PunctualLightCount)
    {
        LightData hdLight = _LightDatas[lightIndex];
        float4 distances;
        GetPunctualLightVectors(positionWS, hdLight, light.direction, distances);
        
        float3 lightColor = hdLight.color * hdLight.diffuseDimmer;
        float exposure = GetCurrentExposureMultiplier();
        if (exposure > 0.99f)
        {
            float maxChan = max(lightColor.r, max(lightColor.g, lightColor.b));
            if (maxChan > 1.0f)
            {
                lightColor /= maxChan;
            }
            light.color = lightColor * INV_PI;
        }
        else
        {
            light.color = lightColor * exposure * INV_PI;
        }
        
        light.distanceAttenuation = PunctualLightAttenuation(
            distances,
            hdLight.rangeAttenuationScale,
            hdLight.rangeAttenuationBias,
            hdLight.angleScale,
            hdLight.angleOffset);
        light.shadowAttenuation = 1.0;
    }
    return light;
}

#else

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"

#endif

// Keep the established URP look, but use a response closer to HDRP/Lit for
// physically-scaled HDRP lighting. A small amount of wrap remains so the fur
// does not shade like a hard plastic surface.
// wrapScale (= material _DiffuseWrap, 0..1) scales the pipeline's wrap amount:
// 1 = the established look, 0 = pure Lambert — matching how Lit shades the base
// surface, for when the wrap-brightened fur reads paler/yellower than the skin.
float FurDiffuseLightingTerm(float NdotL, float wrapScale)
{
#if defined(FURBRUSH_HDRP_COMPAT)
    float w = 0.18 * saturate(wrapScale);
#else
    float w = saturate(wrapScale);   // legacy URP look: NdotL*0.5+0.5 == wrap 1.0
#endif
    return saturate((NdotL + w) / (1.0 + w));
}

float FurDiffuseLightingTerm(float NdotL)
{
    return FurDiffuseLightingTerm(NdotL, 1.0);
}

half3 FurAnisotropicSpecular(
    half3 albedo,
    float primary,
    float secondary,
    float intensity,
    float glossMultiplier)
{
#if defined(FURBRUSH_HDRP_COMPAT)
    // Hair reflects a mostly neutral primary lobe, but partially tinting it
    // prevents broad clipped-white bands over a coloured HDRP/Lit surface.
    half3 primaryTint = lerp(half3(1.0, 1.0, 1.0), albedo, 0.35);
    return intensity * (primary * 0.55 * primaryTint
                      + secondary * 0.35 * albedo) * glossMultiplier;
#else
    return intensity * (primary * 0.8 + secondary * 0.5 * albedo) * glossMultiplier;
#endif
}

float FurTransmissionLightingScale()
{
#if defined(FURBRUSH_HDRP_COMPAT)
    return 0.65;
#else
    return 1.0;
#endif
}

half3 FurRootTipTint(half3 rootColor, half3 tipColor, float strandT)
{
#if defined(FURBRUSH_HDRP_COMPAT)
    // Keep each control local to its physical end. A linear lerp makes both colors visibly tint
    // nearly the entire fibre, which is especially obvious under bright punctual HDRP lights.
    float rootWeight = pow(saturate(1.0 - strandT), 4.0);
    float tipWeight  = pow(saturate(strandT), 4.0);
    return lerp(half3(1.0, 1.0, 1.0), rootColor, rootWeight)
         * lerp(half3(1.0, 1.0, 1.0), tipColor, tipWeight);
#else
    return lerp(rootColor, tipColor, strandT);
#endif
}

#endif // FURBRUSH_PIPELINE_INCLUDED
