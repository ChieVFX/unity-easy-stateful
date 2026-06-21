// Hologram materialize / dematerialize for uGUI. Reveals the sprite bottom→top
// with a glowing edge, scanlines and flicker. Driven by _Progress (0 = gone,
// 1 = fully materialized) — a HologramController script tweens that value, so
// this also demonstrates animating a CUSTOM script value through StatefulRoot.
Shader "EasyStateful/UIHologram"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Hologram Tint", Color) = (0.22,0.82,1,1)
        _EdgeColor ("Edge Glow", Color) = (0.7,0.95,1,1)
        _Progress ("Progress", Range(0,1)) = 1
        _ScanCount ("Scanline Count", Float) = 90
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

            fixed4 _Color, _EdgeColor, _TextureSampleAdd; float4 _ClipRect;
            sampler2D _MainTex; float4 _MainTex_ST;
            float _Progress, _ScanCount, _AnimTime;

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
                float shape = tex2D(_MainTex, uv).a;

                float edge = 0.14;
                float d = _Progress - uv.y;              // >0 = below the reveal line (materialized)
                float body = saturate(d / edge);          // solid fill behind the line
                float glow = saturate(1.0 - abs(d) / edge); // bright band at the line ('line' is a reserved HLSL keyword)

                float scan = 0.55 + 0.45 * sin(uv.y * _ScanCount - _AnimTime * 5.0);
                float flicker = 0.9 + 0.1 * sin(_AnimTime * 38.0);

                fixed3 rgb = IN.color.rgb * scan * flicker + _EdgeColor.rgb * glow;
                float born = smoothstep(0.0, 0.03, _Progress); // fully gone at _Progress = 0
                float a = shape * saturate(max(body, glow)) * IN.color.a * born;

                fixed4 col = fixed4(rgb, a);

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
