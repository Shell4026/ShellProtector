Shader "Unlit/test"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
		_EncryptTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" }
        LOD 100
		Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
				float4 local_pos : TEXCOORD1;
            };

            UNITY_DECLARE_TEX2D(_MainTex);
			Texture2D _EncryptTex;
            float4 _MainTex_ST;
			float4 _MainTex_TexelSize;
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				o.local_pos = mul(unity_ObjectToWorld, v.vertex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }
			
			/*#include "Assets/ShaderDebugger/debugger.cginc"
			void Debug1(float4 vert, float4 localPos, float v)
			{
				uint root = DebugFragment(vert);
				DbgSetColor(root, float4(1, 1, 1, 1));
				DbgVectorO3(root, localPos.xyz * 0.01);

				DbgChangePosByO3(root, localPos.xyz * 0.01);
				DbgValue1(root, v);      
			}
			void Debug2(float4 vert, float4 localPos, float2 v)
			{
				uint root = DebugFragment(vert);
				DbgSetColor(root, float4(1, 1, 1, 1));
				DbgVectorO3(root, localPos.xyz * 0.01);

				DbgChangePosByO3(root, localPos.xyz * 0.01);
				DbgValue2(root, v);      
			}
			void Debug3(float4 vert, float4 localPos, float3 v)
			{
				uint root = DebugFragment(vert);
				DbgSetColor(root, float4(1, 1, 1, 1));
				DbgVectorO3(root, localPos.xyz * 0.01);

				DbgChangePosByO3(root, localPos.xyz * 0.01);
				DbgValue3(root, v);      
			}*/
			
			static uint k[4] = { 808810359, 808726578, 556873266, 0 };
			void XXTEADecrypt(float4 pixel[2], out uint data[2])
			{
				const uint Delta = 0x9e3779b9;
				data[0] = ((uint)round(pixel[0].r * 255.0f) + ((uint)round(pixel[0].g * 255.0f) << 8) + ((uint)round(pixel[0].b * 255.0f) << 16) + ((uint)round(pixel[0].a * 255.0f) << 24));
				data[1] = ((uint)round(pixel[1].r * 255.0f) + ((uint)round(pixel[1].g * 255.0f) << 8) + ((uint)round(pixel[1].b * 255.0f) << 16) + ((uint)round(pixel[1].a * 255.0f) << 24));

				uint n = 2;
				uint v0, v1, sum;
				uint p, rounds, e;

				rounds = 6 + floor(52 / n);
				sum = rounds * Delta;

				v0 = data[0];
				do
				{
					e = (sum >> 2) & 3;
					for (p = n-1; p > 0; p--)
					{
						v1 = data[p - 1];
						data[p] -= (((v1 >> 5 ^ v0 << 2) + (v0 >> 3 ^ v1 << 4)) ^ ((sum ^ v0) + (k[(p & 3) ^ e] ^ v1)));
						v0 = data[p];
					}
					v1 = data[n - 1];
					data[0] -= (((v1 >> 5 ^ v0 << 2) + (v0 >> 3 ^ v1 << 4)) ^ ((sum ^ v0) + (k[(p & 3) ^ e] ^ v1)));
					v0 = data[0];
					sum -= Delta;
				} while (--rounds > 0);
			}
			
			static uint size = 256;
			float2 GetUV(int idx, int m, int woffset = 0, int hoffset = 0)
			{
				int w = idx % size;
				int h = idx / size;
				return float2((float)w/size, (float)h/size);
			}
			
			float3 GammaCorrection(float3 rgb)
			{
				float3 result = pow(rgb, 2.2);
				return result;
			}
			
            float4 frag (v2f i) : SV_Target
            {
				
				float2 uv = i.uv;
                float4 col = _MainTex.SampleLevel(sampler_MainTex, uv, 0);
				float2 uv_unit = _MainTex_TexelSize.xy;
				
				float x = uv.x;
				float y = uv.y;
				int idx = (size * floor(frac(y) * size)) + floor(frac(x) * size);
				k[3] = floor(idx / 2) * 2;
				int pos[2] = { 0, -1 };
				int offset = pos[idx % 2];
				
				float4 pixels[2];
				pixels[0] = _EncryptTex.SampleLevel(sampler_MainTex, GetUV(idx + 0 + offset, 0), 0);
				pixels[1] = _EncryptTex.SampleLevel(sampler_MainTex, GetUV(idx + 1 + offset, 0), 0);
				
				uint data[2] = { 0, 0 };
				data[0] = (round(pixels[0].r * 255.0f) + ((uint)round(pixels[0].g * 255.0f) << 8) + ((uint)round(pixels[0].b * 255.0f) << 16) + ((uint)round(pixels[0].a * 255.0f) << 24));
				data[1] = (round(pixels[1].r * 255.0f) + ((uint)round(pixels[1].g * 255.0f) << 8) + ((uint)round(pixels[1].b * 255.0f) << 16) + ((uint)round(pixels[1].a * 255.0f) << 24));
				
				XXTEADecrypt(pixels, data);
				
				uint r[2] = { (data[0] & 0x000000FF), ((data[1] & 0x000000FF)) };
				uint g[2] = { ((data[0] & 0x0000FF00) >>  8), ((data[1] & 0x0000FF00) >>  8) };
				uint b[2] = { ((data[0] & 0x00FF0000) >> 16), ((data[1] & 0x00FF0000) >> 16) };
				uint a[2] = { ((data[0] & 0xFF000000) >> 24), ((data[1] & 0xFF000000) >> 24) };
				
				uint color1 = (r[idx % 2] | g[idx % 2] << 8);
				uint color2 = (b[idx % 2] | a[idx % 2] << 8);
				
				uint color1_r = (color1 & 0xF800) >> 11;
				color1_r = color1_r << 3 | color1_r >> 2;
				uint color1_g = (color1 & 0x7E0) >> 5;
				color1_g = color1_g << 2 | color1_g >> 4;
				uint color1_b= color1 & 0x1F;
				color1_b = color1_b << 3 | color1_b >> 2;
				
				uint color2_r = (color2 & 0xF800) >> 11;
				color2_r = color2_r << 3 | color2_r >> 2;
				uint color2_g = (color2 & 0x7E0) >> 5;
				color2_g = color2_g << 2 | color2_g >> 4;
				uint color2_b= color2 & 0x1F;
				color2_b = color2_b << 3 | color2_b >> 2;
				
				float4 col1 = float4(color1_r / 255.0f, color1_g / 255.0f, color1_b / 255.0f, 1);
				float4 col2 = float4(color2_r / 255.0f, color2_g / 255.0f, color2_b / 255.0f, 1);
				
				float4 result;
				if(color1 > color2)
					result = lerp(col2, col1, col);
				else
					result = lerp(col2, col1, 0.5);
				Debug3(i.vertex, i.local_pos, col);
                return float4(GammaCorrection(result), col.a);
            }
            ENDCG
        }
    }
}
