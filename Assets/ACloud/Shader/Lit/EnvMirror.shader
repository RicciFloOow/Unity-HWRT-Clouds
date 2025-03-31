Shader "ACloud/Lit/EnvMirror"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            CGPROGRAM
            #pragma vertex mVert
            #pragma fragment frag

            #include "../Lib/CustomLitLib.cginc"

            struct v2f
            {
	            float4 vertex : SV_POSITION;
	            float2 uv : TEXCOORD0;
	            float3 normal : NORMAL;
	            float4 tangent : TANGENT;
	            float3 viewDir : TEXCOORD1;
	            float4 sspos : TEXCOORD2;
            };

            v2f mVert(VertInput v)
            {
                float4 wPos = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1));
                v2f o;
                o.vertex = mul(UNITY_MATRIX_VP, wPos);
                o.uv = v.uv;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = _WorldSpaceCameraPos - wPos.xyz;
                o.tangent = float4(UnityObjectToWorldDir(v.tangent.xyz), v.tangent.w);
                o.sspos = o.vertex;//TODO:这里可以用前一帧的VP矩阵，这样采样的更安全点
                return o;
            }

            Texture2D _NormalTex;
            Texture2D _CloudColorDepth_Tex;
            SamplerState sampler_LinearClamp;

            LitOutput frag (v2f i)
            {
                float2 ssuv = i.sspos.xy / i.sspos.w;
                ssuv.y = 1 - ssuv.y;
                //
                float3 normal = normalize(i.normal);
                float4 tangent = float4(normalize(i.tangent.xyz), i.tangent.w);
                float3 binormal = cross(normal, tangent.xyz) * tangent.w;
                float4 bumpnormal = _NormalTex.Sample(sampler_LinearClamp, i.uv);
                float3 packednormal = UnpackNormal(bumpnormal);
                normal = normalize(tangent.xyz * packednormal.x + binormal * packednormal.y + normal * packednormal.z);
                LitOutput o;
                o.color = _CloudColorDepth_Tex.Sample(sampler_LinearClamp, ssuv);
                o.normal = float4(normal, 1);
                return o;
            }
            ENDCG
        }
    }
}
