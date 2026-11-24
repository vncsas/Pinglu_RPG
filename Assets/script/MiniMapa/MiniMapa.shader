// Desenha o pedaço do mapa em volta do jogador: gira com a câmera,
// esconde o que ainda não foi explorado e corta em círculo.
Shader "Pinglu/MiniMapa"
{
    Properties
    {
        _MainTex ("Mapa", 2D) = "white" {}
        _Nevoa ("Nevoa (explorado = branco)", 2D) = "white" {}
        _CorNevoa ("Cor da nevoa", Color) = (0.16, 0.13, 0.10, 1)
        _Centro ("Centro (uv)", Vector) = (0.5, 0.5, 0, 0)
        _Zoom ("Zoom", Float) = 0.15
        _Rot ("Rotacao (rad)", Float) = 0
        _Circulo ("Recortar em circulo", Float) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        Cull Off ZWrite Off ZTest Always
        Blend Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _Nevoa;
            float4 _CorNevoa;
            float4 _Centro;
            float _Zoom;
            float _Rot;
            float _Circulo;

            fixed4 frag (v2f_img i) : SV_Target
            {
                float2 d = i.uv - 0.5;
                float c = cos(_Rot), s = sin(_Rot);
                float2 w = float2(d.x * c + d.y * s, -d.x * s + d.y * c);
                float2 uv = _Centro.xy + w * _Zoom;

                fixed4 cor = tex2D(_MainTex, uv);
                // fora do mapa: pergaminho escuro
                float fora = step(uv.x, 0) + step(1, uv.x) + step(uv.y, 0) + step(1, uv.y);
                float explorado = saturate(tex2D(_Nevoa, uv).r) * (1 - saturate(fora));
                // a névoa tem um leve "desenho" de manchas para não ficar chapada
                float mancha = frac(sin(dot(floor(uv * 180), float2(12.9898, 78.233))) * 43758.5453) * 0.04;
                fixed3 rgb = lerp(_CorNevoa.rgb + mancha, cor.rgb, explorado);

                float r = length(d);
                float alfa = 1;
                if (_Circulo > 0.5)
                {
                    alfa = 1 - smoothstep(0.485, 0.5, r);
                    rgb *= lerp(1, 0.55, smoothstep(0.30, 0.5, r));   // borda escurecida
                }
                else
                {
                    float2 q = abs(d);
                    rgb *= lerp(1, 0.6, smoothstep(0.38, 0.5, max(q.x, q.y)));
                }
                return fixed4(rgb, alfa);
            }
            ENDCG
        }
    }
}
