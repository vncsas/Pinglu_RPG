// Água "viva" do lago do Pântano (URP): igual à água do colega (cor por profundidade, reflexo, espuma),
// mais: ondinhas na altura da superfície, visível dos dois lados (de baixo, quando se mergulha, vira um teto verde),
// e ONDAS que nascem onde alguém pisa/nada (o script AmbienteDaAgua manda até 8 ondas por vez em _PingluOndas).
Shader "Pinglu/AguaViva"
{
    Properties
    {
        _ShallowColor ("Cor rasa", Color) = (0.16, 0.30, 0.22, 0.55)
        _DeepColor ("Cor funda", Color) = (0.02, 0.07, 0.06, 0.95)
        _DepthDistance ("Distancia da profundidade", Float) = 1.6
        _NormalMap ("Normal das ondas", 2D) = "bump" {}
        _NormalScale ("Tamanho das ondas", Float) = 0.12
        _WaveSpeed ("Velocidade", Float) = 0.12
        _Smoothness ("Brilho", Range(8, 512)) = 160
        _FoamColor ("Cor da espuma", Color) = (0.75, 0.82, 0.72, 1)
        _FoamWidth ("Largura da espuma", Float) = 0.22
        _Ondulacao ("Altura das ondinhas", Float) = 0.07
        _UnderColor ("Cor vista de baixo", Color) = (0.06, 0.22, 0.17, 0.9)
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
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor, _DeepColor, _FoamColor, _UnderColor; float4 _NormalMap_ST;
                float _DepthDistance, _NormalScale, _WaveSpeed, _Smoothness, _FoamWidth, _Ondulacao;
            CBUFFER_END
            // ondas de quem entra na água: x,y = posição (x,z do mundo), z = instante em que nasceu, w = força
            float4 _PingluOndas[8];
            float _PingluOndasN;

            struct Attributes { float4 posOS : POSITION; };
            struct Varyings { float4 posCS : SV_POSITION; float3 posWS : TEXCOORD0; float4 scr : TEXCOORD1; float fog : TEXCOORD2; };

            Varyings vert(Attributes v)
            {
                Varyings o; o.posWS = TransformObjectToWorld(v.posOS.xyz);
                o.posWS.y += (sin(o.posWS.x*0.55 + _Time.y*1.1) * cos(o.posWS.z*0.47 + _Time.y*0.9) + sin((o.posWS.x+o.posWS.z)*0.9 + _Time.y*1.7)*0.5) * _Ondulacao;
                o.posCS = TransformWorldToHClip(o.posWS);
                o.scr = ComputeScreenPos(o.posCS); o.fog = ComputeFogFactor(o.posCS.z); return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.posWS.xz*_NormalScale;
                float t = _Time.y*_WaveSpeed;
                half3 n1 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv + float2(t, t*0.7)));
                half3 n2 = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv*1.7 - float2(t*0.8, -t)));
                float2 nxy = (n1.xy + n2.xy*0.6)*0.22;
                // ondas de quem pisa na água
                float anel = 0.0;
                for (int k = 0; k < 8; k++)
                {
                    float4 o = _PingluOndas[k];
                    float idade = _Time.y - o.z;
                    if (idade < 0.0 || idade > 3.2 || o.w <= 0.0) continue;
                    float2 d = i.posWS.xz - o.xy; float dist = length(d) + 0.0001;
                    float raio = idade * (1.6 + o.w * 0.9);
                    float f = exp(-pow((dist - raio) * (2.4 - min(o.w, 1.5)*0.5), 2.0)) * saturate(1.0 - idade/3.2) * saturate(o.w);
                    float f2 = exp(-pow((dist - raio*0.62) * 3.0, 2.0)) * saturate(1.0 - idade/2.6) * saturate(o.w) * 0.6;
                    nxy += (d/dist) * (f + f2) * 0.45; anel += f + f2*0.7;
                }
                float3 N = normalize(half3(nxy, 1.0)).xzy;
                float3 V = normalize(GetCameraPositionWS() - i.posWS);

                // visto de baixo (câmera dentro da água): um "teto" esverdeado com brilho onde a luz entra
                if (GetCameraPositionWS().y < i.posWS.y)
                {
                    Light Lb = GetMainLight();
                    float brilho = pow(saturate(dot(-N, Lb.direction)), 3.0) * 0.35;
                    half3 cb = _UnderColor.rgb + Lb.color*brilho*0.4 + anel*0.08;
                    return half4(MixFog(cb, i.fog), _UnderColor.a);
                }

                float2 suv = i.scr.xy/i.scr.w;
                float sceneZ = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                float surfZ = LinearEyeDepth(i.posCS.z, _ZBufferParams);
                float depth = max(sceneZ - surfZ, 0.0);
                float d01 = saturate(depth/_DepthDistance);
                half4 col = lerp(_ShallowColor, _DeepColor, d01);
                float fres = pow(1.0 - saturate(dot(V, N)), 4.0);
                Light L = GetMainLight();
                float3 H = normalize(L.direction + V);
                float spec = pow(saturate(dot(N, H)), _Smoothness) * 0.35;
                half3 refl = GlossyEnvironmentReflection(reflect(-V, N), 0.06, 1.0);
                half3 sky = refl*1.05 + SampleSH(N)*0.15;
                col.rgb = lerp(col.rgb, sky, saturate(0.06 + fres*0.45)) + L.color*spec;
                float foam = saturate(1.0 - depth/_FoamWidth) * saturate(0.6 + n1.x*2.0);
                foam = saturate(foam + anel*0.55);
                col.rgb = lerp(col.rgb, _FoamColor.rgb, foam*0.85);
                col.a = saturate(max(col.a, foam) + fres*0.2);
                col.rgb = MixFog(col.rgb, i.fog);
                return col;
            }
            ENDHLSL
        }
    }
}
