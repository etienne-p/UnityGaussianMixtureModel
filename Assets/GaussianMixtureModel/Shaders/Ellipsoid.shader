﻿Shader "Custom/GaussianMixtureModel/Ellipsoid"
{
    SubShader
    {
        Pass
        {
            ZWrite Off 
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

            HLSLPROGRAM
                #pragma target 4.5
                #pragma vertex Vertex
                #pragma fragment Fragment
                #include "Ellipsoid.hlsl"
            ENDHLSL
        }
    }
}