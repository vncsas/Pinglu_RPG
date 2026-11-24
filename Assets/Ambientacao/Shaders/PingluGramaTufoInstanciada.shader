// Grama para o Terrain (detalhe de malha com instancing) e para espalhar com GPU instancing.
// - gradiente base escura -> ponta clara (vertex color R = altura na lamina, G = sorteio por lamina)
// - manchas de cor em grande escala (ruido no mundo): verde vivo, verde escuro, amarelado seco
// - balanco de vento so nas pontas
// - dois lados, luz principal + ambiente + sombra recebida
Shader "Pinglu/GramaTufoInstanciada"
{
    Properties
    {
        _BaseColor ("Cor da base", Color) = (0.035, 0.11, 0.03, 1)
        _TipColor ("Cor da ponta", Color) = (0.30, 0.55, 0.14, 1)
        _DryColor ("Cor seca (manchas)", Color) = (0.52, 0.52, 0.20, 1)
        _DarkColor ("Cor escura (manchas)", Color) = (0.10, 0.28, 0.10, 1)
        _PatchScale ("Tamanho das manchas", Float) = 0.045
        _PatchStrength ("Forca das manchas", Range(0, 1)) = 0.45
        _BladeVariation ("Variacao por lamina", Range(0, 1)) = 0.35
        _WindStrength ("Vento", Float) = 0.03
        _WindSpeed ("Velocidade do vento", Float) = 1.4
        _Translucency ("Luz atravessando", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _TipColor, _DryColor, _DarkColor;
                float _PatchScale, _PatchStrength, _BladeVariation, _WindStrength, _WindSpeed, _Translucency;
            CBUFFER_END

            struct Attributes { float4 posOS : POSITION; float3 normalOS : NORMAL; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 posCS : SV_POSITION; float3 posWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float4 col : TEXCOORD2; float fog : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float hash21(float2 p) { p = frac(p*float2(123.34, 456.21)); p += dot(p, p+45.32); return frac(p.x*p.y); }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f*f*(3-2*f);
                return lerp(lerp(hash21(i), hash21(i+float2(1,0)), f.x), lerp(hash21(i+float2(0,1)), hash21(i+float2(1,1)), f.x), f.y);
            }

            Varyings vert(Attributes v)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v, o);
                float3 pw = TransformObjectToWorld(v.posOS.xyz);
                float h = v.color.r;                                  // 0 base -> 1 ponta
                float w = sin(_Time.y*_WindSpeed + pw.x*0.35 + pw.z*0.27) + 0.5*sin(_Time.y*_WindSpeed*2.3 + pw.x*0.9);
                pw.xz += float2(1.0, 0.6) * w * _WindStrength * h * h;
                o.posWS = pw; o.posCS = TransformWorldToHClip(pw);
                o.normalWS = normalize(lerp(float3(0,1,0), TransformObjectToWorldNormal(v.normalOS), 0.35));   // normal quase para cima: some o "V" escuro
                o.col = v.color; o.fog = ComputeFogFactor(o.posCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float2 wp = i.posWS.xz;
                float n1 = vnoise(wp*_PatchScale), n2 = vnoise(wp*_PatchScale*3.7+17.0);
                float patch = saturate(n1*0.75+n2*0.25);
                half3 baseC = _BaseColor.rgb, tipC = _TipColor.rgb;
                half3 tint = lerp(_DarkColor.rgb, _DryColor.rgb, smoothstep(0.55, 0.92, patch));
                tipC = lerp(tipC, tint, _PatchStrength*smoothstep(0.10, 0.9, abs(patch-0.5)*2.0+0.2));
                half3 c = lerp(baseC, tipC, saturate(i.col.r*1.15));
                c *= 1.0 + (i.col.g-0.5)*_BladeVariation;             // cada lamina um pouco diferente
                Light L = GetMainLight(TransformWorldToShadowCoord(i.posWS));
                float3 N = normalize(i.normalWS);
                float ndl = saturate(dot(N, L.direction))*0.65 + 0.35;
                float back = saturate(dot(-N, L.direction)) * _Translucency * i.col.r;
                half3 ambient = SampleSH(N);
                half3 lit = c*(L.color*ndl*L.shadowAttenuation + ambient) + c*L.color*back*L.shadowAttenuation;
                lit = MixFog(lit, i.fog);
                return half4(lit, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
