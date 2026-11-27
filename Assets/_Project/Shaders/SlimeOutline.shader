Shader "Clickergochi/Slime Outline"
{
    Properties
    {
        _OutlineColor("Outline Color", Color) = (1, 1, 1, 1)
        _OutlineWidth("Outline Width", Range(0, 100)) = 50
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry+1"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Outline"

            // Desenha depois do objeto (Geometry+1). O stencil ja foi marcado
            // por SlimeOutlineMask em TODAS as pecas do slime, entao so a
            // silhueta conjunta (fora do stencil) recebe contorno.
            Cull Front
            ZWrite Off
            ZTest LEqual
            ColorMask RGBA

            Stencil
            {
                Ref 1
                Comp NotEqual
                Pass Keep
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                // Expansao em unidades locais. 50 = 0.05 antes da escala do
                // slime, enough para aparecer sem engolir o boneco.
                float3 normal = any(input.normalOS) ? normalize(input.normalOS) : float3(0, 0, 0);
                float3 expanded = input.positionOS.xyz + normal * (_OutlineWidth * 0.001);

                output.positionCS = TransformObjectToHClip(expanded);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return _OutlineColor;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
