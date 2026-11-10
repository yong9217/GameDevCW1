Shader "BaseCandidates/Neutral Stone Preview"
{
    Properties{_MainTex("Original palette",2D)="white"{} [HideInInspector] _GlossaryUnlit("Glossary unlit (set by the glossary only)",Float)=0 _PaletteMode("0 mixed, 1 stone, 2 wood",Float)=0}
    SubShader
    {
        Tags{"RenderType"="Opaque"}
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        sampler2D _MainTex; float _GlossaryUnlit;
        half _PaletteMode;
        struct Input{float2 uv_MainTex;};
        void surf(Input IN,inout SurfaceOutputStandard o)
        {
            fixed3 c=tex2D(_MainTex,IN.uv_MainTex).rgb;
            half high=max(c.r,max(c.g,c.b)),low=min(c.r,min(c.g,c.b));
            half accent=smoothstep(.10,.30,high-low);
            if(_PaletteMode>.5)accent=_PaletteMode>1.5?1:0;
            half value=dot(c,half3(.2126,.7152,.0722));
            fixed3 stone=fixed3(.65,.61,.51),wood=fixed3(.40,.26,.14);
            o.Albedo=lerp(stone,wood,accent)*lerp(.68,1.07,smoothstep(.08,.85,value));
            o.Metallic=0;o.Smoothness=.03;o.Alpha=1;
            // Glossary portraits: flat, unlit colour (no light, no shadow). 0 in the game = unchanged.
            o.Emission+=o.Albedo*_GlossaryUnlit; o.Albedo*=1-_GlossaryUnlit; o.Smoothness*=1-_GlossaryUnlit;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
