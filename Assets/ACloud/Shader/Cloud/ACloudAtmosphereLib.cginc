//ref: https://developer.nvidia.com/gpugems/gpugems2/part-ii-shading-lighting-and-shadows/chapter-16-accurate-atmospheric-scattering
//ref: http://nishitalab.org/user/nis/cdrom/sig93_nis.pdf Display of the Earth Taking into Account Atmospheric Scattering
//ref: https://www2.imm.dtu.dk/pubdb/edoc/imm2554.pdf Real Time Rendering of Atmospheric Scattering Effects for Flight Simulation
#ifndef ACLOUDATMOSPHERELIB_INCLUDE
#define ACLOUDATMOSPHERELIB_INCLUDE

#define OPTICAL_DEPTH_ITERATIONS 16
#define IN_SCATTERING_ITERATIONS 4

#define OPTICAL_DEPTH_ITERATIONS_QUALITY_HIGH 16
#define OPTICAL_DEPTH_ITERATIONS_QUALITY_NORMAL 8
#define OPTICAL_DEPTH_ITERATIONS_QUALITY_LOW 4

#define IN_SCATTERING_ITERATIONS_QUALITY_HIGH 32
#define IN_SCATTERING_ITERATIONS_QUALITY_NORMAL 16
#define IN_SCATTERING_ITERATIONS_QUALITY_LOW 8

int _IterativeRenderingStep;

float _CameraRelativeHeight;
float _HeightScaleValue;
float _AtmosphericThickness;
float _PlanetRadius;
float _MainLightIntensity;

float3 _MainLightDirection;
float3 _SunLightDirection;//这个和上面的区别在于上面的是实时更新的, 而这个是间隔更新的
float3 _RayleighScatterParam;//1 / pow(wavelength, 4) = pow(1 / wavelength, 4)

TextureCube _AtmosphereCubeTex;
SamplerState sampler_AtmosphereCubeTex;

float PhaseFunction(float g, float cosTheta)
{
	float g2 = g * g;
	return 3 * (1 - g2) * (1 + cosTheta * cosTheta) / ((4 + 2 * g2) * pow(1 + g2 - 2 * g * cosTheta, 1.5));
}

float PhaseFunctionApprox(float cosTheta)
{
	return 0.75 * (1 + cosTheta * cosTheta);
}

//参考文献中给出的分子密度为K*exp(-h/H_0), 其中K是一个大于0的常量, H_0是一个在给定条件下的比例值, h是海平面高度
//但这样的模型有个问题, 就是其大气层的厚度是无穷的：因为不论h多高, 分子的密度总是大于0
//我们这里先将高度"归一化"(h01):0是海平面高度, 1是大气层厚度
//然后在exp()的基础上乘上一个(1-h01)来保证在h01=1时密度为0
float GetPointDensityFlat(float height)
{
	//大气层与地面都是平面的情况("地平"世界)
	float h01 = saturate(height / _AtmosphericThickness);//[0, 1]
	return (1 - h01) * exp(-h01 * _HeightScaleValue);
}

float GetPointDensitySpherical(float3 pos, float3 planetCenter)
{
	float height = length(pos - planetCenter) - _PlanetRadius;
	float h01 = saturate(height / _AtmosphericThickness);
	return (1 - h01) * exp(-h01 * _HeightScaleValue);
}

float GetPointDensitySpherical(float3 pos)
{
	float height = length(pos) - _PlanetRadius;
	float h01 = saturate(height / _AtmosphericThickness);
	return (1 - h01) * exp(-h01 * _HeightScaleValue);
}

//基于GetPointDensityFlat()积分
//不定积分\int (1 - x) * exp(-k * x) dx = (1 + (x - 1) * k) * exp(-k * x) / k^2 + const(这里我们令constant为0, 因为实际需要用到的是定积分)
float GetPointDensityFlatIntegral(float x)
{
	x = saturate(x / _AtmosphericThickness);
	return (1 + (x - 1) * _HeightScaleValue) * exp(-x * _HeightScaleValue) / (_HeightScaleValue * _HeightScaleValue);
}

