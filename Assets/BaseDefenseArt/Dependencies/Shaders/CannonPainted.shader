Shader "BaseDefense/Cannon Painted Steel and Timber"
{
    Properties
    {
        _MainTex ("Original cannon texture",2D)="white"{} [HideInInspector] _GlossaryUnlit("Glossary unlit (set by the glossary only)",Float)=0
        _Part ("0 barrel, 1 carriage",Float)=0
        _Steel ("Blue steel",Color)=(.20,.27,.32,1)
        _Brass ("Warm brass fittings",Color)=(.53,.34,.13,1)
        _Paint ("Muted red paint",Color)=(.33,.065,.055,1)
        _Timber ("Warm timber",Color)=(.33,.18,.075,1)
        _Detail ("Retained painted detail",Range(0,1))=.55
    }
    SubShader
    {
        Tags {"RenderType"="Opaque"}
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0
        sampler2D _MainTex; float _GlossaryUnlit;
        half _Part,_Detail;
        fixed4 _Steel,_Brass,_Paint,_Timber;
        struct Input {float2 uv_MainTex;float3 modelPos;};
        void vert(inout appdata_full v,out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input,o);
            o.modelPos=v.vertex.xyz;
        }
        float band(float value,float low,float high)
        {
            float feather=max(fwidth(value),.0015);
            return smoothstep(low-feather,low+feather,value)*(1-smoothstep(high-feather,high+feather,value));
        }
        void surf(Input IN,inout SurfaceOutputStandard o)
        {
            float3 p=IN.modelPos;
            fixed3 source=tex2D(_MainTex,IN.uv_MainTex).rgb;
            half lum=dot(source,half3(.2126,.7152,.0722));
            half fittings=0,paint=0;
            fixed3 palette;
            if(_Part<.5)
            {
                // Positions correspond to the existing raised bands in Cannon1_Turret.
                fittings=max(band(p.z,-.145,.205),max(band(p.z,1.20,1.34),band(p.z,-.80,-.72)));
                paint=band(p.z,.39,.56);
                palette=lerp(_Steel.rgb,_Paint.rgb,paint);
                palette=lerp(palette,_Brass.rgb,fittings);
                // Keep the bore visibly hollow, including the inner muzzle wall.
                half bore=(1-smoothstep(.232,.255,length(p.xy)))*smoothstep(.90,.98,p.z);
                palette=lerp(palette,fixed3(.025,.033,.038),bore);
                fittings*=1-bore;
            }
            else
            {
                // Timber supports, painted middle panels, brass feet and pivot caps.
                float side=smoothstep(.37,.42,abs(p.x));
                paint=band(p.y,.47,.78)*side;
                float pivot=band(p.y,.88,1.08)*smoothstep(.54,.57,abs(p.x));
                fittings=max(band(p.y,.105,.25),pivot);
                palette=lerp(_Timber.rgb,_Paint.rgb,paint*.85);
                palette=lerp(palette,_Steel.rgb,1-smoothstep(.08,.12,p.y));
                palette=lerp(palette,_Brass.rgb,fittings);
            }
            half textureTone=lerp(.62,1.28,smoothstep(.015,.6,lum));
            half cavities=lerp(.46,1,smoothstep(.008,.065,lum));
            fixed3 result=palette*lerp(1,textureTone,_Detail)*cavities;
            // A small source contribution retains rivets, seams and wear without glossy black bands.
            o.Albedo=lerp(result,source,.10);
            o.Metallic=fittings*.18;
            o.Smoothness=lerp(.08,.22,fittings);
            o.Alpha=1;
            // Glossary portraits: flat, unlit colour (no light, no shadow). 0 in the game = unchanged.
            o.Emission+=o.Albedo*_GlossaryUnlit; o.Albedo*=1-_GlossaryUnlit; o.Smoothness*=1-_GlossaryUnlit;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
