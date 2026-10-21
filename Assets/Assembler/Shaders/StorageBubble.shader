Shader "Assembler/StorageBubble"
{
    Properties { _Color ("Tint", Color) = (0.7,0.9,1,0.4) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos:SV_POSITION; float3 normal:TEXCOORD0; float3 view:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            fixed4 _Color;
            v2f vert(appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_OUTPUT(v2f,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos=UnityObjectToClipPos(v.vertex);
                o.normal=UnityObjectToWorldNormal(v.normal);
                o.view=UnityWorldSpaceViewDir(mul(unity_ObjectToWorld,v.vertex).xyz);
                return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float rim=pow(1-saturate(dot(normalize(i.normal),normalize(i.view))),2.6);
                float highlight=pow(saturate(dot(normalize(i.normal),normalize(float3(-.5,.8,-.5)))),28);
                return fixed4(lerp(_Color.rgb,float3(1,1,1),rim*.65+highlight*.3),.035+rim*_Color.a+highlight*.25);
            }
            ENDCG
        }
    }
}
