Shader "MADFINGER/Environment/Scroll 2 Layers Aditive - additive blend" {
	Properties {
		_MainTex ("Base layer (RGB)", 2D) = "white" {}
		_DetailTex ("2nd layer (RGB)", 2D) = "white" {}
		_ScrollX ("Base layer Scroll speed X", Float) = 1
		_ScrollY ("Base layer Scroll speed Y", Float) = 0
		_Scroll2X ("2nd layer Scroll speed X", Float) = 1
		_Scroll2Y ("2nd layer Scroll speed Y", Float) = 0
		_AMultiplier ("Layer Multiplier", Float) = 0.5
		_TintColor ("Tint color", Vector) = (1,1,1,1)
	}
	SubShader {
		LOD 100
		Tags { "Queue"="Transparent" "RenderType"="Transparent" }
		Blend One One
		ZWrite Off

		Pass {
			Fog { Mode Off }

			CGPROGRAM
			#pragma target 3.0
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			sampler2D _MainTex;
			float4 _MainTex_ST;
			sampler2D _DetailTex;
			float4 _DetailTex_ST;
			float _ScrollX;
			float _ScrollY;
			float _Scroll2X;
			float _Scroll2Y;
			float _AMultiplier;
			float4 _TintColor;

			struct appdata {
				float4 vertex : POSITION;
				float4 color : COLOR;
				float2 uv : TEXCOORD0;
			};

			struct v2f {
				float4 pos : SV_POSITION;
				float4 color : COLOR;
				float2 uvBase : TEXCOORD0;
				float2 uvDetail : TEXCOORD1;
			};

			v2f vert (appdata v) {
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uvBase = TRANSFORM_TEX(v.uv, _MainTex) + frac(_Time.xy * float2(_ScrollX, _ScrollY));
				o.uvDetail = TRANSFORM_TEX(v.uv, _DetailTex) + frac(_Time.xy * float2(_Scroll2X, _Scroll2Y));

				float4 tint = v.color * _TintColor;
				tint *= _TintColor.w;
				tint.rgb *= _AMultiplier;
				o.color = tint;
				return o;
			}

			fixed4 frag (v2f i) : SV_Target {
				return (tex2D(_MainTex, i.uvBase) + tex2D(_DetailTex, i.uvDetail)) * i.color;
			}
			ENDCG
		}
	}
}
