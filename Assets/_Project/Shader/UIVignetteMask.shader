Shader "UI/VignetteMask"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _FadeStart ("Fade Start", Range(0, 0.5)) = 0.30
        _FadeEnd ("Fade End", Range(0, 0.5)) = 0.50
        _VignetteStrength ("Vignette Strength", Range(0, 1)) = 1

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)]
        _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Vignette"

            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #pragma target 2.0

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;

            float _FadeStart;
            float _FadeEnd;
            float _VignetteStrength;

            float4 _ClipRect;

            v2f vert(appdata v)
            {
                v2f o;

                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                o.color = v.color * _Color;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 color = (tex2D(_MainTex, i.uv) + _TextureSampleAdd) * i.color;

                // Distância do pixel em relação ao centro.
                float2 centeredUV = abs(i.uv - float2(0.5, 0.5));

                // Identifica a distância até as bordas.
                float edgeDistance = max(centeredUV.x, centeredUV.y);

                // Cria a máscara de transparência.
                float edgeMask = 1.0 - smoothstep(
                    _FadeStart,
                    _FadeEnd,
                    edgeDistance
                );

                // Controla a intensidade da vinheta.
                float vignette = lerp(
                    1.0,
                    edgeMask,
                    _VignetteStrength
                );

                color.a *= vignette;

                #ifdef UNITY_UI_CLIP_RECT
                    color.a *= UnityGet2DClipping(
                        i.worldPosition.xy,
                        _ClipRect
                    );
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                    clip(color.a - 0.001);
                #endif

                return color;
            }

            ENDCG
        }
    }
}