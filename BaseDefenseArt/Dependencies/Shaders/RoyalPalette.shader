Shader "DnoPreview/RoyalPalette"
{
    Properties { _MainTex ("Original diffuse", 2D) = "white" {} _RoyalPalette ("Royal palette", Range(0,1)) = 1 }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _MainTex_ST; float _RoyalPalette;
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
            v2f vert(appdata_base v) { v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=TRANSFORM_TEX(v.texcoord,_MainTex); return o; }
            fixed4 frag(v2f i):SV_Target
            {
                fixed4 c=tex2D(_MainTex,i.uv); float3 royal=c.rgb;
                // Preserve the source texture. Remap cool blue armor and cyan trim in the material.
                if(c.b>c.r*1.3 && c.g>c.r*1.2 && c.b-c.r>.1) {
                    float l=dot(c.rgb,float3(.2126,.7152,.0722));
                    if(c.g>c.r*1.65 && c.g>.4) royal=float3(.78,.56,.18)*saturate(l*1.35);
                    else royal=float3(.86,.85,.80)*saturate(l*1.75+.12);
                }
                return fixed4(lerp(c.rgb,royal,_RoyalPalette),c.a);
            }
            ENDCG
        }
    }
}
