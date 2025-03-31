#ifndef RTNOISEHELPERLIB_INCLUDE
#define RTNOISEHELPERLIB_INCLUDE

#define FLT_EPSILON     1.192092896e-07 // Smallest positive number, such that 1.0 + FLT_EPSILON != 1.0
#define FLT_MIN         1.175494351e-38 // Minimum representable positive floating-point number
#define FLT_MAX         3.402823466e+38 // Maximum representable floating-point number
#define TWO_PI          6.28318530718

// https://www.pcg-random.org/
float pcgHash(inout uint state)
{
	state = state * 747796405u + 2891336453u;
	uint word = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u;
	word = (word >> 22u) ^ word;
	return word / 4294967295.0;
}

float pcgHashNormalDistribution(inout uint state)
{
	float theta = TWO_PI * pcgHash(state);
	float rho = sqrt(-2 * log(pcgHash(state)));
	return rho * cos(theta);
}

float3 pcgRandomDirection(inout uint state)
{
	float x = pcgHashNormalDistribution(state);
	float y = pcgHashNormalDistribution(state);
	float z = pcgHashNormalDistribution(state);
	return normalize(float3(x, y, z));//TODO: use safe normalize
}

float3 pcgRandomHemisphereDirection(float3 normal, inout uint state)
{
	float3 dir = pcgRandomDirection(state);
	return dir * sign(dot(normal, dir));
}

float2 pcgRandomPointInUnitDisk(inout uint state)
{
	float randomAngle = TWO_PI * pcgHash(state);
	float sinTheta, cosTheta;
	sincos(randomAngle, sinTheta, cosTheta);
	return pcgHash(state) * float2(cosTheta, sinTheta);
}
#endif