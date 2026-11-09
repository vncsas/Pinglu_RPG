// Capim e flores estilo "jogo de aventura": raiz escura, ponta clara, manchas de cor pelo campo,
// vento balançando mais na ponta, luz atravessando as folhas contra o sol. Funciona com instancing (detalhes do Terrain).
// Para flores: ligue "Usar cor do vértice" (a cor de cada flor vem da própria malha).
Shader "Pinglu/Capim"
{
    Properties
    {
        _CorBase ("Cor da raiz", Color) = (0.07, 0.26, 0.04, 1)
        _CorPonta ("Cor da ponta", Color) = (0.50, 0.78, 0.18, 1)
        _CorVariacao ("Cor das manchas", Color) = (0.72, 0.78, 0.16, 1)
        _EscalaManchas ("Tamanho das manchas (1/m)", Float) = 0.04
        _ForcaManchas ("Força das manchas", Range(0, 1)) = 0.55
        _UsarCorDoVertice ("Usar cor do vértice (flores)", Range(0, 1)) = 0
        _VentoForca ("Vento: força (m)", Float) = 0.14
        _VentoVelocidade ("Vento: velocidade", Float) = 1.5
        _VentoEscala ("Vento: tamanho das ondas (1/m)", Float) = 0.1
        _Translucidez ("Luz atravessando a folha", Range(0, 1)) = 0.5
        _Brilho ("Brilho geral", Range(0.5, 2)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _CorBase;
                float4 _CorPonta;
                float4 _CorVariacao;
                float _EscalaManchas;
                float _ForcaManchas;
                float _UsarCorDoVertice;
                float _VentoForca;
                float _VentoVelocidade;
                float _VentoEscala;
                float _Translucidez;
                float _Brilho;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 color : TEXCOORD3;
                float fogFactor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float VNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                // vento: ondas largas e lentas + tremor rápido; só a parte de cima se mexe (uv.y = altura da folha, 0..1)
                float h = saturate(IN.uv.y);
                h = h * h;
                float2 wp = posWS.xz * _VentoEscala;
                float t = _Time.y * _VentoVelocidade;
                float onda = sin(wp.x * 6.2831 + wp.y * 3.7 + t) * 0.65 + sin(wp.x * 13.0 - wp.y * 9.0 + t * 1.9) * 0.35;
                float rajada = VNoise(posWS.xz * 0.03 + t * 0.12);
                float forca = _VentoForca * (0.6 + rajada);
                posWS.x += onda * forca * h;
                posWS.z += onda * forca * 0.55 * h;
                posWS.y -= abs(onda) * forca * 0.25 * h;

                OUT.positionWS = posWS;
                OUT.positionCS = TransformWorldToHClip(posWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                float altura = saturate(IN.uv.y);

                // cor: raiz escura -> ponta clara, com manchas de outra cor espalhadas pelo campo
                float3 grad = lerp(_CorBase.rgb, _CorPonta.rgb, saturate(altura * 1.15));
                float mancha = VNoise(IN.positionWS.xz * _EscalaManchas) * 0.65 + VNoise(IN.positionWS.xz * _EscalaManchas * 3.7 + 11.0) * 0.35;
                mancha = smoothstep(0.35, 0.75, mancha) * _ForcaManchas;
                float3 comManchas = lerp(grad, _CorVariacao.rgb * (0.55 + 0.45 * altura), mancha);
                float3 corGrama = comManchas * (0.82 + 0.18 * IN.color.r);
                float3 corFlor = IN.color.rgb * (0.7 + 0.3 * saturate(altura + 0.3));
                float3 albedo = lerp(corGrama, corFlor, _UsarCorDoVertice) * _Brilho;

                // luz: sol (meia-lambert, com sombra), ambiente e luz atravessando a folha quando o sol está atrás
                float3 n = normalize(lerp(float3(0.0, 1.0, 0.0), normalize(IN.normalWS), 0.25));
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light luz = GetMainLight(shadowCoord);
                float ndl = saturate(dot(n, luz.direction)) * 0.5 + 0.5;
                float3 ambiente = SampleSH(n);
                float3 cor = albedo * (luz.color * ndl * luz.shadowAttenuation + ambiente);
                float3 V = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float atras = pow(saturate(dot(-luz.direction, V)), 3.0) * altura * _Translucidez;
                cor += albedo * luz.color * atras * luz.shadowAttenuation * 1.6;

                cor = MixFog(cor, IN.fogFactor);
                return half4(cor, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
