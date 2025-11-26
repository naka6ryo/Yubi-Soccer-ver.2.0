Shader "UI/CircleHole" {
    Properties {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        _Radius ("Radius", Range(0,1)) = 0
        _Feather ("Feather", Range(0,0.5)) = 0.02
    }
    SubShader {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        Lighting Off
        ZWrite Off

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            fixed4 _Color;
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Radius;
            float _Feather;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Compute screen-space pixel coordinates so the hole is a true circle regardless of aspect
                float2 screenPx = float2(i.uv.x * _ScreenParams.x, i.uv.y * _ScreenParams.y);
                float2 centerPx = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
                float distPx = length(screenPx - centerPx);
                float maxDist = length(centerPx);
                float normDist = distPx / maxDist;

                // smoothstep produces 0 inside radius, 1 outside (hole in center)
                float alphaMask = smoothstep(_Radius - _Feather, _Radius + _Feather, normDist);

                fixed4 tex = tex2D(_MainTex, i.uv);
                fixed4 baseCol = tex * _Color;
                baseCol.a = baseCol.a * alphaMask;
                return baseCol;
            }
            ENDCG
        }
    }
}
