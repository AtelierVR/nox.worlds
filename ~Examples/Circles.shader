Shader "Unlit/Circles"
{
    Properties
    {
        _CircleColour ("Circle Colour", color) = (1, 1, 1, 1)
        _BaseColour ("Base Colour", color) = (1, 1, 1, 0)
        _Radius1 ("Radius 1 (m)", float) = 0.5
        _Radius2 ("Radius 2 (m)", float) = 1.2
        _Radius3 ("Radius 3 (m)", float) = 3.0
        _LineThickness ("Line Thickness (m)", float) = 0.02
        _Center ("Center (world)", Vector) = (0, 0, 0, 0)
        _ODistance ("Start Transparency Distance", float) = 5
        _TDistance ("Full Transparency Distance", float) = 10
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_SINGLE_PASS_STEREO STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON
            #include "UnityCG.cginc"

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _CircleColour;
            fixed4 _BaseColour;
            float _Radius1;
            float _Radius2;
            float _Radius3;
            float _LineThickness;
            float4 _Center;
            float _ODistance;
            float _TDistance;

            // Implémentation custom de smoothstep (HLSL ne fournit pas toujours l'intrinsique attendu)
            inline float SmoothStepCustom(float edge0, float edge1, float x)
            {
                float t = saturate((x - edge0) / max(1e-6, edge1 - edge0));
                return t * t * (3.0 - 2.0 * t);
            }

            // Fonction utilitaire hors du fragment (les fonctions imbriquées ne sont pas valides en HLSL/CG)
            inline float LineContribution(float dist, float r, float halfThickness, float pixelWidth)
            {
                float d = abs(dist - r);
                float edge0 = halfThickness - pixelWidth;
                float edge1 = halfThickness + pixelWidth;
                // Inversion pour que centre de la bande => alpha 1
                return saturate(1.0 - SmoothStepCustom(edge0, edge1, d));
            }

            v2f vert (appdata_full v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Distance horizontale (XZ) au centre monde
                float2 centerXZ = _Center.xz;
                float2 posXZ = i.worldPos.xz;
                float dist = length(posXZ - centerXZ);

                // Antialiasing basé sur fwidth (taille approx. d'un pixel en espace monde projeté)
                float pixelWidth = fwidth(dist);
                float halfThickness = _LineThickness * 0.5;

                // Contributions des trois cercles
                float a1 = LineContribution(dist, _Radius1, halfThickness, pixelWidth);
                float a2 = LineContribution(dist, _Radius2, halfThickness, pixelWidth);
                float a3 = LineContribution(dist, _Radius3, halfThickness, pixelWidth);

                float lineAlpha = max(max(a1, a2), a3);
                fixed4 col = lerp(_BaseColour, _CircleColour, lineAlpha);

                // Distance falloff depuis la caméra
                float3 viewDirW = _WorldSpaceCameraPos - i.worldPos;
                float viewDist = length(viewDirW);
                float denom = max(1e-5, _TDistance - _ODistance);
                float falloff = saturate((viewDist - _ODistance) / denom);
                col.a *= (1.0 - falloff);

                return col;
            }
            ENDCG
        }
    }
}
