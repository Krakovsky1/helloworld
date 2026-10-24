// Built-in RP fallback: double-sided Standard-like shader (only used when URP is not installed)
Shader "HotelParadise/StandardDoubleSided"
{
    Properties { _Color ("Color", Color) = (1,1,1,1) _MainTex ("Albedo", 2D) = "white" {} _Glossiness ("Smoothness", Range(0,1)) = 0.12 _Metallic ("Metallic", Range(0,1)) = 0 _EmissionColor ("Emission", Color) = (0,0,0,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        sampler2D _MainTex; fixed4 _Color; half _Glossiness; half _Metallic; fixed4 _EmissionColor;
        struct Input { float2 uv_MainTex; fixed facing : VFACE; };
        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb; o.Metallic = _Metallic; o.Smoothness = _Glossiness; o.Emission = _EmissionColor.rgb;
            o.Normal = IN.facing > 0 ? float3(0,0,1) : float3(0,0,-1);
        }
        ENDCG
    }
    FallBack "Diffuse"
}
