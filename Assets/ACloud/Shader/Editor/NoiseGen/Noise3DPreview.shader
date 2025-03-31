Shader "ACloud/Editor/Noise3DPreview"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            Cull Off ZWrite Off ZTest Always
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local METHOD_WORLEY METHOD_PERLINWORLEY

            #include "UnityCG.cginc"
            #include "../../Lib/OfflineNoiseHelperLib.cginc"
            #include "../../Lib/UtilLib.cginc"

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
                return o;
            }

            float _NoiseVolumeDepth;
            float4 _NoiseParameter0;
            float4 _NoiseParameter1;
            float4 _NoiseParameter2;

            fixed4 frag(v2f i) : SV_Target
            {
                float n = 0;
#if METHOD_WORLEY
                n = FbmWorley(float3(i.uv, _NoiseVolumeDepth) * _NoiseParameter0.x + _NoiseParameter0.yzw, _NoiseParameter1.x);
#elif METHOD_PERLINWORLEY
                n = PerlinWorleyNoise(float3(i.uv, _NoiseVolumeDepth), _NoiseParameter0, _NoiseParameter1, _NoiseParameter2);
#else

#endif
                return n;
            }
            ENDCG
        }
    }
}
