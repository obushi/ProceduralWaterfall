Shader "Waterfall/Render"
{
	CGINCLUDE
	#include "UnityCG.cginc"

	struct StreamLine
	{
		int id;
		float3 birthPosition;
		float3 deathPosition;
		float3 position;
		float3 initVelocity;
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

	StructuredBuffer<StreamLine> _ParticlesBuffer;
	sampler2D _DropTexture;
	float4 _DropTexture_ST;
	float _DropSize;
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
		v2g o = (v2g)0;
		o.position = _ParticlesBuffer[id].position;
		o.color = float4(0.3, 0.3, 0.3, 0.1);
		return o;
	}

	[maxvertexcount(4)]
	void geom(point v2g In[1], inout TriangleStream<g2f> SpriteStream)
	{
		g2f o = (g2f)0;
		[unroll]
		for (int i = 0; i < 4; i++)
		{
			float3 position = g_positions[i] * _DropSize;
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
		return tex2D(_DropTexture, i.texcoord.xy) * i.color;
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
