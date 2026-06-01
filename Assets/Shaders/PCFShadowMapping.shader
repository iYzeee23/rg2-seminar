// PCF - Percentage Closer Filtering (Reeves et al. 1987)
// kljucna ideja: filtriraj rezultate poredjenja, ne same dubine
// interpolirane dubine nemaju fizicko znacenje - mogu pasti izmedju dva objekta

// reference:
// Reeves, Salesin & Cook (1987) https://dl.acm.org/doi/10.1145/37402.37435
// GPU Gems Ch. 11 https://developer.nvidia.com/gpugems/gpugems/part-ii-lighting-and-shadows/chapter-11-shadow-map-antialiasing
// Akenine-Moller et al. (2018) "Real-Time Rendering" https://www.realtimerendering.com/

Shader "Shadows/PCFShadowMapping"
{
    Properties
    {
        _Color ("Color", Color) = (0.8, 0.8, 0.8, 1)
        _ShadowBias ("Shadow Bias", Float) = 0.005
        _ShadowStrength ("Shadow Strength", Float) = 0.6
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "LightMode" = "ForwardBase" }
        LOD 100
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 wN : TEXCOORD0;
                float3 wP : TEXCOORD1;
                float4 sC : TEXCOORD2;
                float sD : TEXCOORD3;
            };

            float4 _Color;
            float _ShadowBias;
            float _ShadowStrength;
            sampler2D _ShadowMap;
            float4 _LightPosition;
            float4 _LightParams;
            float4x4 _LightViewProjection;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wN = UnityObjectToWorldNormal(v.normal);
                float4 wp = mul(unity_ObjectToWorld, v.vertex);
                o.wP = wp.xyz;
                float dist = length(wp.xyz - _LightPosition.xyz);
                o.sD = (dist - _LightParams.x) / (_LightParams.y - _LightParams.x);
                o.sC = mul(_LightViewProjection, wp);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Blinn-Phong (isto kao BasicShadowMapping)
                float3 n = normalize(i.wN);
                float3 l = normalize(_LightPosition.xyz - i.wP);
                float lam = max(0, dot(n, l));
                float3 vd = normalize(_WorldSpaceCameraPos.xyz - i.wP);
                float spec = pow(max(0, dot(vd, reflect(-l, n))), 32);

                float2 suv = (i.sC.xy / i.sC.w) * 0.5 + 0.5;

                // velicina jednog teksela u shadow mapi
                float ts = 1.0 / 1024.0;

                // PCF 5x5 kernel (Reeves 1987)
                // svaki uzorak: poredi pa onda usrednji binarne rezultate
                float shadow = 0;
                for (int x = -2; x <= 2; x++)
                {
                    for (int y = -2; y <= 2; y++)
                    {
                        float d = tex2D(_ShadowMap, suv + float2(x, y) * ts).r;
                        shadow += (i.sD - _ShadowBias > d) ? 1.0 : 0.0;
                    }
                }

                // 25 uzoraka => [0, 1] procenat u senci
                shadow = shadow / 25.0 * _ShadowStrength;

                if (suv.x < 0 || suv.x > 1 || suv.y < 0 || suv.y > 1)
                    shadow = 0;

                float3 col = _Color.rgb * (0.2 + 0.8 * lam * (1 - shadow)) + spec * 0.3 * (1 - shadow);
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
