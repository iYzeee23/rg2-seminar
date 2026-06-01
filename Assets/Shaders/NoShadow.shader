// referentni shader bez senki (mode 0)
// Blinn-Phong osvetljenje: ambient + diffuse + specular

// reference:
// Phong (1975) "Illumination for computer generated pictures" https://dl.acm.org/doi/10.1145/360825.360839
// Blinn (1977) "Models of light reflection for computer synthesized pictures" https://dl.acm.org/doi/10.1145/965141.563893
Shader "Shadows/NoShadow"
{
    Properties
    {
        _Color ("Color", Color) = (0.8, 0.8, 0.8, 1)
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
            struct v2f { float4 pos : SV_POSITION; float3 wN : TEXCOORD0; float3 wP : TEXCOORD1; };

            float4 _Color;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wN = UnityObjectToWorldNormal(v.normal);
                o.wP = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.wN);

                // smer directional light-a
                float3 l = normalize(_WorldSpaceLightPos0.xyz);
                float lam = max(0, dot(n, l));
                float3 v = normalize(_WorldSpaceCameraPos.xyz - i.wP);

                // spekularna sa eksponentom 32
                float spec = pow(max(0, dot(v, reflect(-l, n))), 32);
                float3 col = _Color.rgb * (0.2 + 0.8 * lam) + spec * 0.3;
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
