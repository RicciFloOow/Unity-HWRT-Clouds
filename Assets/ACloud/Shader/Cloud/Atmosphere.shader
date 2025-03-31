Shader "ACloud/Sky/Atmosphere"
{
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Blend One One

        CGINCLUDE
        #include "UnityCG.cginc"
        #include "../Lib/UtilLib.cginc"
        #include "ACloudAtmosphereLib.cginc"

        float4x4 _CubeFaceRenderingCam2WorldMatrix;

        struct v2f
        {
            float2 uv : TEXCOORD0;
            float4 vertex : SV_POSITION;
        };

        v2f vert(uint vertexID : SV_VertexID)
        {
            v2f o;
            o.vertex = GetFullScreenTriangleVertexPosition(vertexID);
            o.uv = GetFullScreenTriangleTexCoord(vertexID);
#if UNITY_UV_STARTS_AT_TOP
            o.uv.y = 1 - o.uv.y;//转回来
#endif
            return o;
        }

        float3 GetViewRayDirFromScreenSpace(float2 uv)
        {
            float2 ndcCoords = (uv * 2 - 1);//FOV为90度
            float3 viewDirection = normalize(float3(ndcCoords.x, ndcCoords.y, 1));
            return normalize(mul((float3x3)_CubeFaceRenderingCam2WorldMatrix, viewDirection));
        }
        ENDCG

        Pass
        {
            Name "High Quality Atmosphere"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            float4 frag (v2f i) : SV_Target
            {
                float3 viewDir = GetViewRayDirFromScreenSpace(i.uv);
                float3 aCol = GetIterativeRenderingInScatteringLight(viewDir, IN_SCATTERING_ITERATIONS_QUALITY_HIGH, OPTICAL_DEPTH_ITERATIONS_QUALITY_HIGH);
                return float4(aCol, 1);
            }
            ENDCG
        }

        Pass
        {
            Name "Normal Quality Atmosphere"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            float3 frag (v2f i) : SV_Target
            {
                float3 viewDir = GetViewRayDirFromScreenSpace(i.uv);
                float3 aCol = GetIterativeRenderingInScatteringLight(viewDir, IN_SCATTERING_ITERATIONS_QUALITY_NORMAL, OPTICAL_DEPTH_ITERATIONS_QUALITY_NORMAL);
                return aCol;
            }
            ENDCG
        }

        Pass
        {
            Name "Low Quality Atmosphere"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            half4 frag (v2f i) : SV_Target
            {
                float3 viewDir = GetViewRayDirFromScreenSpace(i.uv);
                float3 aCol = GetIterativeRenderingInScatteringLight(viewDir, IN_SCATTERING_ITERATIONS_QUALITY_LOW, OPTICAL_DEPTH_ITERATIONS_QUALITY_LOW);
                return float4(aCol, 1);
            }
            ENDCG
        }
    }
}
