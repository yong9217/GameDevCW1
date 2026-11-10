Shader "BaseDefense/Crossbow Simple Timber"
{
    Properties
    {
        _MainTex("Original texture",2D)="white"{} [HideInInspector] _GlossaryUnlit("Glossary unlit (set by the glossary only)",Float)=0
        _PaletteStrength("Steel palette simplification",Range(0,1))=.76
        _TextureDetail("Steel tonal detail",Range(0,1))=.37
        _MipBias("Steel fine detail suppression",Range(0,4))=.65
        _MetalColor("Matte steel",Color)=(.24,.29,.33,1)
        _WoodColor("Simple timber",Color)=(.38,.26,.15,1)
        _GoldColor("Muted brass",Color)=(.48,.38,.22,1)
        _CrystalColor("Unused shared palette color",Color)=(.06,.38,.58,1)
        _CrystalMode("Unused shared palette flag",Float)=0
        _WoodSimplification("Timber simplification",Range(0,1))=.98
        _WoodMipBias("Timber fine detail suppression",Range(0,6))=4.5
    }
    SubShader
    {
        Tags{"RenderType"="Opaque"}
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _MainTex; float _GlossaryUnlit;
        half _PaletteStrength,_TextureDetail,_MipBias,_WoodSimplification,_WoodMipBias;
        fixed4 _MetalColor,_WoodColor,_GoldColor;
        struct Input{float2 uv_MainTex;};
        void surf(Input IN,inout SurfaceOutputStandard o)
        {
            fixed3 source=tex2Dbias(_MainTex,float4(IN.uv_MainTex,0,_MipBias)).rgb;
            half lum=dot(source,half3(.2126,.7152,.0722));
            half warm=smoothstep(.025,.12,source.r-source.b);
            half brass=smoothstep(.22,.52,lum)*warm;
            fixed3 palette=lerp(_MetalColor.rgb,_WoodColor.rgb,warm);
            palette=lerp(palette,_GoldColor.rgb,brass*.7);
            half detail=lerp(1,lerp(.55,1.22,smoothstep(.025,.7,lum)),_TextureDetail);
            half cavity=lerp(.48,1,smoothstep(.012,.10,lum));
            fixed3 steelResult=lerp(source,palette*detail*cavity,_PaletteStrength);
            // Only warm timber regions are flattened. Steel still uses the approved V7 treatment.
            fixed3 broad=tex2Dbias(_MainTex,float4(IN.uv_MainTex,0,_WoodMipBias)).rgb;
            half timberMask=smoothstep(.015,.075,broad.r-broad.b)*smoothstep(1.08,1.30,broad.r/(broad.b+.01));
            half broadLum=dot(broad,half3(.2126,.7152,.0722));
            half timberTone=lerp(.91,1.04,smoothstep(.06,.42,broadLum));
            fixed3 simpleWood=_WoodColor.rgb*timberTone;
            o.Albedo=lerp(steelResult,simpleWood,timberMask*_WoodSimplification);
            o.Metallic=0;o.Smoothness=.06;o.Alpha=1;
            // Glossary portraits: flat, unlit colour (no light, no shadow). 0 in the game = unchanged.
            o.Emission+=o.Albedo*_GlossaryUnlit; o.Albedo*=1-_GlossaryUnlit; o.Smoothness*=1-_GlossaryUnlit;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
