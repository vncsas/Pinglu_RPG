Shader "Squirrel/Fur Card URP"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base / Albedo (inherited)", 2D) = "white" {}
        [MainColor]   _BaseColor ("Base Color", Color) = (1,1,1,1)
        [Toggle(_SMOOTHNESSMAP)] _UseSmoothnessMap ("Use Smoothness Map", Float) = 0
        _SmoothnessMap ("Smoothness / Roughness", 2D) = "white" {}
        [Toggle(_ROUGHNESS_INPUT)] _RoughnessInput ("Map is Roughness", Float) = 0
        _SmoothnessChannel ("Smoothness Channel (0R 1G 2B 3A)", Float) = 3
        [Toggle(_ALPHATEX)] _UseAlphaTex ("Use Alpha Texture", Float) = 0
        [Toggle(_ALPHALUM)] _AlphaFromLuminance ("Alpha from Grayscale (Luminance)", Float) = 1
        _FurAlpha ("Fur Alpha (strands)", 2D) = "white" {}
        [Toggle] _AlphaFlipY ("Flip Alpha Vertically", Float) = 1
        _AlphaMipBias ("Alpha Mip Bias", Range(-3,0)) = -2

        _RootColor ("Root Shade", Color) = (1,1,1,1)
        _TipColor ("Tip Shade", Color) = (1,1,1,1)
        _AlbedoInfluence ("Albedo Influence", Range(0,1)) = 1
        _RootAO ("Root AO", Range(0,2)) = 0.5

        _Smoothness ("Smoothness", Range(0,1)) = 0.469
        _AnisoSpecular ("Aniso Specular", Range(0,2)) = 0.062
        _Rim ("Rim / Transmission", Range(0,1)) = 0.495
        _DiffuseWrap ("Diffuse Wrap (1 = fur look, 0 = match Lit)", Range(0,1)) = 1
        _ShadowOpacity ("Fur Shadow Opacity", Range(0,1)) = 0.45

        _CardStrands ("Strands per Card", Range(1,40)) = 12
        _Thinness ("Tip Thinness", Range(0,1)) = 0.6
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.6
        _AlphaDither ("Alpha Dither Strength", Range(0,1)) = 0.5

        _ColorVar ("Color Variation", Range(0,1)) = 0.25

        _FurControlMap ("Control Map (R:len, G:mask, B:comb)", 2D) = "white" {}
        _CombStrength ("Card Comb Response", Range(0,5)) = 2.19
        _CombSurfaceAdherence ("Card Lay Flatness", Range(0,1)) = 1.0
        _CombTipBend ("Card Bend", Range(0,1)) = 0.5
        _CombJitter ("Micro Flow Noise", Range(0,0.35)) = 0.0
        _Gravity ("Gravity", Vector) = (0,-1,0,0)
        _GravityStrength ("Gravity Strength", Range(0,1)) = 0.0
        _MaxLength ("Max Length", Float) = 0.1
        _LengthScale ("Length Scale", Float) = 1
        _AlbedoProjection ("Albedo Projection", Float) = 1
        _HasControlMap ("Has Control Map", Float) = 0
        _FurWidthMap("Width Map", 2D) = "white" {}
        _HasWidthMap("Has Width Map", Float) = 0
        [Toggle] _CardBillboard ("Billboard To Camera", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CardVert
            #pragma fragment CardFrag

            #pragma shader_feature_local _ALPHATEX
            #pragma shader_feature_local _ALPHALUM
            #pragma shader_feature_local _SMOOTHNESSMAP
            #pragma shader_feature_local _ROUGHNESS_INPUT
            // Fragment-scoped shadow keywords + no vertex-light variant (no such path in this
            // shader) — same variant diet as the shell shader.
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #define FURBRUSH_FORWARD_LIGHTING
            #include "FurCard.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            Cull Off
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma shader_feature_local _ALPHATEX
            #pragma shader_feature_local _ALPHALUM
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "FurCard.hlsl"
#ifndef FURBRUSH_HDRP_COMPAT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#endif

            float3 _LightDirection;
            float3 _LightPosition;

            CardDepthVaryings ShadowVert(CardAttributes IN)
            {
                CardDepthVaryings o = (CardDepthVaryings)0;
                float3 posOS = CardScaleAndApplyCombAndGravity(IN.positionOS.xyz, IN.normalOS, IN.tangentOS, IN.uv1, IN.uv, IN.controlUV.xy, IN.baseUV);
                float3 positionWS = TransformObjectToWorld(posOS);
                float3 normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirWS = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirWS));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.positionCS = positionCS;
                o.uv = IN.uv;
                o.normalWS = normalWS;
                o.controlUV = IN.controlUV;
                return o;
            }

            half4 ShadowFrag(CardDepthVaryings IN) : SV_Target { CardClip(IN.uv, IN.controlUV, IN.positionCS); CardClipShadowOpacity(IN.positionCS); return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            Cull Off
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma shader_feature_local _ALPHATEX
            #pragma shader_feature_local _ALPHALUM

            #include "FurCard.hlsl"

            CardDepthVaryings DepthVert(CardAttributes IN)
            {
                CardDepthVaryings o = (CardDepthVaryings)0;
                float3 posOS = CardScaleAndApplyCombAndGravity(IN.positionOS.xyz, IN.normalOS, IN.tangentOS, IN.uv1, IN.uv, IN.controlUV.xy, IN.baseUV);
                float3 positionWS = TransformObjectToWorld(posOS);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.uv = IN.uv;
                o.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                o.controlUV = IN.controlUV;
                return o;
            }

            half4 DepthFrag(CardDepthVaryings IN) : SV_Target { CardClip(IN.uv, IN.controlUV, IN.positionCS); return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #pragma shader_feature_local _ALPHATEX
            #pragma shader_feature_local _ALPHALUM

            #include "FurCard.hlsl"

            CardDepthVaryings DepthNormalsVert(CardAttributes IN)
            {
                CardDepthVaryings o = (CardDepthVaryings)0;
                float3 posOS = CardScaleAndApplyCombAndGravity(IN.positionOS.xyz, IN.normalOS, IN.tangentOS, IN.uv1, IN.uv, IN.controlUV.xy, IN.baseUV);
                float3 positionWS = TransformObjectToWorld(posOS);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.uv = IN.uv;
                o.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                o.controlUV = IN.controlUV;
                return o;
            }

            half4 DepthNormalsFrag(CardDepthVaryings IN) : SV_Target
            {
                CardClip(IN.uv, IN.controlUV, IN.positionCS);
                return half4(normalize(IN.normalWS), 0.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
