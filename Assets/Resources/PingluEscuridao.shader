// Escuridão que entra na tela (tela de morte): fumaça escura nascendo das bordas e se fechando até o centro.
// Tudo feito por ruído no shader (nada de imagem): _Progresso 0 = tela limpa, 1 = quase tudo escuro (o centro ainda respira).
Shader "Pinglu/Escuridao"
{
    Properties
    {
        _Progresso ("Progresso", Range(0,1)) = 0
        _Cor ("Cor da escuridão", Color) = (0.008, 0.004, 0.016, 1)
        _Aspecto ("Aspecto da tela", Float) = 1.78
        _Tempo ("Tempo", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 v : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            float _Progresso, _Aspecto, _Tempo;
            fixed4 _Cor;

            v2f vert(appdata i) { v2f o; o.pos = UnityObjectToClipPos(i.v); o.uv = i.uv; return o; }

            float hash(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f * f * (3.0 - 2.0 * f);
                float a = hash(i), b = hash(i + float2(1, 0)), c = hash(i + float2(0, 1)), d = hash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }
            float fbm(float2 p)
            {
                float v = 0.0, a = 0.5;
                for (int k = 0; k < 5; k++) { v += a * noise(p); p = p * 2.03 + float2(17.3, 9.1); a *= 0.5; }
                return v;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * float2(_Aspecto, 1.0);
                float d = length(p) / (0.5 * sqrt(_Aspecto * _Aspecto + 1.0));     // 0 no centro, 1 nos cantos
                float t = _Tempo * 0.12;
                float2 q = p * 2.2;
                float2 w = float2(fbm(q + t), fbm(q + float2(5.2, 1.3) - t));       // distorção: a fumaça se enrola
                float n = fbm(q * 1.4 + w * 1.6 + float2(t * 0.6, -t * 0.4));
                float limiar = lerp(1.4, -0.5, _Progresso);
                float v = d + (n - 0.5) * 0.95;
                float a = smoothstep(limiar - 0.2, limiar + 0.14, v);               // a escuridão avança das bordas para o meio
                float fino = fbm(q * 5.0 + w * 2.0 - t * 2.0);
                a *= lerp(0.72, 1.0, fino);                                         // fiapos
                a = saturate(a) * 0.97;
                float borda = smoothstep(0.0, 0.3, a) * (1.0 - smoothstep(0.3, 0.85, a));
                float3 cor = _Cor.rgb + float3(0.045, 0.035, 0.085) * borda;        // a frente da fumaça é um pouco mais clara e fria
                return fixed4(cor, a);
            }
            ENDCG
        }
    }
}
