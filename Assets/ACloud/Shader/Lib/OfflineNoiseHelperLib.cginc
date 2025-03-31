//Note that these are methods I compiled a long time ago for generating offline noise maps.
//Therefore, I can no longer remember the sources of some of these methods.
//Please let me know if you are aware of their sources.
#ifndef OFFLINENOISEHELPERLIB_INCLUDE
#define OFFLINENOISEHELPERLIB_INCLUDE

StructuredBuffer<int> _EW_Buffer_PerlinNoise3DParams;

//----Common Funs----
//glsl mod
float mod(float x, float y)
{
	return x - y * floor(x / y);
}

float2 mod(float2 x, float2 y)
{
	return x - y * floor(x / y);
}

float3 mod(float3 x, float3 y)
{
	return x - y * floor(x / y);
}

//ref: The Real-time Volumetric Cloudscapes of Horizon: Zero Dawn
float remap(float value, float omin, float omax, float nmin, float nmax)
{
	return nmin + (((value - omin) / (omax - omin)) * (nmax - nmin));
}

//----Hash & Noise----

float hashF1F1C001(float p)
{
	return frac(sin(p + 1.951) * 43758.5453123);
}

float3 hashF3F3C001(float3 p)
{
	p = float3(dot(p, float3(127.1, 311.7, 74.7)),
		dot(p, float3(269.5, 183.3, 246.1)),
		dot(p, float3(113.5, 271.9, 124.6)));
	return -1.0 + 2.0 * frac(sin(p) * 43758.5453123);
}

//ref: https://www.shadertoy.com/view/4djSRW
float3 hashF3F3C002(float3 p)
{
	p = frac(p * float3(0.1031, 0.1030, 0.0973));
	p += dot(p, p.yxz + 33.33);
	return frac((p.xxy + p.yxx) * p.zyx);
}

//ref: https://www.shadertoy.com/view/Xt3cDn
uint baseHashU3U1Nimitz(uint3 p)
{
	p = 1103515245U * ((p.xyz >> 1U) ^ (p.yzx));
	uint h32 = 1103515245U * ((p.x ^ p.z) ^ (p.y >> 3U));
	return h32 ^ (h32 >> 16);
}

float hashU3F1Nimitz(uint3 x)
{
	uint n = baseHashU3U1Nimitz(x);
	return float(n) * (1.0 / float(0xffffffffU));
}

float2 hashF3F2Nimitz(float3 x)
{
	uint n = baseHashU3U1Nimitz(x);
	uint2 rz = uint2(n, n * 48271U); //see: http://random.mat.sbg.ac.at/results/karl/server/node4.html
	return float2(rz.xy & (uint2)0x7fffffffU) / float(0x7fffffff);
}

float3 hashU3F3Nimitz(uint3 x)
{
	uint n = baseHashU3U1Nimitz(x);
	uint3 rz = uint3(n, n * 16807U, n * 48271U); //see: http://random.mat.sbg.ac.at/results/karl/server/node4.html
	return float3(rz & (uint3)0x7fffffffU) / float(0x7fffffff);
}

//ref: https://www.shadertoy.com/view/XsX3zB
float3 hashF3F3Nikat(float3 c)
{
	float j = 4096 * sin(dot(c, float3(17, 59, 15)));
	float3 r;
	r.z = frac(512 * j);
	j *= 0.125;
	r.x = frac(512 * j);
	j *= 0.125;
	r.y = frac(512 * j);
	return r - 0.5;//values in [-0.5, 0.5]^3
}

