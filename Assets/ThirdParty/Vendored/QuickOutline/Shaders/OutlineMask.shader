Shader "Hidden/QuickOutline/OutlineMask" {
    Properties {
        _ZTest ("ZTest", Float) = 4
    }
    SubShader {
        Tags { "RenderType"="Opaque" }
        Pass {
            Name "OutlineMask"
            Cull Off
            ZWrite Off
            ZTest [_ZTest]
            ColorMask 0
            Stencil {
                Ref 1
                Comp Always
                Pass Replace
            }
        }
    }
}
