Shader "Custom/SkinningShader"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
	}

	SubShader
	{
		LOD 100

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"

			uniform float4x4 _Matrices[24];

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				float4 tangent : TANGENT; //los índices de los huesos y sus respectivos weights
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			
			v2f vert (appdata v)
			{
				v2f o;

				float4 pos = mul(_Matrices[v.tangent.x], v.vertex) * v.tangent.y + mul(_Matrices[v.tangent.z], v.vertex) * v.tangent.w;
				//multiplica a la posición del vértice por las transformaciones de las matrices, y luego por el peso de cada hueso

				o.vertex = UnityObjectToClipPos(pos); //transforma el espacio local en espacio de clip y usando la cámara de la escena
				o.uv = TRANSFORM_TEX(v.uv, _MainTex); //
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				fixed4 col = tex2D(_MainTex, i.uv);
				return col;
			}
			ENDCG
		}
	}
}
