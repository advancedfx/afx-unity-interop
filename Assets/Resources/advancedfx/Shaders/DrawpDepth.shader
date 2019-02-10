Shader "Unlit/DrawpDepth"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100

		Pass
		{
			ZWrite On
			ZTest Always
			ColorMask 0
		
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
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
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				return o;
			}
			
			float frag (v2f i) : SV_Depth
			{
				float4 col = tex2D(_MainTex, i.uv);
			
				// https://docs.unity3d.com/Manual/UpgradeGuide55.html
				// x = -1+far/near
				// y = 1
				// z = x/far = -1/far + 1/near
				// w = 1/far
				
				float g_Afx_zNear = 1.0/((_ZBufferParams.x + 1)*_ZBufferParams.w) * 100.0 / 2.54;
				float g_Afx_zFar = (1.0/_ZBufferParams.w) * 100 / 2.54;
				
				float f1 = (-1) * g_Afx_zFar * g_Afx_zNear * 1.0;
				float xD = g_Afx_zFar - g_Afx_zNear;
				
				float depth = (255 * 1.0/16777215.0)*col.r +(255 * 256.0/16777215.0)*col.g +(255 * 65536.0/16777215.0)*col.b;
				
				// decode to linear in inch:
				depth = f1/(depth * xD -g_Afx_zFar);
				
				// to meters (Unity):
				depth = depth * 2.54 / 100.0;
				
				// to Unity zBuffer (inverse of LinearEyeDepth):
				depth = (1.0/depth - _ZBufferParams.w) / _ZBufferParams.z;
				
				return depth;
			}
			ENDCG
		}
	}
}
