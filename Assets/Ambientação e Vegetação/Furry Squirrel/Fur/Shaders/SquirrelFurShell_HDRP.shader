Shader "Squirrel/Fur Shell HDRP"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base / Albedo (harvested)", 2D) = "white" {}
        [MainColor]   _BaseColor ("Base Color", Color) = (1,1,1,1)
        [Toggle(_SMOOTHNESSMAP)] _UseSmoothnessMap ("Use Smoothness Map", Float) = 0
        _SmoothnessMap ("Smoothness / Roughness", 2D) = "white" {}
        _Smoothness ("Smoothness", Range(0,1)) = 0.35
        [Toggle(_ROUGHNESS_INPUT)] _RoughnessInput ("Map is Roughness", Float) = 0
        _SmoothnessChannel ("Smoothness Channel (0R 1G 2B 3A)", Float) = 3
        [Toggle(_NORMALMAP)] _UseNormalMap ("Use Normal Map", Float) = 0
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}

        [NoScaleOffset] _FurControlMap ("Control Map (R=len G=mask B,A=comb)", 2D) = "black" {}

        _MaxLength ("Max Length", Float) = 0.05
        _LengthScale ("Length Scale", Range(0,3)) = 0.2
        _Density ("Strand Density", Range(1,2048)) = 450
        _Thinness ("Tip Thinness", Range(0,1)) = 0.65
        _LengthJitter ("Length Jitter", Range(0,1)) = 0.35
        _Clumping ("Clumping", Range(0,4)) = 1
        _DirRandom ("Direction Randomness", Range(0,1)) = 0.3
        _ColorVar ("Color Variation", Range(0,1)) = 0.25

        _RootColor ("Root Shade", Color) = (0.5,0.5,0.5,1)
        _TipColor ("Tip Shade", Color) = (1,1,1,1)
        _AlbedoInfluence ("Albedo Influence", Range(0,1)) = 1
        _RootAO ("Root AO", Range(0,2)) = 0.5

        _AnisoSpecular ("Aniso Specular", Range(0,2)) = 0.6
        _Rim ("Rim / Transmission", Range(0,1)) = 0.4
        _DiffuseWrap ("Diffuse Wrap (1 = fur look, 0 = match Lit)", Range(0,1)) = 1
        _ShadowOpacity ("Fur Shadow Opacity", Range(0,1)) = 0.45

        _Gravity ("Gravity (local)", Vector) = (0,-1,0,0)
        _GravityStrength ("Gravity Strength", Range(0,1)) = 0.3
        _CombStrength ("Comb Bend Strength", Range(0,5)) = 1.2
        _CombSurfaceAdherence ("Comb Surface Adherence", Range(0,1)) = 0.65
        _CombJitter ("Micro Flow Noise", Range(0,0.35)) = 0.0
        _CombUprightness ("Combing Uprightness", Range(0,1)) = 0.3

        _ShellCount ("Shell Count (info)", Float) = 28
        _ParallaxScale ("View Projection", Range(0,8)) = 0.5
        _SilhouetteStretch ("Silhouette Stretch", Range(0, 20)) = 1
        _SilhouetteTilt ("Silhouette Tilt", Range(-3, 3)) = 0
        _EnableSilhouetteCorrection ("Enable Silhouette Correction", Float) = 1
        _HasControlMap ("Has Control Map", Float) = 0

        _FurWidthMap("Width Map", 2D) = "white" {}
        _HasWidthMap("Has Width Map", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="HDRenderPipeline" }
        LOD 300

        // ------------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="ForwardOnly" }
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FurVertForward
            #pragma fragment FurFragForward

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _SMOOTHNESSMAP
            #pragma shader_feature_local _ROUGHNESS_INPUT

            // Variant diet: shadow coords are computed in the fragment stage only, so the shadow
            // keywords are fragment-scoped; _ADDITIONAL_LIGHTS_VERTEX and lightmap/mixed-lighting
            // keywords are dropped entirely — this shader has no vertex-light or lightmap path
            // (the fur overlay child can never be lightmapped), so those variants were dead weight.
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #define FURBRUSH_FORWARD_LIGHTING
            #pragma shader_feature_local _CONTROL_UV1
            #define FURBRUSH_HDRP_COMPAT
            #include "FurShell.hlsl"
            ENDHLSL
        }

        // ------------------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            Cull Back
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FurShadowVert
            #pragma fragment FurShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #pragma shader_feature_local _CONTROL_UV1
            #define FURBRUSH_HDRP_COMPAT
            #include "FurShell.hlsl"