float NoiseSimplexF3F1Nikat(float3 p)
{
	float3 s = floor(p + dot(p, 1 / 3.0));
	float3 x = p - s + dot(s, 1 / 6.0);

	float3 e = step(0, x - x.yzx);
	float3 i1 = e * (1 - e.zxy);
	float3 i2 = 1 - e.zxy * (1 - e);

	float3 x1 = x - i1 + 1 / 6.0;
	float3 x2 = x - i2 + 1 / 3.0;
	float3 x3 = x - 0.5;

	float4 w, d;
	w.x = dot(x, x);
	w.y = dot(x1, x1);
	w.z = dot(x2, x2);
	w.w = dot(x3, x3);

	w = max(0.6 - w, 0);

	d.x = dot(hashF3F3Nikat(s), x);
	d.y = dot(hashF3F3Nikat(s + i1), x1);
	d.z = dot(hashF3F3Nikat(s + i2), x2);
	d.w = dot(hashF3F3Nikat(s + 1), x3);

	w *= w;
	w *= w;
	d *= w;

	return dot(d, 52);
}

//ref: https://www.shadertoy.com/view/Xsl3Dl
//ref: https://www.shadertoy.com/view/4dffRH
float NoiseGradientF3F1Iq(float3 p)
{
	float3 i = floor(p);
	float3 f = frac(p);

	float3 u = f * f * f * (f * (f * 6 - 15) + 10);	//quintic
	//float3 u = f * f * (3 - 2 * f);//cubic

	return lerp(lerp(lerp(dot(hashF3F3C001(i), hashF3F3C001(f)),
		dot(hashF3F3C001(i + float3(1, 0, 0)), hashF3F3C001(f - float3(1, 0, 0))), u.x),
		lerp(dot(hashF3F3C001(i + float3(0, 1, 0)), hashF3F3C001(f - float3(0, 1, 0))),
			dot(hashF3F3C001(i + float3(1, 1, 0)), hashF3F3C001(f - float3(1, 1, 0))), u.x), u.y),
		lerp(lerp(dot(hashF3F3C001(i + float3(0, 0, 1)), hashF3F3C001(f - float3(0, 0, 1))),
			dot(hashF3F3C001(i + float3(1, 0, 1)), hashF3F3C001(f - float3(1, 0, 1))), u.x),
			lerp(dot(hashF3F3C001(i + float3(0, 1, 1)), hashF3F3C001(f - float3(0, 1, 1))),
				dot(hashF3F3C001(i + float3(1, 1, 1)), hashF3F3C001(f - float3(1, 1, 1))), u.x), u.y), u.z);
}

float4 NoiseGradientDerivF3F1Iq(float3 p)
{
	float3 i = floor(p);
	float3 f = frac(p);
	//quintic
	float3 u = f * f * f * (f * (f * 6 - 15) + 10);
	float3 du = 30 * f * f * (f * (f - 2) + 1);
	//cubic
	//float3 u = f * f * (3 - 2 * f);
	//float3 du = 6 * f * (1 - f);

	float3 ga = hashF3F3C001(i);
	float3 gb = hashF3F3C001(i + float3(1, 0, 0));
	float3 gc = hashF3F3C001(i + float3(0, 1, 0));
	float3 gd = hashF3F3C001(i + float3(1, 1, 0));
	float3 ge = hashF3F3C001(i + float3(0, 0, 1));
	float3 gf = hashF3F3C001(i + float3(1, 0, 1));
	float3 gg = hashF3F3C001(i + float3(0, 1, 1));
	float3 gh = hashF3F3C001(i + float3(1, 1, 1));

	//projections
	float va = dot(ga, f);
	float vb = dot(gb, f - float3(1, 0, 0));
	float vc = dot(gc, f - float3(0, 1, 0));
	float vd = dot(gd, f - float3(1, 1, 0));
	float ve = dot(ge, f - float3(0, 0, 1));
	float vf = dot(gf, f - float3(1, 0, 1));
	float vg = dot(gg, f - float3(0, 1, 1));
	float vh = dot(gh, f - float3(1, 1, 1));

	//x: noise zyw: derivatives
	return float4(va + u.x * (vb - va) + u.y * (vc - va) + u.z * (ve - va) + u.x * u.y * (va - vb - vc + vd) + u.y * u.z * (va - vc - ve + vg) + u.z * u.x * (va - vb - ve + vf) + (-va + vb + vc - vd + ve - vf - vg + vh) * u.x * u.y * u.z, //noise
		ga + u.x * (gb - ga) + u.y * (gc - ga) + u.z * (ge - ga) + u.x * u.y * (ga - gb - gc + gd) + u.y * u.z * (ga - gc - ge + gg) + u.z * u.x * (ga - gb - ge + gf) + (-ga + gb + gc - gd + ge - gf - gg + gh) * u.x * u.y * u.z + //derivatives
		du * (float3(vb, vc, ve) - va + u.yzx * float3(va - vb - vc + vd, va - vc - ve + vg, va - vb - ve + vf) + u.zxy * float3(va - vb - ve + vf, va - vb - vc + vd, va - vc - ve + vg) + u.yzx * u.zxy * (-va + vb + vc - vd + ve - vf - vg + vh)));
}

