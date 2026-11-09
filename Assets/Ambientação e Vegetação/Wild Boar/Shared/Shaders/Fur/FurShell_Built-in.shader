// FurBrush Developer - export template for Shell fur.
//
// The exporter copies this file, renamed, into a published model's Shaders/ folder, where the fur
// has to render with no FurBrush installed. It differs from FurShell_URP/_HDRP/_BIRP in three
// ways and in nothing else:
//
//  * One shader serves all three pipelines. The URP and HDRP SubShaders open with a
//    PackageRequirements block, so Unity only compiles them in a project that has that package and
//    never reaches their pipeline #includes anywhere else; the Built-in SubShader needs no package.
//    That stands in for FurPipelineDefines.hlsl, which the plugin keeps current with an editor
//    script a published model does not carry, so every pass names its pipeline itself.
//  * The vertex input is FurExportAttributes (FurExportLayout.hlsl), the FBX-safe layout the
//    exporter writes, unpacked into the plugin's FurAttributes before any shading code runs.
//  * On export the ../../Runtime/Shaders/ include prefix is dropped: the shading code travels in
//    the same folder as this file.
Shader "Wild Boar/Built-in/Fur/Shell"
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
        [HideInInspector] _RegionalGroomEnable ("Regional Groom", Float) = 0
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
        _ShadowOpacity ("Fur Cast Shadow Opacity", Range(0,1)) = 0.45

        _Gravity ("Gravity (local)", Vector) = (0,-1,0,0)
        _GravityStrength ("Gravity Strength", Range(0,1)) = 0.3
        [HideInInspector] _DynamicMotion ("Dynamic Motion (runtime)", Vector) = (0,0,0,0)
        [HideInInspector] _Wind ("Wind (runtime)", Vector) = (0,0,0,0)
        _CombStrength ("Comb Bend Strength", Range(0,5)) = 1.2
        _CombSurfaceAdherence ("Comb Surface Adherence", Range(0,1)) = 0.65
        _CombJitter ("Micro Flow Noise", Range(0,0.35)) = 0.0
        _CombUprightness ("Combing Uprightness", Range(0,1)) = 0.3
        // Live quality control: the mesh carries every baked layer and the shader draws this
        // many of them, evenly spaced. The exporter narrows the range to what the mesh carries.
        [HideInInspector] _ShellCount ("Baked layers (mesh metadata)", Float) = 16
        // Written per renderer by Fur Shell Layers when the model carries one copy of its coat.
        [HideInInspector] _ShellLayer ("Shell Layer (runtime)", Float) = 0
        [HideInInspector] _ShellLayers ("Shell Layer Count (runtime)", Float) = 0
        _ParallaxScale ("View Projection", Range(0,8)) = 0.5
        _HasControlMap ("Has Control Map", Float) = 0

        _FurWidthMap("Width Map", 2D) = "white" {}
        _HasWidthMap("Has Width Map", Float) = 0
    }

    // ======================================================================
    // HDRP
    // ======================================================================
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        LOD 300

        Pass
        {
            Name "ForwardBase"
            Tags { "LightMode"="ForwardBase" }
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FurExportVertForward
            #pragma fragment FurFragForward

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _SMOOTHNESSMAP
            #pragma shader_feature_local _ROUGHNESS_INPUT
            #pragma shader_feature_local _CONTROL_UV1

            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #pragma skip_variants LIGHTMAP_ON DIRLIGHTMAP_COMBINED DYNAMICLIGHTMAP_ON LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK VERTEXLIGHT_ON

            #define FURBRUSH_BIRP_COMPAT
            #define FURBRUSH_FORWARD_LIGHTING
            // The layer a draw stands for is a per-renderer value only a published model has.
            #define FURBRUSH_EXPORT_SHELL_LAYERS
            #include "FurShell.hlsl"
            #include "FurExportLayout.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ForwardAdd"
            Tags { "LightMode"="ForwardAdd" }
            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend One One

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FurExportVertForward
            #pragma fragment FurFragForward

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _SMOOTHNESSMAP
            #pragma shader_feature_local _ROUGHNESS_INPUT
            #pragma shader_feature_local _CONTROL_UV1

            #pragma multi_compile_fwdadd_fullshadows
            #pragma multi_compile_fog

            #define FURBRUSH_BIRP_COMPAT
            #define FURBRUSH_FORWARD_LIGHTING
            // The layer a draw stands for is a per-renderer value only a published model has.
            #define FURBRUSH_EXPORT_SHELL_LAYERS
            #include "FurShell.hlsl"
            #include "FurExportLayout.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FurExportShadowVert
            #pragma fragment FurShadowFrag
            #pragma multi_compile_shadowcaster
            #pragma shader_feature_local _CONTROL_UV1

            #define FURBRUSH_BIRP_COMPAT
            // The layer a draw stands for is a per-renderer value only a published model has.
            #define FURBRUSH_EXPORT_SHELL_LAYERS
            #include "FurShell.hlsl"
            #include "FurExportLayout.hlsl"

            struct FurShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 controlUV  : TEXCOORD0;
                float2 strandUV   : TEXCOORD1;
                float  t          : TEXCOORD2;
                float3 lightVec   : TEXCOORD3;
            };

            FurShadowVaryings FurExportShadowVert(FurExportAttributes v)
            {
                FurAttributes IN = FurExportUnpack(v);
                FurShadowVaryings o = (FurShadowVaryings)0;
                float3 posOS, nOS; float2 cuv, auv; float t; FurControl ctrl; float3 strandDirWS;
                FurExtrude(IN, posOS, nOS, cuv, auv, t, ctrl, strandDirWS);

                float3 positionWS = TransformObjectToWorld(posOS);
                float3 normalWS   = TransformObjectToWorldNormal(nOS);

                o.positionCS = FurBuiltInShadowCasterClipPos(positionWS, normalWS);
                o.controlUV  = cuv;
                o.strandUV   = auv;
                o.t          = t;
                o.lightVec   = positionWS - _LightPositionRange.xyz;
                return o;
            }

            float4 FurShadowFrag(FurShadowVaryings IN) : SV_Target
            {
                FurControl ctrl = DecodeFurControl(SAMPLE_TEXTURE2D(_FurControlMap, sampler_FurControlMap, IN.controlUV), _HasControlMap);
                float widthLoc = 1.0;
                if (_HasWidthMap > 0.5)
                {
                    widthLoc = SAMPLE_TEXTURE2D(_FurWidthMap, sampler_FurWidthMap, IN.controlUV).r;
                }
                FurClip(IN.strandUV, ctrl.mask, IN.t, IN.positionCS, widthLoc, ctrl.length);
                FurClipShadowOpacity(IN.strandUV, IN.t);

                #if defined(SHADOWS_CUBE) && !defined(SHADOWS_CUBE_IN_DEPTH_TEX)
                    return UnityEncodeCubeShadowDepth((length(IN.lightVec) + unity_LightShadowBias.x) * _LightPositionRange.w);
                #else
                    return 0;
                #endif
            }
            ENDHLSL
        }
    }

    FallBack Off
    CustomEditor "WildBoarDemo.Editor.FurShaderGUI"
}


