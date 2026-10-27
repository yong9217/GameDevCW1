Shader "Base03/Stone and wood atlas"
{
 Properties
 {
  _MainTex("Original stone and wood atlas",2D)="white"{}
  _Color("Tint",Color)=(1,1,1,1)
  _StoneRegion("Stone UV rectangle",Vector)=(.009765625,.033203125,.126953125,.1171875)
  _WoodRegion("Wood UV rectangle",Vector)=(.334,.431,.09,.12)
  _StoneTileSize("Stone repeat size",Float)=1.3
  _WoodTileSize("Wood repeat size",Float)=1.0
 }
 SubShader
 {
  Tags{"RenderType"="Opaque"}
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows
  #pragma target 3.0
  sampler2D _MainTex;fixed4 _Color;float4 _StoneRegion,_WoodRegion;float _StoneTileSize,_WoodTileSize;
  struct Input{float3 worldPos;float3 worldNormal;};
  void surf(Input IN,inout SurfaceOutputStandard o)
  {
   float3 n=abs(normalize(IN.worldNormal));
   float2 uv=n.x>n.z?IN.worldPos.zy:IN.worldPos.xy;
   if(n.y>max(n.x,n.z))uv=IN.worldPos.xz;
   float2 stoneUV=_StoneRegion.xy+(frac(uv.yx/max(.01,_StoneTileSize))*.98+.01)*_StoneRegion.zw;
   float2 woodUV=_WoodRegion.xy+(frac(IN.worldPos.xz/max(.01,_WoodTileSize))*.98+.01)*_WoodRegion.zw;
   float roof=(n.y>.18&&n.y<.93)?1:0;
   fixed3 stone=tex2D(_MainTex,stoneUV).rgb;
   fixed3 wood=tex2D(_MainTex,woodUV).rgb;
   o.Albedo=lerp(stone,wood,roof)*_Color.rgb;o.Metallic=0;o.Smoothness=.08;o.Alpha=1;
  }
  ENDCG
 }
 Fallback "Diffuse"
}
