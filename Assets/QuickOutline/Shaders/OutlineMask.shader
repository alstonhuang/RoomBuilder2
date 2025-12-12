Shader "Hidden/QuickOutline/OutlineMask" {
    SubShader {
        Tags { "RenderType"="Opaque" }
        Pass {
            Name "OutlineMask"
            Cull Off
            ZWrite Off
            ZTest LEqual
            ColorMask 0
        }
    }
}
