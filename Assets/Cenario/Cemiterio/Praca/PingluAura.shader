// Brilho/fumaça para a aura do Guardião, raios e efeitos: textura * cor (HDR) * cor do vértice.
// _Dst = 1 (One)  -> soma luz (aditivo, brilha)   |   _Dst = 10 (OneMinusSrcAlpha) -> fumaça escura normal
Shader "Pinglu/Aura"
{
    Properties
    {
        _MainTex ("Textura", 2D) = "white" {}
        [HDR] _Cor ("Cor", Color) = (1, 1, 1, 1)
        _Src ("Src", Float) = 5
        _Dst ("Dst", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Aura"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_Src] [_Dst]
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Cor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : TEXCOORD1; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half4 c = t * _Cor * IN.color;
                return c;
            }
            ENDHLSL
        }
    }
}
