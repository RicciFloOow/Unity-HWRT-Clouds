Shader "ACloud/Editor/SDFViewer"
{
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        CGINCLUDE
        #include "UnityCG.cginc"
        #include "../../Lib/UtilLib.cginc"

        struct v2f
        {
            float2 uv : TEXCOORD0;
            float4 vertex : SV_POSITION;
        };

        struct Ray
        {
            float3 origin;
            float3 dir;
        };

        v2f vert(uint vertexID : SV_VertexID)
        {
            v2f o;
            o.vertex = GetFullScreenTriangleVertexPosition(vertexID);
            o.uv = GetFullScreenTriangleTexCoord(vertexID);
            return o;
        }

        float _CameraZoom;
        float3 _CameraWorldPos;

        float4 _AABBMax;//w:scale
        float4 _AABBMin;

        float4x4 _VCam_CameraToWorldMatrix;

        float _SDFExitEpsilon;

        sampler3D _VolumeSDFTex;

        Ray GetRayFromScreenSpace(float2 uv)
        {
            Ray ray = (Ray)0;
            float2 ndcCoords = (uv * 2 - 1) * _CameraZoom;
            float3 viewDirection = normalize(float3(ndcCoords.x, ndcCoords.y, 1));
            float3 rayDirection = normalize(mul((float3x3)_VCam_CameraToWorldMatrix, viewDirection));
            ray.origin = _CameraWorldPos;
            ray.dir = rayDirection;
            return ray;
        }

        float2 RayAABBDis(float3 aabbMin, float3 aabbMax, float3 rayOrigin, float3 rayDir)
        {
            float3 t0 = (aabbMin - rayOrigin) / rayDir;
            float3 t1 = (aabbMax - rayOrigin) / rayDir;
            //
            float3 tmin = min(t0, t1);
            float3 tmax = max(t0, t1);
            //
            float disA = max(max(tmin.x, tmin.y), tmin.z);
            float disB = min(min(tmax.x, tmax.y), tmax.z);
            //
            float disToFrontFace = max(0, disA);//到AABB的距离
            float disToInnerFace = max(0, disB - disToFrontFace);//AABB的正面到背面的距离(射线在AABB内的长度)
            return float2(disToFrontFace, disToInnerFace);
        }
        ENDCG

        Pass
        {
            Name "Traditional Ray Marching Times Count Pass"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            bool Marching(Ray ray, float rayLength, out int marchingTimes)
            {
                float3 size = _AABBMax.xyz - _AABBMin.xyz;
                float m = 0;
                marchingTimes = 0;
                [loop]
                while (m < rayLength)
                {
                    marchingTimes++;
                    float3 sp = ray.origin + ray.dir * m - _AABBMin;
                    sp /= size;
                    float sdf = tex3Dlod(_VolumeSDFTex, float4(sp, 0)).x;
                    float diff = _AABBMax.w * (0.5 - sdf);
                    m += diff;
                    if (_SDFExitEpsilon > diff)
                    {
                        return true;
                    }
                }
                return false;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                Ray ray = GetRayFromScreenSpace(i.uv);
                float2 aabb = RayAABBDis(_AABBMin.xyz, _AABBMax.xyz, ray.origin, ray.dir);
                if (aabb.y <= 0)
                {
                    return 1;
                }
                int marchingTimes;
                ray.origin += ray.dir * aabb.x;
                bool isHit = Marching(ray, aabb.y, marchingTimes);
                return isHit ? float4(0, 0, marchingTimes / 8.0, 1) : float4(marchingTimes / 8.0, 0, 0, 1);
            }
            ENDCG
        }
    }
}
