// renderuje dubinu scene iz ugla svetla (replacement shader na shadow kameri)
// R = depth (koriste sve tehnike), G = depth^2 (koristi samo VSM za varijansu)

// reference:
// Williams (1978) https://dl.acm.org/doi/10.1145/965139.807402
// Donnelly & Lauritzen (2006) https://dl.acm.org/doi/10.1145/1111411.1111440

// korisni materijali iz dokumentacije:
// https://docs.unity3d.com/ScriptReference/Camera.SetReplacementShader.html
// https://docs.unity3d.com/ScriptReference/RenderTextureFormat.RGFloat.html

// linearizacija dubine: GPU Gems 3
// https://developer.nvidia.com/gpugems/gpugems3/part-ii-light-and-shadows/chapter-10-parallel-split-shadow-maps-programmable-gpus

Shader "Shadows/ShadowCaster"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float depth : TEXCOORD0; };

            // globalne, postavlja ShadowMapCamera.cs
            float4 _LightPosition;
            float4 _LightParams; // x = near, y = far

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);

                // d = (dist - near) / (far - near) => [0, 1]
                float4 wp = mul(unity_ObjectToWorld, v.vertex);
                float dist = length(wp.xyz - _LightPosition.xyz);
                o.depth = (dist - _LightParams.x) / (_LightParams.y - _LightParams.x);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                // R = depth, G = depth^2
                // Var(x) = E(x^2) - E(x)^2 (Donnelly 2006)
                return float4(i.depth, i.depth * i.depth, 0, 1);
            }
            ENDCG
        }
    }
}
