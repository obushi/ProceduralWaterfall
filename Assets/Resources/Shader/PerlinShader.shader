Shader "Hidden/PerlinTexture"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
	}
	CGINCLUDE

	#include "UnityCG.cginc"
	#include "ClassicNoise2D.cginc"

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
