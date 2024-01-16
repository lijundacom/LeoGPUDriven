Shader "Custom/SplineMeshSurfShader" {
	Properties{
		_Color("Color", Color) = (1,1,1,1)
		_MainTex("Albedo (RGB)", 2D) = "white" {}
		_Glossiness("Smoothness", Range(0,1)) = 0.5
		_Metallic("Metallic", Range(0,1)) = 0.0
			//_StartPos("StartPos",Vector) = (0, 0, 0, 1)
			//_StartTangent("StartTangent",Vector) = (0, 1, 0, 0)
			//_StartRoll("StartRoll",float) = 0.0
			//_EndPos("EndPos",Vector) = (0, 0, 0, 1)
			//_EndTangent("EndTangent",Vector) = (0, 1, 0, 0)
			//_EndRoll("EndRoll",float) = 0.0

			//_SplineUpDir("SplineUpDir",Vector) = (0, 1, 0, 0)
			//_SplineMeshMinZ("SplineMeshMinZ",float) = 0.0
			//_SplineMeshScaleZ("SplineMeshScaleZ",float) = 0.0

			//_SplineMeshDir("SplineMeshDir",Vector) = (0,0,1,0)
			//_SplineMeshX("SplineMeshX",Vector) = (1,0,0,0)
			//_SplineMeshY("SplineMeshY",Vector) = (0,1,0,0)
	}
		SubShader{
			Tags { "RenderType" = "Opaque" }
			LOD 200

			CGPROGRAM
			// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
			#pragma exclude_renderers gles
			// Physically based Standard lighting model, and enable shadows on all light types
			#pragma surface surf Standard fullforwardshadows vertex:vert
			// Use shader model 3.0 target, to get nicer looking lighting
			#pragma target 3.0

			sampler2D _MainTex;

			float3 _StartPos;
			float3 _StartTangent;
			float _StartRoll;
			float3 _EndPos;
			float3 _EndTangent;
			float _EndRoll;

			float3 _SplineUpDir;
			float _SplineMeshMinZ;
			float _SplineMeshScaleZ;

			float3 _SplineMeshDir;
			float3 _SplineMeshX;
			float3 _SplineMeshY;

			struct Input {
				float2 uv_MainTex;
			};

			half _Glossiness;
			half _Metallic;
			fixed4 _Color;

			float3 SplineEvalPos(float3 StartPos, float3 StartTangent, float3 EndPos, float3 EndTangent, float A)
			{
				float A2 = A  * A;
				float A3 = A2 * A;

				return (((2 * A3) - (3 * A2) + 1) * StartPos) + ((A3 - (2 * A2) + A) * StartTangent) + ((A3 - A2) * EndTangent) + (((-2 * A3) + (3 * A2)) * EndPos);
			}

			float3 SplineEvalDir(float3 StartPos, float3 StartTangent, float3 EndPos, float3 EndTangent, float A)
			{
				float3 C = (6 * StartPos) + (3 * StartTangent) + (3 * EndTangent) - (6 * EndPos);
				float3 D = (-6 * StartPos) - (4 * StartTangent) - (2 * EndTangent) + (6 * EndPos);
				float3 E = StartTangent;

				float A2 = A  * A;

				return normalize((C * A2) + (D * A) + E);
			}

			float4x3 calcSliceTransform(float YPos)
			{
				float t = YPos * _SplineMeshScaleZ - _SplineMeshMinZ;
				float smoothT = smoothstep(0, 1, t);

				//frenet理论
				//当前位置的顶点与方向根据起点与终点的设置插值
				float3 SplinePos = SplineEvalPos(_StartPos, _StartTangent, _EndPos, _EndTangent, t);
				float3 SplineDir = SplineEvalDir(_StartPos, _StartTangent, _EndPos, _EndTangent, t);

				//根据SplineDir与当前_SplineUpDir 计算当前坐标系(过程类似视图坐标系的建立)
				float3 BaseXVec = normalize(cross(_SplineUpDir, SplineDir));
				float3 BaseYVec = normalize(cross(SplineDir, BaseXVec));

				// Apply roll to frame around spline
				float UseRoll = lerp(_StartRoll, _EndRoll, smoothT);
				float SinAng, CosAng;
				sincos(UseRoll, SinAng, CosAng);
				float3 XVec = (CosAng * BaseXVec) - (SinAng * BaseYVec);
				float3 YVec = (CosAng * BaseYVec) + (SinAng * BaseXVec);
				
				//mul(transpose(A),B), A为正交矩阵，A由三轴组成的行向量矩阵，放左边需要转置成列向量				
				//简单来看，_SplineMeshDir为x轴{1,0,0},则下面的不转换，x轴={0,0,0},y轴=XYec,z轴=YVec
				//_SplineMeshDir为y轴{0,1,0},则x轴=YVec,y轴={0,0,0},z轴=XYec
				//_SplineMeshDir为z轴{0,0,1},则x轴=XYec,y轴=YVec，z轴={0,0,0}
				float3x3 SliceTransform3 = mul(transpose(float3x3(_SplineMeshDir, _SplineMeshX, _SplineMeshY)),
					float3x3(float3(0, 0, 0), XVec, YVec));
				//SliceTransform是一个行向量组成的矩阵
				float4x3 SliceTransform = float4x3(SliceTransform3[0], SliceTransform3[1], SliceTransform3[2], SplinePos);
				return SliceTransform;
			}

			void vert(inout appdata_full v)
			{
				////如下顶点位置偏移右上前1
				//float4x4 mx = float4x4(float4(1, 0, 0, 0), float4(0, 1, 0, 0), float4(0, 0, 1, 0), float4(1, 1, 1, 1));
				////矩阵左，向量右，向量与矩阵为列向量。
				//v.vertex = mul(transpose(mx), v.vertex);
				////向量左，矩阵右，则向量与矩阵为行向量。
				//v.vertex = mul(v.vertex, mx);

				////向量左，矩阵右,([1*N])*([N*X])，向量与矩阵为行向量。
				//float4x3 mx4x3 = float4x3(float3(1, 0, 0), float3(0, 1, 0), float3(0, 0, 1),float3(1,1,1));
				//v.vertex = float4(mul(v.vertex,mx4x3),v.vertex.w);
				////矩阵左与向量右,([X*N])*([N*1]) mx3x4 = transpose(mx4x3)，表面看矩阵无意义，实际是mx4x3的列向量
				//float3x4 mx3x4 = float3x4(float4(1, 0, 0, 1), float4(0, 1, 0, 1), float4(0, 0, 1, 1));
				//v.vertex = float4(mx3x4, v.vertex), v.vertex.w);
				////这种错误，mx4x3是由行向量组成，必需放左边才有意义
				//v.vertex = mul(mx4x3, v.vertex.xyz);
				
				float t = dot(v.vertex.xyz, _SplineMeshDir);
				float4x3 SliceTransform = calcSliceTransform(t);
				v.vertex = float4(mul(v.vertex,SliceTransform),v.vertex.w);
			}

			void surf(Input IN, inout SurfaceOutputStandard o) {
				// Albedo comes from a texture tinted by color
				fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
				o.Albedo = c.rgb;
				// Metallic and smoothness come from slider variables
				o.Metallic = _Metallic;
				o.Smoothness = _Glossiness;
				o.Alpha = c.a;
			}
			ENDCG
		}
			FallBack "Diffuse"
}
