#ifndef ACLOUDLIB_INCLUDE
#define ACLOUDLIB_INCLUDE

#include "UnityRaytracingMeshUtils.cginc"
#include "../Lib/RTNoiseHelperLib.cginc"
#include "ACloudDataStruct.cginc"
#include "ACloudAtmosphereLib.cginc"

#define REFLECTABLE_SMOOTHNESS 0.9

struct RayPayload
{
	//xyz: color w:density(这只是我们demo里面用的，正确的应该用transmittance)
	float4 color;
	//eye space depth, 天空盒为_CloudTracingFarPlane
	//当步进得到的density超过阈值时, 我们记录该深度
	float depth;
    bool needSecondaryRay;
    //前16位表示最近的有效无像光源的索引, 后16位表示最近的有效减集的索引
    //解压索引后，如果&0x00008000为1，那么我们认为不受无像光源或是减集的影响
    uint lightMaskIndex;
    ////射线起点到(无像光源的)AABB的交点的范围
    ////前16位表示近表面的距离, 后16位表示远表面的距离, 我们存距离的整数部分(不用float16，这精度还不如整数)
    //uint lightRange;
    ////与上面的类似, 是减集的范围
    //uint maskRange;
	//前16位表示最近的云的近表面的距离, 后16位表示最近的云的索引(距离在前的好处是可以直接比较大小, 值大的距离远, 值小的距离近)
    uint clouds0DisIndex;
	//前16位表示第二近的云的近表面的距离, 后16位表示第二近的云的索引
    uint clouds1DisIndex;
    //前16位表示第三近的云的近表面的距离, 后16位表示第三近的云的索引
    uint clouds2DisIndex;
	//前16位表示第四近的云的近表面的距离, 后16位表示第四近的云的索引
    uint clouds3DisIndex;
    //最近的云的范围
    uint cloudRange0;
};

struct CloudAttributeData
{
    uint encodedRange;
};

struct CloudMarchingInfo 
{
    float m;//射线长度
    float depth;
    float totalDensity;
    uint totalMarchingTime;
};

RaytracingAccelerationStructure _CloudsAccelStruct;

float _CloudSimulationTime;

float _CloudTracingNearPlane;
float _CloudTracingFarPlane;
float _CloudTracingLayerBottom;
float _CloudTracingLayerTop;

float3 _CloudShapeBundleInvSize;
float3 _CameraForward;

float4x4 _CameraVPInvMatrix;

StructuredBuffer<AABB> _ACloudAABBBuffer;
StructuredBuffer<CloudDataField> _ACloudTracingDataFieldBuffer;
//前16为用于表示当前云的形状的索引, 后16位表示当前云的类型
//0~255表示云:为云的类型
//256~511表示无像光源:只计算间接光源影响, 不用于直接着色
//512~1023表示减集:作为Mask来裁剪云
StructuredBuffer<uint> _ACloudTypeBuffer;
//x: tex size, y: tex offset
StructuredBuffer<int2> _ACloudShapeDataBuffer;

Texture3D<float> _CloudShapeBundle_Tex;
Texture3D<float4> _CloudNoise3D_Tex;

SamplerState sampler_CloudShapeBundle_Tex;
SamplerState sampler_CloudNoise3D_Tex;

float3 DepthToWorldSpace(float3 ndcPos)
{
	float4 wpos = mul(_CameraVPInvMatrix, float4(ndcPos, 1));
	return wpos.xyz / wpos.w;
}

float4 GetSkyboxColor(float3 rayDir)
{
    //TODO:夜晚的银河+月亮+极光等
    //TODO:Color Banding
    float sun = rayDir.y >= 0 ? pow(max(0, dot(rayDir, _MainLightDirection)), 256) * _MainLightIntensity : 0;
    //return sun + GetInScatteringLight(float3(0, _CameraRelativeHeight, 0), rayDir);//直接marching的结果
    //TODO:大气层的散射可以采样cubemap(低频更新, 这样即使迭代多一点, 平均下来也不会有太大开销)
    //return sun + GetInScatteringLightFlatHybrid(float3(0, _CameraRelativeHeight, 0), rayDir);
    float3 atmosphere = _AtmosphereCubeTex.SampleLevel(sampler_AtmosphereCubeTex, rayDir, 0).xyz;
    return float4(sun + atmosphere, 1);
}

//如果不输出深度, 那么在输出前需要基于depth以及payload.depth(eye space的)来修改density
void CloudZTest(float depth, inout RayPayload payload)
{
	//需要将depth转为eye space的来比较(因为相机与云渲染时的near/far plane的设置不同)
	//TODO:
}