//ref: https://mrl.cs.nyu.edu/~perlin/noise/
float PerlinGrad(int hash, float x, float y, float z)
{
	int h = hash & 15;
	float u = h < 8 ? x : y;
	float v = h < 4 ? y : (h == 12 || h == 14 ? x : z);
	return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
}

float PerlinNoiseF3F1Origin(float3 x)
{
	//int p[] = { 151,160,137,91,90,15,
	//131,13,201,95,96,53,194,233,7,225,140,36,103,30,69,142,8,99,37,240,21,10,23,
	//190, 6,148,247,120,234,75,0,26,197,62,94,252,219,203,117,35,11,32,57,177,33,
	//88,237,149,56,87,174,20,125,136,171,168, 68,175,74,165,71,134,139,48,27,166,
	//77,146,158,231,83,111,229,122,60,211,133,230,220,105,92,41,55,46,245,40,244,
	//102,143,54, 65,25,63,161, 1,216,80,73,209,76,132,187,208, 89,18,169,200,196,
	//135,130,116,188,159,86,164,100,109,198,173,186, 3,64,52,217,226,250,124,123,
	//5,202,38,147,118,126,255,82,85,212,207,206,59,227,47,16,58,17,182,189,28,42,
	//223,183,170,213,119,248,152, 2,44,154,163, 70,221,153,101,155,167, 43,172,9,
	//129,22,39,253, 19,98,108,110,79,113,224,232,178,185, 112,104,218,246,97,228,
	//251,34,242,193,238,210,144,12,191,179,162,241, 81,51,145,235,249,14,239,107,
	//49,192,214, 31,181,199,106,157,184, 84,204,176,115,121,50,45,127, 4,150,254,
	//138,236,205,93,222,114,67,29,24,72,243,141,128,195,78,66,215,61,156,180
	//};
	//Here we use _EW_Buffer_PerlinNoise3DParams instead of p[]

	//
	int3 X = (int3)floor(x) & 255;
	x = frac(x);
	float3 u = x * x * x * (x * (x * 6 - 15) + 10);
	int A = _EW_Buffer_PerlinNoise3DParams[mod(X.x, 256)] + X.y;
	int AA = _EW_Buffer_PerlinNoise3DParams[mod(A, 256)] + X.z;
	int AB = _EW_Buffer_PerlinNoise3DParams[mod(A + 1, 256)] + X.z;
	int B = _EW_Buffer_PerlinNoise3DParams[mod(X.x + 1, 256)] + X.y;
	int BA = _EW_Buffer_PerlinNoise3DParams[mod(B, 256)] + X.z;
	int BB = _EW_Buffer_PerlinNoise3DParams[mod(B + 1, 256)] + X.z;

	return lerp(lerp(lerp(PerlinGrad(_EW_Buffer_PerlinNoise3DParams[mod(AA, 256)], x.x, x.y, x.z),
		PerlinGrad(_EW_Buffer_PerlinNoise3DParams[mod(BA, 256)], x.x - 1, x.y, x.z), u.x),
		lerp(PerlinGrad(_EW_Buffer_PerlinNoise3DParams[mod(AB, 256)], x.x, x.y - 1, x.z),
			PerlinGrad(_EW_Buffer_PerlinNoise3DParams[mod(BB, 256)], x.x - 1, x.y - 1, x.z), u.x), u.y),
		lerp(lerp(PerlinGrad(_EW_Buffer_PerlinNoise3DParams[mod(AA + 1, 256)], x.x, x.y, x.z - 1),
			PerlinGrad(_EW_Buffer_PerlinNoise3DParams[mod(BA + 1, 256)], x.x - 1, x.y, x.z - 1), u.x),
			lerp(PerlinGrad(_EW_Buffer_PerlinNoise3DParams[mod(AB + 1, 256)], x.x, x.y - 1, x.z - 1),
				PerlinGrad(_EW_Buffer_PerlinNoise3DParams[mod(BB + 1, 256)], x.x - 1, x.y - 1, x.z - 1),
				u.x), u.y), u.z);
}

