// Redemoinho do portal no formato da abertura do arco:
// retângulo embaixo (até o chão) e meia-lua em cima, com a espiral girando
// dentro. "_Abertura" faz a luz crescer do centro (abrindo) ou ser sugada
// para o centro (fechando), sem mudar o tamanho do arco.
Shader "Pinglu/Portal"
{
    Properties
    {
        [HDR] _Cor ("Cor", Color) = (0.55, 0.45, 1, 1)
        _Intensidade ("Intensidade", Float) = 1.3
        _Velocidade ("Velocidade do giro", Float) = 0.35
        _Abertura ("Abertura (1 aberto, 0 fechado)", Range(0, 1)) = 1
        _Arco ("Altura onde começa a meia-lua (0-1)", Range(0.1, 0.95)) = 0.6
        _Aspecto ("Altura / largura da abertura", Float) = 1.6
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            float4 _Cor;
            float _Intensidade, _Velocidade, _Abertura, _Arco, _Aspecto;

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float x = (uv.x - 0.5) * 2.0;
                float yy = (uv.y - _Arco) / max(1.0 - _Arco, 0.01);
                // distância até a borda do arco (1 = meio, 0 = borda, negativo = fora)
                float dist = uv.y < _Arco ? 1.0 - abs(x) : 1.0 - length(float2(x, yy));
                dist = min(dist, uv.y * 8.0);
                if (dist <= 0.0) discard;
                float suave = smoothstep(0.0, 0.14, dist);           // borda macia, se fundindo na pedra

                // posição em relação ao centro da abertura
                float2 p = float2(x * 0.5, (uv.y - 0.5) * _Aspecto * 0.5);
                float r = length(p) + 0.0001;
                float a = atan2(p.y, p.x);
                float rMax = 0.5 * max(1.0, _Aspecto);              // até o canto mais longe
                float rn = r / rMax;

                // abrindo/fechando: a luz ocupa só até "frente" (o resto é sugado para o centro)
                float frente = _Abertura * 1.05;
                float visivel = 1.0 - smoothstep(frente - 0.06, frente, rn);
                float anel = exp(-pow((rn - frente) / 0.035, 2.0)) * (1.0 - _Abertura) * 3.0;

                float t = _Time.y * _Velocidade;
                float torcao = a + log(r + 0.05) * 2.6 + t * 1.6;
                float bracos = pow(saturate(cos(torcao * 4.0)), 3.0);
                float finos = pow(saturate(cos(torcao * 11.0 + 1.3)), 6.0) * 0.5;
                float nucleo = exp(-pow(r / 0.12, 2.0));
                float borda = saturate(1.0 - dist / 0.12);

                float brilho = ((bracos * 0.8 + finos) * saturate(1.15 - rn) + nucleo * 1.1 + borda * borda * 0.5 + 0.10) * visivel + anel;
                // brilho que satura devagar: mantém o violeta e só o miolo fica branco
                float e = 1.0 - exp(-brilho * _Intensidade * 0.9);
                float3 cor = lerp(_Cor.rgb, float3(1, 1, 1), e * e * 0.45) * e * 1.6 * suave;
                return fixed4(cor, 1.0);
            }
            ENDCG
        }
    }
}
