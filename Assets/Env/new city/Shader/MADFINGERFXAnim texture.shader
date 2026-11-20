Shader "MADFINGER/FX/Anim texture" {
	Properties {
		_MainTex ("Base (RGB)", 2D) = "white" {}
		_NumTexTiles ("Num tex tiles", Vector) = (4,4,0,0)
		_ReplaySpeed ("Replay speed - FPS", Float) = 4
		_Color ("Color", Vector) = (1,1,1,1)
	}
	SubShader {
		Tags { "IgnoreProjector"="True" "Queue"="Transparent" "RenderType"="Transparent" }
		Blend One One
		ZWrite Off
		Cull Off

		Pass {
			CGPROGRAM
			#pragma target 3.0
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			sampler2D _MainTex;
			float4 _NumTexTiles;
			float _ReplaySpeed;
			float4 _Color;

			struct appdata {
				float4 vertex : POSITION;
				float4 color : COLOR;
				float2 uv : TEXCOORD0;
			};

			struct v2f {
				float4 pos : SV_POSITION;
				float4 color : COLOR;
				float2 uvCurrent : TEXCOORD0;
				float2 uvNext : TEXCOORD1;
			};

			v2f vert (appdata v) {
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);

				float time = v.color.a * 60.0 + _Time.y;
				float frame = floor(time * _ReplaySpeed);
				float2 tiles = _NumTexTiles.xy;

				float2 tileCurrent = float2(frame, floor(frame / tiles.x));
				float2 tileNext = float2(frame + 1.0, floor((frame + 1.0) / tiles.x));

				o.uvCurrent = (v.uv + frac(tileCurrent / tiles) * tiles) / tiles;
				o.uvNext = (v.uv + frac(tileNext / tiles) * tiles) / tiles;
				o.color = float4(v.color.rgb * _Color.rgb, frac(time * _ReplaySpeed));
				return o;
			}

			fixed4 frag (v2f i) : SV_Target {
				fixed4 current = tex2D(_MainTex, i.uvCurrent);
				fixed4 next = tex2D(_MainTex, i.uvNext);
				return lerp(current, next, i.color.a) * i.color;
			}
			ENDCG
		}
	}
}
