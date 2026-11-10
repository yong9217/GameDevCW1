Shader "BaseDefense/Soft Palette Towers"
{
    Properties
    {
        _MainTex ("Original texture (preserved)", 2D) = "white" {} [HideInInspector] _GlossaryUnlit("Glossary unlit (set by the glossary only)",Float)=0
        _PaletteStrength ("Palette simplification", Range(0,1)) = 0.92
        _TextureDetail ("Original tonal detail", Range(0,1)) = 0.22
        _MipBias ("Fine detail suppression", Range(0,4)) = 1.5
        _MetalColor ("Matte steel", Color) = (0.46,0.51,0.55,1)
        _WoodColor ("Muted wood", Color) = (0.54,0.39,0.23,1)
        _GoldColor ("Muted brass", Color) = (0.66,0.54,0.31,1)
        _CrystalColor ("Crystal blue", Color) = (0.12,0.58,0.78,1)
        _CrystalMode ("Crystal material", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _MainTex; float _GlossaryUnlit;
        half _PaletteStrength, _TextureDetail, _MipBias, _CrystalMode;
        fixed4 _MetalColor, _WoodColor, _GoldColor, _CrystalColor;
        struct Input { float2 uv_MainTex; };
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed3 source = tex2Dbias(_MainTex, float4(IN.uv_MainTex,0,_MipBias)).rgb;
            half luminance = dot(source, half3(0.2126,0.7152,0.0722));
            // Original UVs identify material regions; their painted highlights are compressed.
            half warm = smoothstep(0.025,0.12, source.r-source.b);
            half brass = smoothstep(0.22,0.52,luminance) * warm;
            half blue = smoothstep(0.025,0.14,source.b-source.r) * _CrystalMode;
            fixed3 palette = lerp(_MetalColor.rgb,_WoodColor.rgb,warm);
            palette = lerp(palette,_GoldColor.rgb,brass*0.7);
            palette = lerp(palette,_CrystalColor.rgb,blue);
            half detail = lerp(1.0,lerp(0.55,1.22,smoothstep(0.025,0.7,luminance)),_TextureDetail);
            // Preserve the darkest creases and muzzle interior without reproducing bright rims.
            half cavity = lerp(0.48,1.0,smoothstep(0.012,0.10,luminance));
            fixed3 simplified = palette * detail * cavity;
            o.Albedo = lerp(source,simplified,_PaletteStrength);
            o.Metallic = 0;
            o.Smoothness = 0.06;
            o.Emission = simplified * blue * 0.06;
            o.Alpha = 1;
            // Glossary portraits: flat, unlit colour (no light, no shadow). 0 in the game = unchanged.
            o.Emission+=o.Albedo*_GlossaryUnlit; o.Albedo*=1-_GlossaryUnlit; o.Smoothness*=1-_GlossaryUnlit;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
