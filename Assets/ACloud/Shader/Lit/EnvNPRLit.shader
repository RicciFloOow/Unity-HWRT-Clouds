Shader "ACloud/Lit/EnvNPRLit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Smoothness ("Smoothness", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "../Lib/CustomLitLib.cginc"

            sampler2D _MainTex;
            float _Smoothness;

            LitOutput frag (V2F i)
            {
                i.normal = normalize(i.normal);
                i.viewDir = normalize(i.viewDir);
                //
                half4 texCol = tex2D(_MainTex, i.uv);
                float NDotV = abs(dot(i.normal, i.viewDir)) * 0.5 + 0.5;
                //
                texCol.xyz *= NDotV;
                //
                LitOutput o;
                o.color = texCol;
                o.normal = float4(i.normal, _Smoothness);
                return o;
            }
            ENDCG
        }
    }
}
