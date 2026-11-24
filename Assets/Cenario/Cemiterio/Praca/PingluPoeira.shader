// Poeira e brasas da cena do Guardião: bolinha macia que usa a cor/transparência de cada partícula.
// _Src/_Dst: 5/10 = poeira normal (mistura); 5/1 = brasa brilhante (soma).
Shader "Pinglu/Poeira"
{
    Properties
    {
        _Cor ("Cor", Color) = (1, 1, 1, 1)
        [HideInInspector] _Src ("Src", Float) = 5
        [HideInInspector] _Dst ("Dst", Float) = 10
        _Ambiente ("Escurece com o ambiente (0 a 1)", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend [_Src] [_Dst]
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Cor;
                float _Ambiente;
            CBUFFER_END
            struct A { float4 p : POSITION; float4 c : COLOR; float2 uv : TEXCOORD0; };
            struct V { float4 p : SV_POSITION; float4 c : COLOR; float2 uv : TEXCOORD0; float amb : TEXCOORD1; };
            V vert(A i)
            {
                V o;
                o.p = TransformObjectToHClip(i.p.xyz);
                o.c = i.c;
                o.uv = i.uv;
                float a = dot(SampleSH(float3(0, 1, 0)), float3(0.3, 0.59, 0.11));
                o.amb = lerp(1.0, lerp(0.22, 1.0, saturate(a * 1.6)), _Ambiente);
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                float d = length(i.uv - 0.5) * 2.0;
                float a = saturate(1.0 - d);
                a = a * a * (3.0 - 2.0 * a);
                float ruido = frac(sin(dot(floor(i.uv * 7.0), float2(12.9898, 78.233))) * 43758.5453);
                a *= lerp(0.8, 1.0, ruido);
                half3 cor = _Cor.rgb * i.c.rgb * i.amb;
                return half4(cor, a * i.c.a * _Cor.a);
            }
            ENDHLSL
        }
    }
}
