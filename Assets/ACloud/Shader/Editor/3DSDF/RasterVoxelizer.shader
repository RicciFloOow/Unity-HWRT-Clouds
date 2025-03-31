//保守光栅化(DX11.3开始有的特性, 因此unity可能需要切DX12才能用)
//ref: https://docs.unity3d.com/2022.3/Documentation/Manual/SL-Conservative.html
//ref: https://learn.microsoft.com/en-us/windows/win32/direct3d11/conservative-rasterization
//ref: https://learn.microsoft.com/en-us/windows/win32/direct3d12/conservative-rasterization
Shader "ACloud/Editor/RasterVoxelizer"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            Conservative True//必须启用保守光栅化
            Cull Off ZWrite Off ZTest Always
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }

            uint frag (v2f i) : SV_Target
            {
                return 8;
            }
            ENDCG
        }
    }
}
