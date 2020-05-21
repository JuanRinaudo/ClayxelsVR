// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'


Shader "Clayxels/ClayxelBuiltInShader"
{
	SubShader
	{
		ZWrite Off // shadow pass, normal oriented billboards         

		Tags { "Queue" = "Geometry" "RenderType"="Opaque" }

		CGPROGRAM
		
		#pragma surface surf Standard vertex:vert addshadow fullforwardshadows
		#pragma target 3.0

		#include "UnityCG.cginc"

		#define SHADERPASS_SHADOWCASTER

		#if defined (SHADER_API_D3D11) || defined(SHADER_API_METAL)
			#include "../clayxelSRPUtils.cginc"
		#endif

		float _Smoothness;
		float _Metallic;
		sampler2D _MainTex;

		struct VertexData{
			float4 vertex : POSITION;
			float3 normal : NORMAL;
			float4 tangent : TANGENT;
			float4 color : COLOR;
			float4 texcoord1 : TEXCOORD1;
			float4 texcoord2 : TEXCOORD2;
			uint vid : SV_VertexID;
		};

		struct Input
		{
			float2 tex : TEXCOORD0;
			float4 color : COLOR;
		};

		void vert(inout VertexData outVertex, out Input outData){
			UNITY_INITIALIZE_OUTPUT(Input, outData);

			#if defined (SHADER_API_D3D11) || defined(SHADER_API_METAL)
				float4 tex;
				
				clayxelVert(outVertex.vid, tex, outVertex.color.xyz, outVertex.vertex.xyz, outVertex.normal);

				outVertex.color.w = 1.0;
				outVertex.vertex.w = 1.0;
				outData.tex = tex.xy;
			#endif
		}

		void surf(Input IN, inout SurfaceOutputStandard o){
			if(length(IN.tex-0.5) > 0.5){// if outside circle
				discard;
			}

			o.Albedo = IN.color * 0.5;
		}

		ENDCG

		ZWrite On // splatting pass, no shadows
		Cull Back

		CGPROGRAM

		#pragma multi_compile SPLATTEXTURE_ON SPLATTEXTURE_OFF
		#pragma surface surf Standard vertex:vert 
		#pragma target 3.0

		#include "UnityCG.cginc"

		#if defined (SHADER_API_D3D11) || defined(SHADER_API_METAL)
			#include "../clayxelSRPUtils.cginc"
		#endif

		float _Smoothness;
		float _Metallic;
		float4 _Emission;
		float _EmissionIntensity;
		sampler2D _MainTex;

		struct VertexData{
			float4 vertex : POSITION;
			float3 normal : NORMAL;
			float4 tangent : TANGENT;
			float4 color : COLOR;
			float4 texcoord1 : TEXCOORD1;
			float4 texcoord2 : TEXCOORD2;
			uint vid : SV_VertexID;
		};

		struct Input
		{
			float2 tex : TEXCOORD0;
			float4 color : COLOR;
		};

		void vert(inout VertexData outVertex, out Input outData){
			UNITY_INITIALIZE_OUTPUT(Input, outData);

			#if defined (SHADER_API_D3D11) || defined(SHADER_API_METAL)
				float4 tex;
				
				clayxelVert(outVertex.vid, tex, outVertex.color.xyz, outVertex.vertex.xyz, outVertex.normal);

				outVertex.color.w = 1.0;
				outVertex.vertex.w = 1.0;
				outData.tex = tex.xy;
				
			#endif
		}

		void surf(Input IN, inout SurfaceOutputStandard o)
		{ 
			#if defined (SHADER_API_D3D11) || defined(SHADER_API_METAL)
				#if SPLATTEXTURE_ON
					float alphaTexture = tex2D(_MainTex, IN.tex).a;
					clayxelFrag(IN.color, float4(IN.tex, 0.0, 0.0), alphaTexture, o.Alpha);
					
					if(o.Alpha < 0.95){
						discard;
					}
				#else
					if(length(IN.tex-0.5) > 0.5){// if outside circle
						discard;
					}
				#endif
			#endif

			o.Albedo = IN.color;
			o.Metallic = _Metallic;
			o.Smoothness = _Smoothness;
			o.Emission = _Emission * _EmissionIntensity;
		}

		ENDCG
	}
}