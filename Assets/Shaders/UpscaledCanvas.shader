// 3DPixelCamera의 UpscaledCanvas용 언릿 셰이더.
// 게임 카메라가 그린 저해상도 RT(_LowResTexture, 포인트 필터)를 그대로 표시한다.
Shader "BeatSlash/UpscaledCanvas"
{
    Properties
    {
        _LowResTexture ("Low Res Texture", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off
        ZWrite On

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _LowResTexture;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return tex2D(_LowResTexture, i.uv);
            }
            ENDCG
        }
    }
}
