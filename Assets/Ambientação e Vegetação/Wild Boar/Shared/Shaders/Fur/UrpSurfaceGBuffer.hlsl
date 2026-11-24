#ifndef RIPVERTICES_URP_SURFACE_GBUFFER_INCLUDED
#define RIPVERTICES_URP_SURFACE_GBUFFER_INCLUDED

#if UNITY_VERSION >= 600100
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GBufferOutput.hlsl"
#define RipVerticesGBufferOutput GBufferFragOutput
#define RipVerticesPackGBuffer PackGBuffersBRDFData
#else
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl"
#define RipVerticesGBufferOutput FragmentOutput
#define RipVerticesPackGBuffer BRDFDataToGbuffer
#endif

half3 FurDeferredTransmission(Light light, half3 viewWS, half normalView)
{
#if defined(_LIGHT_LAYERS)
    if (!IsMatchingLightLayer(light.layerMask, GetMeshRenderingLayer()))
        return 0;
#endif
    half transmission = FurTransmissionLobe(normalView, dot(-light.direction, viewWS));
    return transmission * light.color * light.distanceAttenuation * light.shadowAttenuation;
}

// Add only the strand transmission. URP still resolves diffuse and specular from the G-buffer.
half3 EvaluateFurDeferredTransmission(InputData inputData, half strength,
    half normalView)
{
    if (strength <= 0)
        return 0;

    float3 positionWS = inputData.positionWS;
    half4 shadowMask = CalculateShadowMask(inputData);
    Light mainLight = GetMainLight(TransformWorldToShadowCoord(positionWS), positionWS, shadowMask);
    half3 transmission = FurDeferredTransmission(mainLight, inputData.viewDirectionWS, normalView);

#if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX)
    #if USE_CLUSTER_LIGHT_LOOP
    UNITY_LOOP for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        Light light = GetAdditionalLight(lightIndex, positionWS, shadowMask);
        transmission += FurDeferredTransmission(light, inputData.viewDirectionWS, normalView);
    }
    #endif
    uint count = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(count)
        Light light = GetAdditionalLight(lightIndex, positionWS, shadowMask);
        transmission += FurDeferredTransmission(light, inputData.viewDirectionWS, normalView);
    LIGHT_LOOP_END
#endif
    return transmission * strength;
}

RipVerticesGBufferOutput WriteSurfaceGBuffer(InputData inputData, SurfaceData surface)
{
    BRDFData brdf;
    InitializeBRDFData(surface, brdf);
    half3 indirect = GlobalIllumination(brdf, inputData.bakedGI, surface.occlusion,
        inputData.positionWS, inputData.normalWS, inputData.viewDirectionWS);
    return RipVerticesPackGBuffer(brdf, inputData, surface.smoothness,
        indirect + surface.emission, surface.occlusion);
}

// The forward and G-buffer passes share the strand mask and all material sampling.
RipVerticesGBufferOutput WriteFurGBuffer(float4 positionCS, float3 positionWS, half3 normalWS,
    half3 albedo, half smoothness, half transmission, half normalView)
{
    InputData inputData = (InputData)0;
    inputData.positionCS = positionCS;
    inputData.positionWS = positionWS;
    inputData.normalWS = normalWS;
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(positionWS);
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);
    inputData.bakedGI = SampleSH(normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(float2(0, 0));

    SurfaceData surface = (SurfaceData)0;
    surface.albedo = albedo;
    surface.smoothness = smoothness;
    surface.occlusion = 1;
    surface.alpha = 1;
    surface.emission = albedo * EvaluateFurDeferredTransmission(inputData, transmission, normalView);
    return WriteSurfaceGBuffer(inputData, surface);
}

#endif