RayPayload GetDefaultRayPayload()
{
    RayPayload payload = (RayPayload)0;
    payload.lightMaskIndex = 0x80008000;
    payload.clouds0DisIndex = 0xFFFF8000;
    payload.clouds1DisIndex = 0xFFFF8000;
    payload.clouds2DisIndex = 0xFFFF8000;
    payload.clouds3DisIndex = 0xFFFF8000;
    payload.cloudRange0 = 0x0000FFFF;
    return payload;
}

uint CountRayToMarchingRay(uint state)
{
    return 0x40000000 | (state << 15);
}

//判断当前是否是最后一个实例(不用管当前cloud的类型)
bool IsCurrentInstanceNotTheLast(uint state)
{
    uint totalCount = (0x3FFFFFFF & state) >> 15;
    uint currentCount = 0x00007FFF & state;
    return currentCount < totalCount;
}

bool Ray2AABB(float3 aabbMin, float3 aabbMax, float3 rayOrigin, float3 rayDir, out float disNear, out float disFar)
{
    float3 t0 = (aabbMin - rayOrigin) / rayDir;
    float3 t1 = (aabbMax - rayOrigin) / rayDir;

    float3 tmin = min(t0, t1);
    float3 tmax = max(t0, t1);

    float disF = max(max(tmin.x, tmin.y), tmin.z);//front face
    float disB = min(min(tmax.x, tmax.y), tmax.z);//back face

    disNear = max(0, disF);
    disFar = max(disNear, disB);
    return disF <= disB;
}

uint UnsafeEncodeRange2Uint(float near, float far)
{
    //理论上相机到目标的距离不会超过2^16-1, 因为一般来说10km的范围已经比较离谱了
    uint n = (uint)floor(near);
    uint f = (uint)ceil(far);
    return (n << 16) | f;
}

bool EncodedRangeIntersection(uint r0, uint r1)
{
    uint r0n = r0 >> 16;
    uint r0f = r0 & 0x0000FFFF;
    uint r1n = r1 >> 16;
    uint r1f = r1 & 0x0000FFFF;
    return !((r0n > r1f) || (r1n > r0f));
}

bool IsCloudMaybeRendered(inout uint arr[4], uint currentDis)
{
    bool hasLarger = false;
    uint maxValue = arr[0];
    uint maxValueIndex = 0;
	[unroll(4)]
    for (uint i = 0; i < 4; i++)
    {
        if (arr[i] > currentDis)
        {
            hasLarger = true;
        }
        if (arr[i] > maxValue)
        {
            maxValue = arr[i];
            maxValueIndex = i;
        }
    }
	[branch]
    if (!hasLarger)
    {
        return false;
    }
    arr[maxValueIndex] = currentDis;
    for (uint k = 0; k < 3; k++)//冒泡排序升序排列
    {
        for (uint m = 0; m < 3 - k; m++)
        {
            if (arr[m] > arr[m + 1])
            {
                uint swap = arr[m];
                arr[m] = arr[m + 1];
                arr[m + 1] = swap;
            }
        }
    }
    return true;
}

void UpdateIntersectedAABBIndices(CloudAttributeData attributeData, inout RayPayload rayPayload)
{
    uint cloudIndex = PrimitiveIndex();
    uint cloudCatType = _ACloudTypeBuffer[cloudIndex];
    uint cloudType = cloudCatType & 0x0000FFFF;
    //
    if (cloudType < 0x00000100)
    {
        //程序化/自定义的云
        uint currentDis = (attributeData.encodedRange & 0xFFFF0000) | cloudIndex;
        //if (currentDis < rayPayload.clouds0DisIndex)//如果只考虑最近的一个的话(这样表现结果会有很多问题)
        //{
        //    rayPayload.clouds0DisIndex = currentDis;
        //    rayPayload.cloudRange0 = attributeData.encodedRange;
        //}
        
        uint cloudsArray[4] =
        {
            rayPayload.clouds0DisIndex,
			rayPayload.clouds1DisIndex,
			rayPayload.clouds2DisIndex,
			rayPayload.clouds3DisIndex
        };
        if (IsCloudMaybeRendered(cloudsArray, currentDis))
        {
            rayPayload.clouds0DisIndex = cloudsArray[0];
            rayPayload.clouds1DisIndex = cloudsArray[1];
            rayPayload.clouds2DisIndex = cloudsArray[2];
            rayPayload.clouds3DisIndex = cloudsArray[3];
            rayPayload.cloudRange0 = attributeData.encodedRange;
        }
    }
    else if (cloudType < 0x00000200)
    {
        //无像光源
        if (EncodedRangeIntersection(attributeData.encodedRange, rayPayload.cloudRange0))
        {
            //替换
            rayPayload.lightMaskIndex = (cloudIndex << 16) | (0x0000FFFF & rayPayload.lightMaskIndex);
        }
    }
    else if (cloudType < 0x00000400)
    {
        //减集
        if (EncodedRangeIntersection(attributeData.encodedRange, rayPayload.cloudRange0))
        {
            //替换
            rayPayload.lightMaskIndex = cloudIndex | (0xFFFF0000 & rayPayload.lightMaskIndex);
        }
    }
}

