// Pele de pedra do Guardião: usa a cor guardada em cada vértice (sem textura, então não tem emendas)
// e mostra rachaduras brilhantes que vão se espalhando quando _Rachar sobe de 0 a 1 (ou mais).
Shader "Pinglu/Golem"
{
    Properties
    {
        _Brilho ("Brilho da cor", Range(0.5, 2.5)) = 1.3
        _Rachar ("Rachaduras (0 a 1.5)", Range(0, 1.5)) = 0
        [HDR] _CorRachadura ("Cor da rachadura", Color) = (3.0, 0.9, 0.2, 1)
        _EscalaRachadura ("Tamanho das pedras (1/m)", Float) = 1.5
        _Pulso ("Pulso da rachadura", Float) = 1
        _Furia ("Fúria (2ª fase)", Range(0, 1)) = 0
        [HDR] _CorFuria ("Cor da fúria", Color) = (3.0, 0.35, 0.12, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Brilho;
                float _Rachar;
                float4 _CorRachadura;
                float _EscalaRachadura;
                float _Pulso;
                float _Furia;
                float4 _CorFuria;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 color : COLOR; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 color : TEXCOORD2;
                float3 posOS : TEXCOORD3;
                float fogFactor : TEXCOORD4;
            };

            float3 Hash33(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return frac((p.xxy + p.yxx) * p.zyx);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.color = IN.color;
                OUT.posOS = IN.positionOS.xyz;
                OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 albedo = SRGBToLinear(IN.color.rgb) * _Brilho;
                float3 n = normalize(IN.normalWS);
                Light luz = GetMainLight(TransformWorldToShadowCoord(IN.positionWS));
                float ndl = saturate(dot(n, luz.direction) * 0.65 + 0.35);
                float3 cor = albedo * (luz.color * ndl * luz.shadowAttenuation + SampleSH(n));

                // rachaduras: bordas entre "pedras" de um ruído celular 3D; aparecem aos poucos, de baixo para cima
                if (_Rachar > 0.001)
                {
                    float3 p = IN.posOS * _EscalaRachadura;
                    float3 ip = floor(p), fp = frac(p);
                    float f1 = 8.0, f2 = 8.0; float3 idMaisPerto = float3(0, 0, 0);
                    for (int x = -1; x <= 1; x++)
                    for (int y = -1; y <= 1; y++)
                    for (int z = -1; z <= 1; z++)
                    {
                        float3 g = float3(x, y, z);
                        float3 o = Hash33(ip + g);
                        float d = length(g + o - fp);
                        if (d < f1) { f2 = f1; f1 = d; idMaisPerto = ip + g; }
                        else if (d < f2) f2 = d;
                    }
                    float borda = f2 - f1;
                    float linha = 1.0 - smoothstep(0.0, 0.085, borda);
                    float sorte = Hash33(idMaisPerto).x * 0.6 + saturate(1.0 - IN.posOS.y / 5.6) * 0.4;
                    float aparece = smoothstep(sorte - 0.05, sorte + 0.05, _Rachar);
                    float brilho = (0.75 + 0.25 * sin(_Time.y * 5.0 + sorte * 20.0 * _Pulso));
                    cor *= lerp(1.0, 0.55, linha * aparece);
                    cor += _CorRachadura.rgb * linha * aparece * brilho;
                }
                // fúria (2ª fase): contorno incandescente, como se o corpo queimasse por dentro
                if (_Furia > 0.001)
                {
                    float3 vd = normalize(GetWorldSpaceViewDir(IN.positionWS));
                    float rim = pow(1.0 - saturate(dot(n, vd)), 2.2);
                    float pulsoF = 0.75 + 0.25 * sin(_Time.y * 3.0);
                    cor += _CorFuria.rgb * rim * _Furia * pulsoF * 2.2;
                    cor *= lerp(1.0, float3(1.12, 0.82, 0.8), _Furia * 0.5);
                }
                cor = MixFog(cor, IN.fogFactor);
                return half4(cor, 1.0);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/SHADOWCASTER"
    }
}
