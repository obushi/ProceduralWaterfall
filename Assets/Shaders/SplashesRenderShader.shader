Shader "Waterfall/SplashesRender"
{
CGINCLUDE
#include "UnityCG.cginc"

	struct Splash
	{
		uint id;
		float dropSize;
		float3 position;
		float3 velocity;
	};

	struct v2g
	{
		float3 position : TEXCOORD0;
		float4 color : COLOR;
	};

	struct g2f
	{
		float4 position : POSITION;
		float2 texcoord : TEXCOORD0;
		float4 color : COLOR;
	};

	StructuredBuffer<Splash> _SplashesBuff;
	sampler2D _SplashTexture;
	float4 _SplashTexture_ST;
	float _SplashSize;

	float4x4 _InvViewMatrix;

	static const float3 g_positions[4] =
	{
		float3(-1, 1, 0),
		float3(1, 1, 0),
		float3(-1,-1, 0),
		float3(1,-1, 0),
	};

	static const float2 g_texcoords[4] =
	{
		float2(0, 0),
		float2(1, 0),
		float2(0, 1),
		float2(1, 1),
	};

	v2g vert(uint id : SV_VertexID)
	{
		v2g o;
		o.position = _SplashesBuff[id].position;
		o.color = float4(0.2, 0.23, 0.23, 0.2);
		return o;
	}

	[maxvertexcount(4)]
	void geom(point v2g In[1], inout TriangleStream<g2f> SpriteStream)
	{
		g2f o;
		[unroll]
		for (int i = 0; i < 4; i++)
		{
			float3 position = g_positions[i] * _SplashSize;
			position = mul(_InvViewMatrix, position) + In[0].position;
			o.position = mul(UNITY_MATRIX_MVP, float4(position, 1.0));

			o.color = In[0].color;
			o.texcoord = g_texcoords[i];

			SpriteStream.Append(o);
		}

		SpriteStream.RestartStrip();
	}

	fixed4 frag(g2f i) : SV_Target
	{
		return tex2D(_SplashTexture, i.texcoord.xy) * i.color;
	}
		ENDCG

	SubShader
	{
		Tags{ "RenderType" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" }
			LOD 100

			Zwrite Off
			Blend One One
			Cull Off

			Pass
		{
			CGPROGRAM
			#pragma target 5.0
			#pragma vertex vert
			#pragma geometry geom
			#pragma fragment frag
			ENDCG
		}
	}
}
