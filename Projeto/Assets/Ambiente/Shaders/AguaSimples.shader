// Agua simples para pantano/lago (URP): cor por profundidade (usa o depth texture), ondas por duas normais que correm, fresnel,
// brilho do sol e espuma na margem. Ative "Depth Texture" no URP asset (o script Montar faz isso).
Shader "Pinglu/AguaSimples"
{
    Properties
    {
        _ShallowColor ("Cor rasa", Color) = (0.16, 0.30, 0.22, 0.55)
        _DeepColor ("Cor funda", Color) = (0.02, 0.07, 0.06, 0.95)
        _DepthDistance ("Distancia da profundidade", Float) = 1.6
        _NormalMap ("Normal das ondas", 2D) = "bump" {}
        _NormalScale ("Tamanho das ondas", Float) = 0.12
        _WaveSpeed ("Velocidade", Float) = 0.04
        _Smoothness ("Brilho", Range(8, 512)) = 160
        _FoamColor ("Cor da espuma", Color) = (0.75, 0.82, 0.72, 1)
        _FoamWidth ("Largura da espuma", Float) = 0.22
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor, _DeepColor, _FoamColor; float4 _NormalMap_ST;
                float _DepthDistance, _NormalScale, _WaveSpeed, _Smoothness, _FoamWidth;
            CBUFFER_END

            struct Attributes { float4 posOS : POSITION; };
            struct Varyings { float4 posCS : SV_POSITION; float3 posWS : TEXCOORD0; float4 scr : TEXCOORD1; float fog : TEXCOORD2; };

            Varyings vert(Attributes v)
            {
                Varyings o; o.posWS = TransformObjectToWorld(v.posOS.xyz); o.posCS = TransformWorldToHClip(o.posWS);
                o.scr = ComputeScreenPos(o.posCS); o.fog = ComputeFogFactor(o.posCS.z); return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.posWS.xz*_NormalScale;
                float t = _Time.y*_WaveSpeed;
                half3 n1 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv + float2(t, t*0.7)));
                half3 n2 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv*1.7 - float2(t*0.8, -t)));
                float3 N = normalize(half3((n1.xy + n2.xy*0.6)*0.22, 1.0)).xzy;       // normal tangente -> mundo (plano horizontal)
                float2 suv = i.scr.xy/i.scr.w;
                float sceneZ = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                float surfZ = LinearEyeDepth(i.posCS.z, _ZBufferParams);
                float depth = max(sceneZ - surfZ, 0.0);
                float d01 = saturate(depth/_DepthDistance);
                half4 col = lerp(_ShallowColor, _DeepColor, d01);
                float3 V = normalize(GetCameraPositionWS() - i.posWS);
                float fres = pow(1.0 - saturate(dot(V, N)), 4.0);
                Light L = GetMainLight();
                float3 H = normalize(L.direction + V);
                float spec = pow(saturate(dot(N, H)), _Smoothness) * 0.35;
                half3 refl = GlossyEnvironmentReflection(reflect(-V, N), 0.06, 1.0);
                half3 sky = refl*1.05 + SampleSH(N)*0.15;
                col.rgb = lerp(col.rgb, sky, saturate(0.06 + fres*0.45)) + L.color*spec;
                float foam = saturate(1.0 - depth/_FoamWidth) * saturate(0.6 + n1.x*2.0);
                col.rgb = lerp(col.rgb, _FoamColor.rgb, foam*0.85);
                col.a = saturate(max(col.a, foam) + fres*0.2);
                col.rgb = MixFog(col.rgb, i.fog);
                return col;
            }
            ENDHLSL
        }
    }
}
