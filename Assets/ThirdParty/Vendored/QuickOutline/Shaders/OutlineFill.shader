Shader "Hidden/QuickOutline/OutlineFill" {
    Properties {
        _OutlineWidth ("Outline Width", Float) = 2.0
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _ZTest ("ZTest", Float) = 4
    }
    SubShader {
        Tags { "RenderType"="Opaque" }
        Cull Front
        ZWrite Off
        ZTest [_ZTest]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass {
            Name "OutlineFill"
            Stencil {
                Ref 1
                Comp NotEqual
                Pass Keep
            }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            uniform float _OutlineWidth;
            uniform float4 _OutlineColor;

            struct appdata {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f {
                float4 pos : SV_POSITION;
            };

            v2f vert (appdata v) {
                v2f o;
                float3 norm = normalize(UnityObjectToWorldNormal(v.normal));
                float3 pos = mul(unity_ObjectToWorld, v.vertex).xyz;
                pos += norm * _OutlineWidth * 0.01; // 0.01 to keep width in reasonable units
                o.pos = UnityObjectToClipPos(float4(pos,1));
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                return _OutlineColor;
            }
            ENDCG
        }
    }
}
