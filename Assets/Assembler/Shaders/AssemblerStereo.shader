Shader "Assembler/StereoLit"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma target 3.0
        // Generate base, additional-light and shadow passes, including vertex
        // lights used for less-important torches. No emission or brightness floor.
        #pragma surface surf Lambert fullforwardshadows addshadow
        #pragma multi_compile_instancing
        #pragma multi_compile __ STEREO_INSTANCING_ON UNITY_SINGLE_PASS_STEREO
        sampler2D _MainTex;
        fixed4 _Color;
        struct Input { float2 uv_MainTex; };
        void surf(Input input, inout SurfaceOutput output)
        {
            fixed4 albedo = tex2D(_MainTex, input.uv_MainTex) * _Color;
            output.Albedo = albedo.rgb;
            output.Alpha = 1;
        }
        ENDCG
    }
    Fallback Off
}