#ifndef FURBRUSH_HDRP_COMPAT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#endif

            float3 _LightDirection;
            float3 _LightPosition;

            FurDepthVaryings FurShadowVert(FurAttributes IN)
            {
                FurDepthVaryings o = (FurDepthVaryings)0;
                float3 posOS, nOS; float2 cuv, auv; float t; FurControl ctrl;
                FurExtrude(IN, posOS, nOS, cuv, auv, t, ctrl, o.strandDirWS);

                float3 positionWS = TransformObjectToWorld(posOS);
                float3 normalWS   = TransformObjectToWorldNormal(nOS);

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
                o.controlUV = cuv; o.strandUV = auv; o.t = t; o.normalWS = normalWS;
                o.positionWS = positionWS;
                o.tangentWS = float4(TransformObjectToWorldDir(IN.tangentOS.xyz), IN.tangentOS.w);
                return o;
            }

            half4 FurShadowFrag(FurDepthVaryings IN) : SV_Target
            {
                FurControl ctrl = DecodeFurControl(SAMPLE_TEXTURE2D(_FurControlMap, sampler_FurControlMap, IN.controlUV), _HasControlMap);
                float widthLoc = 1.0;
                if (_HasWidthMap > 0.5)
                {
                    widthLoc = SAMPLE_TEXTURE2D(_FurWidthMap, sampler_FurWidthMap, IN.controlUV).r;
                }
                // Plain round-footprint clip: the silhouette stretch is derived from the CAMERA
                // view, which is meaningless (and camera-dependent -> shimmer) in a light-space
                // shadow render — and skipping it makes the shadow pass much cheaper per shell.
                FurClip(IN.strandUV, ctrl.mask, IN.t, IN.positionCS, widthLoc, ctrl.length);
                FurClipShadowOpacity(IN.positionCS);
                return 0;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        Pass
        {
            Name "DepthForwardOnly"
            Tags { "LightMode"="DepthForwardOnly" }
            Cull Back
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FurDepthVert
            #pragma fragment FurDepthFrag

            #pragma shader_feature_local _CONTROL_UV1
            #define FURBRUSH_HDRP_COMPAT
            #include "FurShell.hlsl"

             FurDepthVaryings FurDepthVert(FurAttributes IN)
             {
                 FurDepthVaryings o = (FurDepthVaryings)0;
                 float3 posOS, nOS; float2 cuv, auv; float t; FurControl ctrl;
                 FurExtrude(IN, posOS, nOS, cuv, auv, t, ctrl, o.strandDirWS);
                 VertexPositionInputs vpi = GetVertexPositionInputs(posOS);
                 o.positionCS = vpi.positionCS;
                 o.controlUV = cuv; o.strandUV = auv; o.t = t;
                 o.normalWS = TransformObjectToWorldNormal(nOS);
                 o.positionWS = vpi.positionWS;
                 o.tangentWS = float4(TransformObjectToWorldDir(IN.tangentOS.xyz), IN.tangentOS.w);
                 return o;
             }

             half4 FurDepthFrag(FurDepthVaryings IN) : SV_Target
             {
                 FurControl ctrl = DecodeFurControl(SAMPLE_TEXTURE2D(_FurControlMap, sampler_FurControlMap, IN.controlUV), _HasControlMap);
                 float widthLoc = 1.0;
                 if (_HasWidthMap > 0.5)
                 {
                     widthLoc = SAMPLE_TEXTURE2D(_FurWidthMap, sampler_FurWidthMap, IN.controlUV).r;
                 }
                 // Exact seed coverage used by ForwardLit. The old projected one-sample clip could
                 // write depth where the color pass had no matching fragment, exposing sky or
                 // other models through a phantom shell layer.
                 FurClip(IN.strandUV, ctrl.mask, IN.t, IN.positionCS, widthLoc, ctrl.length);
                 return 0;
             }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        // Editor selection outline mask. Without this pass the outline system builds its mask
        // from a DITHERED pass — the stipple holes read as millions of tiny mask edges and the
        // whole coat speckles blue/orange while selected. Same geometry as DepthOnly, but the
        // clip uses the plain coverage threshold with NO noise, so the mask (and the outline)
        // follows the clean fur silhouette.
        Pass
        {
            Name "SceneSelectionPass"
            Tags { "LightMode"="SceneSelectionPass" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FurSelectionVert
            #pragma fragment FurSelectionFrag
            #pragma editor_sync_compilation

            #pragma shader_feature_local _CONTROL_UV1
            #define FURBRUSH_HDRP_COMPAT
            #include "FurShell.hlsl"

            int _ObjectId;
            int _PassValue;

            FurDepthVaryings FurSelectionVert(FurAttributes IN)
            {
                FurDepthVaryings o = (FurDepthVaryings)0;
                float3 posOS, nOS; float2 cuv, auv; float t; FurControl ctrl;
                FurExtrude(IN, posOS, nOS, cuv, auv, t, ctrl, o.strandDirWS);
                VertexPositionInputs vpi = GetVertexPositionInputs(posOS);
                o.positionCS = vpi.positionCS;
                o.controlUV = cuv; o.strandUV = auv; o.t = t;
                o.normalWS = TransformObjectToWorldNormal(nOS);
                o.positionWS = vpi.positionWS;
                o.tangentWS = float4(TransformObjectToWorldDir(IN.tangentOS.xyz), IN.tangentOS.w);
                return o;
            }

            float4 FurSelectionFrag(FurDepthVaryings IN) : SV_Target
            {
                FurControl ctrl = DecodeFurControl(SAMPLE_TEXTURE2D(_FurControlMap, sampler_FurControlMap, IN.controlUV), _HasControlMap);
                float widthLoc = 1.0;
                if (_HasWidthMap > 0.5)
                {
                    widthLoc = SAMPLE_TEXTURE2D(_FurWidthMap, sampler_FurWidthMap, IN.controlUV).r;
                }
                float cov = FurCoverage(IN.strandUV, ctrl.mask, IN.t, float2(1.0, 0.0), 1.0, widthLoc, ctrl.length);
                clip(cov - FUR_CLIP_THRESHOLD);
                return float4(_ObjectId, _PassValue, 1.0, 1.0);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FurDepthNormalsVert
            #pragma fragment FurDepthNormalsFrag

            #pragma shader_feature_local _CONTROL_UV1
            #define FURBRUSH_HDRP_COMPAT
            #include "FurShell.hlsl"

             FurDepthVaryings FurDepthNormalsVert(FurAttributes IN)
             {
                 FurDepthVaryings o = (FurDepthVaryings)0;
                 float3 posOS, nOS; float2 cuv, auv; float t; FurControl ctrl;
                 FurExtrude(IN, posOS, nOS, cuv, auv, t, ctrl, o.strandDirWS);
                 VertexPositionInputs vpi = GetVertexPositionInputs(posOS);
                 o.positionCS = vpi.positionCS;
                 o.controlUV = cuv; o.strandUV = auv; o.t = t;
                 o.normalWS = TransformObjectToWorldNormal(nOS);
                 o.positionWS = vpi.positionWS;
                 o.tangentWS = float4(TransformObjectToWorldDir(IN.tangentOS.xyz), IN.tangentOS.w);
                 return o;
             }

             half4 FurDepthNormalsFrag(FurDepthVaryings IN) : SV_Target
             {
                 FurControl ctrl = DecodeFurControl(SAMPLE_TEXTURE2D(_FurControlMap, sampler_FurControlMap, IN.controlUV), _HasControlMap);
                 float widthLoc = 1.0;
                 if (_HasWidthMap > 0.5)
                 {
                     widthLoc = SAMPLE_TEXTURE2D(_FurWidthMap, sampler_FurWidthMap, IN.controlUV).r;
                 }
                 FurClip(IN.strandUV, ctrl.mask, IN.t, IN.positionCS, widthLoc, ctrl.length);
                 return half4(normalize(IN.normalWS), 0.0);
             }
            ENDHLSL
        }
    }

    FallBack Off
}
