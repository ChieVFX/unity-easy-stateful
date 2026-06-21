// Animated "aurora" gradient for uGUI. Drop on an Image's material.
// Vertex color (Image.color) tints/fades the whole effect, so it still
// plays nicely with StatefulRoot color states.
Shader "EasyStateful/UIAurora"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _ColorA ("Color A", Color) = (0.05,0.07,0.11,1)
        _ColorB ("Color B", Color) = (0.10,0.24,0.48,1)
        _ColorC ("Color C", Color) = (0.36,0.16,0.52,1)
        _Speed ("Speed", Float) = 0.5
        _Scale ("Scale", Float) = 3.0
        _AnimTime ("Anim Time", Float) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t { float4 vertex:POSITION; float4 color:COLOR; float2 texcoord:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 texcoord:TEXCOORD0; float4 worldPosition:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

            fixed4 _Color, _TextureSampleAdd; float4 _ClipRect;
            sampler2D _MainTex; float4 _MainTex_ST;
            fixed4 _ColorA, _ColorB, _ColorC; float _Speed, _Scale, _AnimTime;

            v2f vert(appdata_t v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float t = _AnimTime * _Speed;
                float w1 = sin(uv.x * _Scale + t) * 0.5 + 0.5;
                float w2 = sin(uv.y * _Scale * 1.3 - t * 1.2 + uv.x * 2.0) * 0.5 + 0.5;
                float w3 = sin((uv.x + uv.y) * _Scale * 0.8 + t * 0.7) * 0.5 + 0.5;

                fixed4 col = lerp(_ColorA, _ColorB, w1);
                col = lerp(col, _ColorC, w2 * 0.6);
                col = lerp(col, _ColorB, w3 * 0.3);
                col.a = 1;
                col *= IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif
                return col;
            }
            ENDCG
        }
    }
}
