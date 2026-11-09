// Halo de luz de lanterna: um brilho macio que sempre olha para a câmera (soma luz, não esconde nada).
// Some de dia (pelo brilho do ambiente), de muito longe e de muito perto.
Shader "Pinglu/Brilho"
{
    Properties
    {
        _Cor ("Cor", Color) = (1, 0.7, 0.38, 1)
        _Tamanho ("Tamanho (m)", Float) = 1.6
        _Intensidade ("Intensidade", Float) = 1.2
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+10" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend One One
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
                float _Tamanho;
                float _Intensidade;
            CBUFFER_END
            struct A { float4 p : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 p : SV_POSITION; float2 uv : TEXCOORD0; float fade : TEXCOORD1; };
            V vert(A i)
            {
                V o;
                float3 centro = TransformObjectToWorld(float3(0, 0, 0));
                float3 direita = UNITY_MATRIX_V[0].xyz;
                float3 cima = UNITY_MATRIX_V[1].xyz;
                float3 pos = centro + (direita * i.p.x + cima * i.p.y) * _Tamanho;
                o.p = TransformWorldToHClip(pos);
                o.uv = i.uv;
                float dist = distance(_WorldSpaceCameraPos, centro);
                float f = saturate((dist - 1.2) / 3.0) * (1.0 - saturate((dist - 55.0) / 35.0));
                float amb = dot(SampleSH(float3(0, 1, 0)), float3(0.3, 0.59, 0.11));
                f *= saturate((0.85 - amb) * 3.0);        // de dia o ambiente é claro e o halo some
                o.fade = f;
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                float d = length(i.uv - 0.5) * 2.0;
                float a = pow(saturate(1.0 - d), 2.4);
                float centro = pow(saturate(1.0 - d * 2.2), 2.0);
                float3 c = _Cor.rgb * (a * 0.8 + centro * 0.9) * _Intensidade * i.fade;
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