float FoldingPow(float x, float p)
{
    return pow(abs(abs(x * 2.0 - 1.0) * 2.0 - 1.0), p);
}

//ref: https://web.archive.org/web/20141102063940/http://omlc.org/education/ece532/class3/hg.html
float HGScatteringFunction(float cosTheta, float g)
{
    float g2 = g * g;
    float denom = rsqrt(1 + g2 - 2 * g * cosTheta);
    return (1 - g2) * denom * denom * denom / 12.56637061436;//4 * pi
}

void UnpackCloudShapeSizeOffset(uint category, out uint3 texSize, out uint3 texOffset)
{
	int2 packedData = _ACloudShapeDataBuffer[category];
	texSize = uint3(packedData.x >> 20, (packedData.x >> 10) & 0x000003FF, packedData.x & 0x000003FF);
	texOffset = uint3(packedData.y >> 20, (packedData.y >> 10) & 0x000003FF, packedData.y & 0x000003FF);
}

float GetDistanceFromCloudShapeSDF(float3 localSamplePos, float3 aabbSize, uint3 texSize, uint3 texOffset)
{
	float3 scale = texSize / aabbSize;
	float3 sampleCoord = localSamplePos * scale + texOffset;
    sampleCoord = clamp(sampleCoord + 0.5, texOffset, texOffset + texSize - 1);
    float sdf = _CloudShapeBundle_Tex.SampleLevel(sampler_CloudShapeBundle_Tex, sampleCoord * _CloudShapeBundleInvSize, 0);
	return (0.5 - sdf);
}

//
float GetDetailDensityFromCloudNoise(float3 worldSamplePos)
{
    float3 windAnim = float3(0.6, 0, 0.8) * _CloudSimulationTime * 0.025;//非常简易的风的影响
    float3 sp = worldSamplePos * 0.0018 + windAnim;
    float4 noise = _CloudNoise3D_Tex.SampleLevel(sampler_CloudNoise3D_Tex, sp, 0);
    return saturate(lerp(1 - FoldingPow(noise.x, 4), FoldingPow(noise.y, 2), noise.z)) * 2 - 1;
}

float MarchingDirectLight(float3 rayOrigin, AABB aabb, uint3 cloudSize, uint3 cloudOffset)
{
    //TODO: marching directional light scattering
    //TODO: 以rayOrigin + (rFar + 1e-5) * _MainLightDirection为起点, 继续tracing最近的aabb
    //也就是需要创建rayPayload.rayState为2的射线, 并且RAY_FLAG不能为RAY_FLAG_FORCE_NON_OPAQUE
    //这个射线的存在可以在绝大多数情况下实现云间的阴影(当然也有可能存在"错误"的检测到其他类型"云"的情况, 比如减集或是光源)
    //但是, 这样做了之后会有个问题, 就是其他几何体对云的阴影就可能会与之冲突了: 比如山对云的影响
    //至少我玩forbidden west的时候没看到山对云的阴影
    //所以这一块我也只是在考虑中而没全部实现(感觉用SSCS来做应该不错, 特别是对于NPR的结果来说)
    return 0;//
}

