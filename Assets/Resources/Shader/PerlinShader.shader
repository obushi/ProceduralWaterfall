Shader "Hidden/PerlinTexture"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
	}
	CGINCLUDE

	#include "UnityCG.cginc"
	#include "ClassicNoise2D.cginc"

	//sampler2D _MainTex;
	//float4 _MainTex_ST;

	//struct appdata
	//{
	//	float4 vertex : POSITION;
	//	float2 uv : TEXCOORD0;
	//};

	//struct v2f
	//{
	//	float2 uv : TEXCOORD0;
	//	float4 vertex : SV_POSITION;
	//};

	//v2f vert(appdata v)
	//{
	//	v2f o;
	//	o.vertex = float4((float3)(cnoise(i.uv * 8.0) + 1.0), 1.0);
	//	o.uv = v.uv;
	//	return o;
	//}

	float fbm(float2 st)
	{
		float val = 0.0;
		float amp = 0.5;
		float freq = 0.0;

		for (int i = 0; i < 4; i++)
		{
			val += amp * (cnoise(st) * 0.5 + 0.5);
			st *= 2.0;
			amp *= 0.5;
		}
		return val;
	}

	float4 frag(v2f_img i) : SV_Target
	{
		float3 color = (float3)fbm(i.uv * 10 + float2(0, _Time.y));
		return float4(color, 1.0);
	}
	ENDCG

	SubShader
	{
	Pass
	{
		CGPROGRAM
		#pragma target 5.0
		#pragma vertex vert_img
		#pragma fragment frag
		ENDCG
	}
	}
}
