#ifndef CUSTOMLITLIB_INCLUDE
#define CUSTOMLITLIB_INCLUDE

#include "UnityCG.cginc"

struct VertInput
{
	float4 vertex : POSITION;
	float2 uv : TEXCOORD0;
	float3 normal : NORMAL;
	float4 tangent : TANGENT;
};

struct V2F
{
	float4 vertex : SV_POSITION;
	float2 uv : TEXCOORD0;
	float3 normal : NORMAL;
	float4 tangent : TANGENT;
	float3 viewDir : TEXCOORD1;
};

struct LitOutput
{
	half4 color : SV_Target0;
	float4 normal : SV_Target1;
};

V2F vert(VertInput v)
{
	float4 wPos = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1));
	V2F o;
	o.vertex = mul(UNITY_MATRIX_VP, wPos);
	o.uv = v.uv;
	o.normal = UnityObjectToWorldNormal(v.normal);
	o.viewDir = _WorldSpaceCameraPos - wPos.xyz;
	o.tangent = float4(UnityObjectToWorldDir(v.tangent.xyz), v.tangent.w);
	return o;
}

#endif