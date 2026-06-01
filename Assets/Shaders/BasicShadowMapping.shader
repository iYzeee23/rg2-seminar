// Basic shadow mapping (Williams 1978)
// za svaki fragment: uporedi dubinu iz ugla svetla sa shadow mapom
// ako je fragment dalji => u senci

// reference:
// Williams (1978) https://dl.acm.org/doi/10.1145/965139.807402
// Akenine-Moller et al. (2018) "Real-Time Rendering" https://www.realtimerendering.com/
// Blinn (1977) https://dl.acm.org/doi/10.1145/965141.563893

Shader "Shadows/BasicShadowMapping"
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
                float4 sC : TEXCOORD2; // shadow clip koordinate
                float sD : TEXCOORD3; // linearizovana dubina od svetla
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

                // ista linearizacija kao u ShadowCaster.shader
                float dist = length(wp.xyz - _LightPosition.xyz);
                o.sD = (dist - _LightParams.x) / (_LightParams.y - _LightParams.x);

                // projekcija u prostor svetla za shadow map lookup (Williams 1978)
                o.sC = mul(_LightViewProjection, wp);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Blinn-Phong (Blinn 1977)
                float3 n = normalize(i.wN);
                float3 l = normalize(_LightPosition.xyz - i.wP);
                float lam = max(0, dot(n, l));
                float3 v = normalize(_WorldSpaceCameraPos.xyz - i.wP);
                float spec = pow(max(0, dot(v, reflect(-l, n))), 32);

                // shadow test (Williams 1978)
                // perspektivno deljenje: clip -> NDC [-1,1] -> UV [0,1]
                float2 suv = (i.sC.xy / i.sC.w) * 0.5 + 0.5;
                float mapD = tex2D(_ShadowMap, suv).r;

                // ako je fragment dalji od shadow map dubine => u senci nekog blizeg objekta
                // bias sprecava da povrsina senci samu sebe (RTR, Sec. 7.4.1)
                float shadow = 0;
                if (suv.x > 0 && suv.x < 1 && suv.y > 0 && suv.y < 1)
                    shadow = (i.sD - _ShadowBias > mapD) ? _ShadowStrength : 0;

                float3 col = _Color.rgb * (0.2 + 0.8 * lam * (1 - shadow)) + spec * 0.3 * (1 - shadow);
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
