//等未来出了Traversal shader就更准确且方便了(因为是forwarded ray)
Shader "ACloud/Sky/ACloud"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            Name "RayTracedCloud"
            Tags{ "LightMode" = "RayTracingACloud" }

            HLSLPROGRAM
            #pragma raytracing ACloudShader
            #include "ACloudLib.cginc"

            [shader("intersection")]
            void Intersection()
            {
                uint cloudIndex = PrimitiveIndex();
                float3 origin = WorldRayOrigin();
                float3 direction = WorldRayDirection();
                //
                float disNear, disFar;
                AABB aabb = _ACloudAABBBuffer[cloudIndex];
                Ray2AABB(aabb.Min, aabb.Max, origin, direction, disNear, disFar);
                CloudAttributeData attributeData = (CloudAttributeData)0;
                attributeData.encodedRange = UnsafeEncodeRange2Uint(disNear, disFar);
                ReportHit(disFar, 0, attributeData);
            }

            [shader("anyhit")]
            void AnyHit(inout RayPayload rayPayload : SV_RayPayload, CloudAttributeData attributeData : SV_IntersectionAttributes)
            {
                UpdateIntersectedAABBIndices(attributeData, rayPayload);
                IgnoreHit();
            }
            ENDHLSL
        }
    }
}