//ref: https://github.com/sebh/TileableVolumeNoise/blob/master/TileableVolumeNoise.cpp
float NoiseTileableF3F1Sebh(float3 x)
{
	float3 p = floor(x);
	float3 f = frac(x);

	f = f * f * (3 - 2 * f);

	float n = p.x + p.y * 57 + 113 * p.z;
	return lerp(lerp(lerp(hashF1F1C001(n), hashF1F1C001(n + 1), f.x),
		lerp(hashF1F1C001(n + 57), hashF1F1C001(n + 58), f.x), f.y
	),
		lerp(lerp(hashF1F1C001(n + 113), hashF1F1C001(n + 114), f.x),
			lerp(hashF1F1C001(n + 170), hashF1F1C001(n + 171), f.x), f.y
		), f.z
	);
}

float CellsTileableF3F1Sebh(float3 p, float cellCount)
{
	//Worley Noise
	const float3 pCell = p * cellCount;
	float d = 1.0e10;
	for (int xo = -1; xo <= 1; xo++)
	{
		for (int yo = -1; yo <= 1; yo++)
		{
			for (int zo = -1; zo <= 1; zo++)
			{
				float3 tp = floor(pCell) + float3(xo, yo, zo);

				tp = pCell - tp - hashF3F3C002(mod(tp, cellCount));//change noise here

				//d = min(d, dot(tp, tp));
				d = min(d, length(tp));
			}
		}
	}
	return saturate(d);
}

float PerlinNoiseF3F1Sebh(float3 pIn, float frequency, int octaveCount)
{
	//fbm
	const float octaveFrenquencyFactor = 2;
	//
	float sum = 0;
	float weightSum = 0;
	float weight = 0.5;
	for (int oct = 0; oct < octaveCount; oct++)
	{
		float3 p = pIn * frequency;
		float val = PerlinNoiseF3F1Origin(p);

		sum += val * weight;
		weightSum += weight;

		weight *= weight;
		frequency *= octaveFrenquencyFactor;
	}

	float noise = (sum / weightSum) * 0.5 + 0.5;
	return saturate(noise);
}

float FbmWorley(float3 p, float c)
{
	float H = 1.15;
	float G = exp2(-H);
	float f = 1.0;
	float a = 1.0;
	float b = 0.0;
	float wn = 0.0;
	int numOctaves = 10;
	for (int i = 0; i < numOctaves; i++)
	{
		wn += a * (1 - CellsTileableF3F1Sebh(p * f, c));
		b += a;
		f *= 2.0;
		a *= G;
	}
	return (wn / b);
}

float PerlinWorleyNoise(float3 p, float4 np0, float4 np1, float4 np2)
{
	return remap(PerlinNoiseF3F1Sebh(p * np0.x + np0.yzw, np2.y, clamp((int)ceil(np2.z), 1, 32)) * np2.w + (1 - np2.w), 1 - FbmWorley(p * np1.x + np1.yzw, np2.x), 1, 0, 1);
}


#endif