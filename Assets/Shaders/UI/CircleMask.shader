Shader "UI/CircleMask" {
    Properties {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        _Radius ("Radius", Range(0,2)) = 0
        _Feather ("Feather", Range(0,0.5)) = 0.005
        _Hardness ("Hardness", Range(0.1,20)) = 12
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
            float _Hardness;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Compute screen-space pixel coordinates so the mask is a true circle regardless of aspect
                float2 screenPx = float2(i.uv.x * _ScreenParams.x, i.uv.y * _ScreenParams.y);
                float2 centerPx = float2(_ScreenParams.x * 0.5, _ScreenParams.y * 0.5);
                float distPx = length(screenPx - centerPx);
                // maximum distance from center to corner in pixels
                float maxDist = length(centerPx);
                // normalized distance 0..1 (0=center, 1=corner)
                float normDist = distPx / maxDist;

                // radius is interpreted in normalized units (0..1) where 1 reaches corners
                float alphaMask = 1.0 - smoothstep(_Radius - _Feather, _Radius + _Feather, normDist);
                // sharpen the edge by applying a hardness exponent (>1 sharpens)
                alphaMask = pow(max(clamp(alphaMask, 0.0, 1.0), 0.0001), _Hardness);

                fixed4 tex = tex2D(_MainTex, i.uv);
                fixed4 baseCol = tex * _Color;
                baseCol.a = baseCol.a * alphaMask;
                return baseCol;
            }
            ENDCG
        }
    }
}