float GetOpticalDepthFlat(float hStart, float hEnd)
{
	//主要注意的是, 定积分的值与上下限的顺序有关, 而我们需要的Optical Depth应当与顺序无关, 因此输出时多一个绝对值
	float d_s = GetPointDensityFlatIntegral(hStart);
	float d_e = GetPointDensityFlatIntegral(hEnd);
	return abs(d_s - d_e);
}

float GetOpticalDepthSpherical(float3 rayOrigin, float3 rayDir, float rayLength)//TODO:定积分的解析式or数值近似
{
	float3 spos = rayOrigin;
	float step = rayLength / (OPTICAL_DEPTH_ITERATIONS - 1);
	float od = 0;

	for (uint i = 0; i < OPTICAL_DEPTH_ITERATIONS; i++)
	{
		float d = GetPointDensitySpherical(spos);
		spos += rayDir * step;
		od += d * step;
	}
	return od;
}

float GetOpticalDepthSpherical(float3 rayOrigin, float3 rayDir, float rayLength, uint totalIteration)
{
	float3 spos = rayOrigin;
	float step = rayLength / (totalIteration - 1);
	float od = 0;

	for (uint i = 0; i < totalIteration; i++)
	{
		float d = GetPointDensitySpherical(spos);
		spos += rayDir * step;
		od += d * step;
	}
	return od;
}

float GetAdjacentEdgeLengthWithAngle(float cosTheta, float a, float c)
{
	return c * cosTheta + sqrt(max(0, c * c * (cosTheta * cosTheta - 1) + a * a));
}

//当我们假设在计算天空盒时, 玩家的坐标xz坐标总是0, 只有y(也就是高度)有作用的情况下(并且我们可以默认一定有交点, 相机一定在大气层内)
//我们可以用已知三角形的两条边长与一个夹角来快速得到未知的第三边的长度(不需要通过ray-sphere intersection来求解):
//记射线原点为A, 球心为B, 交点为C,
//那么cos(\angle CAB) = dot(rayDir, float3(0, -1, 0)) = -rayDir.y
//BC长_PlanetRadius + _AtmosphericThickness
//AB长_PlanetRadius + height
//AC就可以求解了: 是一个一元二次方程, delta由于我们假设一定有交点因此不需要管, 而加减号则是用加号(根据图像分析)
float GetToSphereSurfaceRayLength(float height, float3 rayDir)
{
	float cosTheta = -rayDir.y;
	float a = _PlanetRadius + _AtmosphericThickness;//BC
	float c = _PlanetRadius + height;//AB
	return GetAdjacentEdgeLengthWithAngle(cosTheta, a, c);
}

float3 GetInScatteringLight(float3 rayOrigin, float3 rayDir)
{
	rayOrigin += float3(0, _PlanetRadius, 0);
	//
	float3 inScatteringPos = rayOrigin;
	float3 inScatteringLight = 0;
	//
	float atmoRadius = _PlanetRadius + _AtmosphericThickness;
	float cosPhi = dot(rayDir, -_MainLightDirection);
	float rayleighPhase = PhaseFunctionApprox(cosPhi);//Rayleigh散射的g约为0, 就用近似的
	//
	if (rayDir.y < 0 && rayDir.y < sqrt(1 - _PlanetRadius * _PlanetRadius / (rayOrigin.y * rayOrigin.y)))//实际中我们可以直接用rayDir.y < 0
	{
		return 0;
	}
	//
	float viewRayLength = GetToSphereSurfaceRayLength(rayOrigin.y - _PlanetRadius, rayDir);
	float step = viewRayLength / (IN_SCATTERING_ITERATIONS - 1);
	//
	for (uint i = 0; i < IN_SCATTERING_ITERATIONS; i++)
	{
		float viewRayOD = GetOpticalDepthSpherical(inScatteringPos, -rayDir, i * step);
		//与viewRayLength类似, 因为我们假设存在交点, 也是很容易求得的(当然, 这样的结果在太阳在水平面下时是会有问题的, 我们这里就不处理了)
		float lightRayLength = GetAdjacentEdgeLengthWithAngle(cosPhi, atmoRadius, length(inScatteringPos));
		float lightRayOD = GetOpticalDepthSpherical(inScatteringPos, _MainLightDirection, lightRayLength);
		//
		float3 attenuate = exp(-(viewRayOD + lightRayOD) * _RayleighScatterParam);
		//
		inScatteringLight += attenuate * GetPointDensitySpherical(inScatteringPos) * _RayleighScatterParam * step;
		inScatteringPos += rayDir * step;
	}
	//
	return _MainLightIntensity * rayleighPhase * inScatteringLight;//
}

