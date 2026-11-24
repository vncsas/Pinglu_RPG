// Fundo sólido do portal, no mesmo formato do arco: esconde o que está atrás
// (o carrossel no lobby, a floresta no mundo). Fica atrás do redemoinho.
// "_Abertura" fecha junto com o redemoinho (encolhe para o centro).
Shader "Pinglu/PortalFundo"
{
    Properties
    {
        _CorFundo ("Cor do fundo", Color) = (0.05, 0.03, 0.10, 1)
        _CorCentro ("Cor do centro", Color) = (0.16, 0.10, 0.30, 1)
        _Abertura ("Abertura (1 aberto, 0 fechado)", Range(0, 1)) = 1
        _Arco ("Altura onde começa a meia-lua (0-1)", Range(0.1, 0.95)) = 0.6
        _Aspecto ("Altura / largura da abertura", Float) = 1.6
    }
    SubShader
    {
        Tags { "Queue" = "Geometry+10" "RenderType" = "Opaque" }
        Cull Off
        ZWrite On
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            float4 _CorFundo, _CorCentro;
            float _Abertura, _Arco, _Aspecto;

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
                float dist = uv.y < _Arco ? 1.0 - abs(x) : 1.0 - length(float2(x, yy));
                dist = min(dist, uv.y * 8.0);
                if (dist <= 0.01) discard;                  // um pouco menor que o redemoinho: a borda brilhante cobre a emenda

                // fecha junto com o redemoinho
                float2 p = float2(x * 0.5, (uv.y - 0.5) * _Aspecto * 0.5);
                float rn = length(p) / (0.5 * max(1.0, _Aspecto));
                if (rn > _Abertura * 1.05) discard;

                float centro = saturate(1.0 - rn * 1.4);
                return fixed4(lerp(_CorFundo.rgb, _CorCentro.rgb, centro * centro), 1.0);
            }
            ENDCG
        }
    }
}
