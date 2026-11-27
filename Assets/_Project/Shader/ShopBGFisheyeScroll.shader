Shader "Clickergochi/UI Shop Fisheye Scroll"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _ScrollSpeed ("Scroll Speed (Right, Down)", Vector) = (0.012, 0.009, 0, 0)
        _DistortStrength ("Fisheye Strength", Range(0, 0.35)) = 0.12
        _DistortRadius ("Fisheye Radius", Range(0.05, 0.6)) = 0.24
        _LensDrift ("Lens Drift", Range(0, 0.25)) = 0.08
        _SeamOverlap ("Seam Overlap", Range(0, 0.02)) = 0.0035

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
            Name "ShopBG_Fisheye_Scroll"

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

            float4 _ScrollSpeed;
            float _DistortStrength;
            float _DistortRadius;
            float _LensDrift;
            float _SeamOverlap;

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

            // Lente local: magnifica o centro e volta ao normal na borda.
            // Varias lentes pequenas dao o efeito "olho de peixe em partes".
            float2 ApplyFisheye(float2 uv, float2 center, float radius, float strength)
            {
                float2 delta = uv - center;
                float dist = length(delta);
                float falloff = 1.0 - smoothstep(0.0, radius, dist);
                return uv - delta * (strength * falloff);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y;
                float drift = _LensDrift;
                float2 uv = i.uv;

                // Ilhas de distorcao que tambem flutuam devagar.
                float2 lensA = float2(
                    0.22 + drift * sin(t * 0.13),
                    0.30 + drift * cos(t * 0.11)
                );
                float2 lensB = float2(
                    0.72 + drift * cos(t * 0.09),
                    0.62 + drift * sin(t * 0.15)
                );
                float2 lensC = float2(
                    0.48 + drift * sin(t * 0.07 + 2.1),
                    0.82 + drift * cos(t * 0.12 + 1.3)
                );

                uv = ApplyFisheye(uv, lensA, _DistortRadius, _DistortStrength);
                uv = ApplyFisheye(uv, lensB, _DistortRadius * 0.85, _DistortStrength * 0.85);
                uv = ApplyFisheye(uv, lensC, _DistortRadius * 1.15, _DistortStrength * 0.7);

                // _ScrollSpeed.x = direita; _ScrollSpeed.y = baixo.
                // Subtrair X faz a textura andar para a direita; somar Y
                // (via -_ScrollSpeed.y) faz andar para baixo no espaco UI.
                float2 scrolled = uv - float2(_ScrollSpeed.x, -_ScrollSpeed.y) * t;
                scrolled = frac(scrolled);

                // Cada copia amostra um pouquinho menos da borda. Como o BG e
                // seamless, a copia seguinte cobre essa faixa e a linha some.
                float2 joined = lerp(
                    float2(_SeamOverlap, _SeamOverlap),
                    float2(1.0 - _SeamOverlap, 1.0 - _SeamOverlap),
                    scrolled
                );

                fixed4 color = (tex2D(_MainTex, joined) + _TextureSampleAdd) * i.color;

                #ifdef UNITY_UI_CLIP_RECT
                    color.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
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