//用GetOpticalDepthFlat()替换开销大的GetOpticalDepthSpherical()
float3 GetInScatteringLightFlatHybrid(float3 rayOrigin, float3 rayDir)
{
	rayOrigin += float3(0, _PlanetRadius, 0);
	//
	float3 inScatteringPos = rayOrigin;
	float3 inScatteringLight = 0;
	//
	float atmoRadius = _PlanetRadius + _AtmosphericThickness;
	float cosPhi = dot(rayDir, -_MainLightDirection);
	float rayleighPhase = PhaseFunctionApprox(cosPhi);
	//
	if (rayDir.y < 0)
	{
		return 0;
	}
	//
	float viewRayLength = GetToSphereSurfaceRayLength(rayOrigin.y - _PlanetRadius, rayDir);
	float step = viewRayLength / (IN_SCATTERING_ITERATIONS - 1);
	//
	for (uint i = 0; i < IN_SCATTERING_ITERATIONS; i++)
	{
		float viewRayOD = GetOpticalDepthFlat(inScatteringPos.y, inScatteringPos.y - rayDir.y * i * step);
		float lightRayLength = GetAdjacentEdgeLengthWithAngle(cosPhi, atmoRadius, length(inScatteringPos));
		float lightRayOD = GetOpticalDepthFlat(inScatteringPos.y, inScatteringPos.y + _MainLightDirection.y * lightRayLength);
		//
		float3 attenuate = exp(-(viewRayOD + lightRayOD) * _RayleighScatterParam);
		//
		//这里用GetPointDensitySpherical()效果会更好一点
		//inScatteringLight += attenuate * GetPointDensityFlat(inScatteringPos.y - _PlanetRadius) * _RayleighScatterParam * step;
		inScatteringLight += attenuate * GetPointDensitySpherical(inScatteringPos) * _RayleighScatterParam * step;
		inScatteringPos += rayDir * step;
	}
	//
	return _MainLightIntensity * rayleighPhase * inScatteringLight;//
}

float3 GetIterativeRenderingInScatteringLight(float3 rayDir, uint inScatteringIteration, uint odIteration)
{
	float3 rayOrigin = float3(0, _PlanetRadius + _CameraRelativeHeight, 0);
	float atmoRadius = _PlanetRadius + _AtmosphericThickness;
	float cosPhi = dot(rayDir, -_SunLightDirection);
	float rayleighPhase = PhaseFunctionApprox(cosPhi);
	//
	if (rayDir.y < 0)
	{
		return 0;
	}
	//
	float viewRayLength = GetToSphereSurfaceRayLength(rayOrigin.y - _PlanetRadius, rayDir);
	float step = viewRayLength / (inScatteringIteration - 1);
	float3 inScatteringPos = rayOrigin + rayDir * step * _IterativeRenderingStep;
	float viewRayOD = GetOpticalDepthSpherical(inScatteringPos, -rayDir, _IterativeRenderingStep * step, odIteration);
	float lightRayLength = GetAdjacentEdgeLengthWithAngle(cosPhi, atmoRadius, length(inScatteringPos));
	float lightRayOD = GetOpticalDepthSpherical(inScatteringPos, _SunLightDirection, lightRayLength);
	float3 attenuate = exp(-(viewRayOD + lightRayOD) * _RayleighScatterParam);
	float3 inScatteringLight = attenuate * GetPointDensitySpherical(inScatteringPos) * _RayleighScatterParam * step;
	return _MainLightIntensity * rayleighPhase * inScatteringLight;
}

#endif