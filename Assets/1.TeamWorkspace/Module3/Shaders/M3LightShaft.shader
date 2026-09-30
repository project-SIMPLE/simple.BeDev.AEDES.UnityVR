// Additive, unlit, for the glow and the sun beams M3LightShafts lays at the openings of the Module 3
// houses. Its own shader rather than a runtime URP/Lit or Unlit material set to Transparent: those
// rely on shader_feature variants that a Quest build strips when no asset in the build uses them, and
// then draw opaque or not at all. Here the blend is fixed in the pass, so there is nothing to strip.
// Output = gradient (R of _BaseMap) x vertex alpha x vertex colour x _Color; alpha is ignored by the
// One One blend. No fog: an additive surface should not add haze, and nothing this draws is far away.
Shader "AEDES/Module 3/Light Shaft"
{
    Properties
    {
        [NoScaleOffset] _BaseMap ("Gradient (R)", 2D) = "white" {}
        [HDR] _Color ("Colour", Color) = (1, 0.9, 0.75, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        LOD 100

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half gradient = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).r;
                half3 light = _Color.rgb * (_Color.a * gradient * input.color.a) * input.color.rgb;
                return half4(light, 0.0h);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
