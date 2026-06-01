// VSM - Variance Shadow Maps (Donnelly & Lauritzen 2006)
// statisticki pristup umesto binarnog testa "da li je u senci"
// procenjuje verovatnocu da je fragment u senci preko Chebyshev nejednakosti

// shadow mapa cuva dva momenta:
//   E(x) = srednja dubina, E(x^2) = srednji kvadrat dubine
//   Var = E(x^2) - E(x)^2
//   pMax = sigma^2 / (sigma^2 + (depth - mean)^2)

// reference:
// Donnelly & Lauritzen (2006) https://dl.acm.org/doi/10.1145/1111411.1111440
// Lauritzen (2007) "Summed-Area Variance Shadow Maps" GPU Gems 3 https://developer.nvidia.com/gpugems/gpugems3/part-ii-light-and-shadows/chapter-8-summed-area-variance-shadow-maps
// Chebyshev nejednakost (jednosmerna varijanta = Cantelli) https://en.wikipedia.org/wiki/Chebyshev%27s_inequality
// Akenine-Moller et al. (2018) "Real-Time Rendering" https://www.realtimerendering.com/

Shader "Shadows/VarianceShadowMapping"
{
    Properties
    {
        _Color ("Color", Color) = (0.8, 0.8, 0.8, 1)
        _ShadowStrength ("Shadow Strength", Float) = 0.6
        _VSMMinVariance ("Min Variance", Float) = 0.0001
        _LightBleedReduction ("Light Bleed Reduction", Float) = 0.2
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
            float _ShadowStrength;
            float _VSMMinVariance;
            float _LightBleedReduction;
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
                // Blinn-Phong (Blinn 1977)
                float3 n = normalize(i.wN);
                float3 l = normalize(_LightPosition.xyz - i.wP);
                float lam = max(0, dot(n, l));
                float3 vd = normalize(_WorldSpaceCameraPos.xyz - i.wP);
                float spec = pow(max(0, dot(vd, reflect(-l, n))), 32);

                float2 suv = (i.sC.xy / i.sC.w) * 0.5 + 0.5;
                
                // 3x3 box blur na momentima - demonstrira filterabilnost (Donnelly 2006)
                // dodatno, Bilinear filter na shadow mapi je aktivan kad je VSM rezim
                float ts = 1.0 / 1024.0;
                float2 moments = float2(0, 0);
                for (int bx = -1; bx <= 1; bx++)
                    for (int by = -1; by <= 1; by++)
                        moments += tex2D(_ShadowMap, suv + float2(bx, by) * ts).rg;
                moments /= 9.0;

                float depth = i.sD;
                float shadow = 0;

                if (suv.x > 0 && suv.x < 1 && suv.y > 0 && suv.y < 1)
                {
                    // samo ako je fragment potencijalno u senci
                    if (depth > moments.x)
                    {
                        // Var(x) = E(x^2) - E(x)^2 (Donnelly 2006)
                        float variance = max(moments.y - moments.x * moments.x, _VSMMinVariance);

                        // Chebyshev gornja granica: pMax = sigma^2 / (sigma^2 + d^2), d = depth - mean
                        // pMax = verovatnoca da fragment nije u senci
                        float dd = depth - moments.x;
                        float pMax = variance / (variance + dd * dd);

                        // light bleed redukcija: remap pMax iz [reduction, 1] u [0, 1]
                        // potiskuje male pMax koje uzrokuju "light bleed" gde se vise okludera preklapa
                        pMax = saturate((pMax - _LightBleedReduction) / (1.0 - _LightBleedReduction));

                        shadow = (1.0 - pMax) * _ShadowStrength;
                    }
                }

                float3 col = _Color.rgb * (0.2 + 0.8 * lam * (1 - shadow)) + spec * 0.3 * (1 - shadow);
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
