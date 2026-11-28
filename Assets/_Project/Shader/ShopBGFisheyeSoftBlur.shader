Shader "Clickergochi/UI Shop Fisheye Soft Blur"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _ScrollSpeed ("Scroll Speed (Right, Down)", Vector) = (0.012, 0.009, 0, 0)
        _TileScale ("Tile Scale (Zoom)", Range(0.25, 4)) = 1
        _AspectScale ("Aspect Scale (Fit)", Vector) = (1, 1, 0, 0)
        _UVOffset ("UV Offset", Vector) = (0, 0, 0, 0)
        _SeamOffset ("Seam Offset (UV)", Vector) = (0, 0, 0, 0)
        _DistortStrength ("Fisheye Strength", Range(0, 0.5)) = 0.28
        _DistortRadius ("Fisheye Radius", Range(0.05, 0.6)) = 0.34
        _LensDrift ("Lens Drift", Range(0, 0.25)) = 0.12
        _BlurAmount ("Soft Blur", Range(0, 0.01)) = 0.0028
        _PatternRotation ("Pattern Rotation (Z)", Range(-25, 25)) = 0

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
            Name "ShopBG_Fisheye_SoftBlur"

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
            float _TileScale;
            float4 _AspectScale;
            float4 _UVOffset;
            float4 _SeamOffset;
            float _DistortStrength;
            float _DistortRadius;
            float _LensDrift;
            float _BlurAmount;
            float _PatternRotation;

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
            float2 ApplyFisheye(float2 uv, float2 center, float radius, float strength)
            {
                float2 delta = uv - center;
                float dist = length(delta);
                float falloff = 1.0 - smoothstep(0.0, radius, dist);
                return uv - delta * (strength * falloff);
            }

            // Desfoque leve (5 taps) para esconder a diferenca de 1 pixel
            // entre as bordas do padrao de quadrados quando ele repete.
            fixed4 SoftSample(float2 uv)
            {
                fixed4 sum = tex2D(_MainTex, uv) * 4.0;
                sum += tex2D(_MainTex, uv + float2(_BlurAmount, 0.0)) * 2.0;
                sum += tex2D(_MainTex, uv - float2(_BlurAmount, 0.0)) * 2.0;
                sum += tex2D(_MainTex, uv + float2(0.0, _BlurAmount)) * 2.0;
                sum += tex2D(_MainTex, uv - float2(0.0, _BlurAmount)) * 2.0;
                return sum / 12.0 + _TextureSampleAdd;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y;
                float drift = _LensDrift;
                float2 uv = i.uv;

                // Quatro ilhas de distorcao flutuando devagar.
                float2 lensA = float2(
                    0.20 + drift * sin(t * 0.13),
                    0.28 + drift * cos(t * 0.11)
                );
                float2 lensB = float2(
                    0.74 + drift * cos(t * 0.09),
                    0.60 + drift * sin(t * 0.15)
                );
                float2 lensC = float2(
                    0.46 + drift * sin(t * 0.07 + 2.1),
                    0.84 + drift * cos(t * 0.12 + 1.3)
                );
                float2 lensD = float2(
                    0.10 + drift * cos(t * 0.10 + 0.7),
                    0.72 + drift * sin(t * 0.08 + 2.4)
                );

                uv = ApplyFisheye(uv, lensA, _DistortRadius, _DistortStrength);
                uv = ApplyFisheye(uv, lensB, _DistortRadius * 0.85, _DistortStrength * 0.90);
                uv = ApplyFisheye(uv, lensC, _DistortRadius * 1.15, _DistortStrength * 0.75);
                uv = ApplyFisheye(uv, lensD, _DistortRadius * 0.70, _DistortStrength * 0.60);

                // Inclina o padrao em Z sem girar a direcao do scroll: assim
                // as colunas deslizam visivelmente para baixo. Como a UV nao
                // e limitada, o Wrap Mode Repeat cobre as bordas inclinadas.
                float rad = _PatternRotation * 0.01745329252;
                float cr = cos(rad);
                float sr = sin(rad);
                float2 centered = uv - 0.5;
                float2 rotated = float2(
                    cr * centered.x + sr * centered.y,
                    -sr * centered.x + cr * centered.y
                ) + 0.5;

                // NAO usar frac() nem remapear a UV: o salto 1 -> 0 quebra as
                // derivadas (ddx/ddy) e o hardware escolhe um mip grosso na
                // emenda,criando uma linha visivel. Repassar a UV contínua
                // deixa o Repeat fazer o tile com derivadas corretas.
                float2 framed = (rotated - 0.5) * _TileScale + 0.5 + _UVOffset.xy;
                float2 scrolled = framed - float2(_ScrollSpeed.x, -_ScrollSpeed.y) * t;

                // Enquadramento: o painel pode ter proporcao diferente da textura
                // (ex.: WaffleBG3 e quadrada num painel retrato). Aplicar a escala
                // so na amostragem preserva as ilhas de lente e a rotacao no
                // espaco visual 0..1, enquanto o Repeat completa o tile extra.
                // Sem frac(): a UV continua mantem as derivadas da emenda.
                float2 fitted = scrolled * _AspectScale.xy;

                // Move apenas a posicao onde o Repeat troca de chapa.
                // Nao escala a UV: o padrao mantem o tamanho e a proporcao.
                fitted += _SeamOffset.xy;

                fixed4 color = SoftSample(fitted) * i.color;

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