void MarchingGivenCloud(float rayDotCam, float3 rayOrigin, float3 rayDir, uint cloudIndex, inout CloudMarchingInfo marchingInfo)
{
    AABB cloudAABB = _ACloudAABBBuffer[cloudIndex];
    float cloudDisNear, cloudDisFar;
    float3 cloudAABBSize = cloudAABB.Max - cloudAABB.Min;
    Ray2AABB(cloudAABB.Min, cloudAABB.Max, rayOrigin, rayDir, cloudDisNear, cloudDisFar);
    float testRayLength = cloudDisFar - cloudDisNear;
    uint3 cloudSize, cloudOffset;
    uint cloudCatType = _ACloudTypeBuffer[cloudIndex];
    uint cloudCat = cloudCatType >> 16;
    UnpackCloudShapeSizeOffset(cloudCat, cloudSize, cloudOffset);
    marchingInfo.m = cloudDisNear;
    //
    //
    [loop]
    while (marchingInfo.m < cloudDisFar)
    {
        marchingInfo.totalMarchingTime++;
        float3 wsp = rayOrigin + rayDir * marchingInfo.m;
        float3 lsp = wsp - cloudAABB.Min;//local sample pos
        float sdf = GetDistanceFromCloudShapeSDF(lsp, cloudAABBSize, cloudSize, cloudOffset);
        float dis = sdf * cloudAABBSize.x;
        if (sdf <= 0.1)
        {
            float adaptiveStep = sqrt(marchingInfo.m) * 0.2;
            dis = max(adaptiveStep, 1);
            //TODO:需要处理lightMaskIndex, 特别是减集需要降低density
            float baseDensity = 0.1 - sdf;//基础的density我们也用sdf的值来简单模拟一下(正常的话应该通过density计算transmittance, 不过我们这density数值太大就不考虑转换了)
            float detailDensity = GetDetailDensityFromCloudNoise(wsp);
            float density = baseDensity + detailDensity * 0.05;//baseDensity + detailDensity * 0.05
            marchingInfo.totalDensity += density;
            if (density > 0)
            {
                //TODO:light
            }
            //
            if (marchingInfo.totalDensity > 0.9)
            {
                float ed = rayDotCam * marchingInfo.m;
                marchingInfo.depth = min(marchingInfo.depth, ed);//marchingInfo.m是越来越大的, 因此能保证记录的即是最小的
            }
        }
        marchingInfo.m += dis;
    }
    marchingInfo.totalDensity = saturate(marchingInfo.totalDensity);
}

bool CheckIfNeedToContinueMarching(CloudMarchingInfo marchingInfo, uint nextIndex, out bool hitCloud)
{
    hitCloud = marchingInfo.totalDensity > 0;
    if ((nextIndex & 0x00008000) != 0 || marchingInfo.totalMarchingTime >= 128)//无效或是总次数超过阈值
    {
        return hitCloud;
    }
    else
    {
        return marchingInfo.totalDensity >= 1;
    }
}

bool MarchingCloud(float3 rayOrigin, float3 rayDir, float4 skyCol, inout RayPayload rayPayload)
{
    uint cloudIndex0 = rayPayload.clouds0DisIndex & 0x0000FFFF;
    uint cloudIndex1 = rayPayload.clouds1DisIndex & 0x0000FFFF;
    uint cloudIndex2 = rayPayload.clouds2DisIndex & 0x0000FFFF;
    uint cloudIndex3 = rayPayload.clouds3DisIndex & 0x0000FFFF;
	//
	if ((cloudIndex0 & 0x00008000) != 0)
	{
	    //未命中云, 可能碰到的是减集或是无像光源
	    return false;
	}
	//
    CloudMarchingInfo marchingInfo = (CloudMarchingInfo)0;
    marchingInfo.depth = _CloudTracingFarPlane;
    float rayDotCam = dot(rayDir, _CameraForward);
    bool hitCloud;
	//
	//TODO:区分渲染精度, 低精度的可以直接用attributeData的encodedRange来估计最近的AABB的采样点(或者用rayPayload的cloudRange0)
    MarchingGivenCloud(rayDotCam, rayOrigin, rayDir, cloudIndex0, marchingInfo);
	//
    if (CheckIfNeedToContinueMarching(marchingInfo, cloudIndex1, hitCloud))
    {
        if (hitCloud)
        {
            //注意, 正常的云的渲染的alpha应该用transmittance, 我们这里就用density代替一下
            rayPayload.color = float4(lerp(skyCol.xyz, 1, marchingInfo.totalDensity), marchingInfo.totalDensity);
            rayPayload.depth = marchingInfo.depth;
            return true;
        }
        else
        {
            return false;
        }
    }
    //尝试Marching cloudIndex1
    MarchingGivenCloud(rayDotCam, rayOrigin, rayDir, cloudIndex1, marchingInfo);
    //
    if (CheckIfNeedToContinueMarching(marchingInfo, cloudIndex2, hitCloud))
    {
        if (hitCloud)
        {
            rayPayload.color = float4(lerp(skyCol.xyz, 1, marchingInfo.totalDensity), marchingInfo.totalDensity);
            rayPayload.depth = marchingInfo.depth;
            return true;
        }
        else
        {
            return false;
        }
    }
    //尝试Marching cloudIndex2
    MarchingGivenCloud(rayDotCam, rayOrigin, rayDir, cloudIndex2, marchingInfo);
    //
    if (CheckIfNeedToContinueMarching(marchingInfo, 0x00008000, hitCloud))
    {
        if (hitCloud)
        {
            rayPayload.color = float4(lerp(skyCol.xyz, 1, marchingInfo.totalDensity), marchingInfo.totalDensity);
            rayPayload.depth = marchingInfo.depth;
            return true;
        }
        else
        {
            return false;
        }
    }

	return false;
}



#endif