Shader"GPUDriven/RenderTerrain"
{
    Properties
    {
        _MainTex ("Color Texture", 2D) = "white" {}
        _HeightMap ("HeightMap", 2D) = "white" {}
        _RenderPatchMap("_RenderPatchMap",2D) = "white"{}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        ZWrite On
        ZTest LEqual
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog



            #include "UnityCG.cginc"
            #include "../ComputeShader/DataStructDefine.cginc"
            #include "../ComputeShader/TerrainFuncUtil.cginc"
            #include "./RenderUtils.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _HeightMap;

            sampler2D _RenderPatchMap;
            uniform float4 globalValueList[10];

            Buffer<float> debugBuff;

            

            v2f vert(appdata v)    
            {
                v2f o;
                uint y = v.instanceID * 2 / 512;
                uint x = v.instanceID * 2 - y * 512;
                float2 uv0 = (1.0 / 512) * (uint2(x, y) + 0.5);
                float2 uv1 = (1.0 / 512) * (uint2(x + 1, y) + 0.5);
                float4 pix0 = tex2Dlod(_RenderPatchMap, float4(uv0, 0, 0));
                float4 pix1 = tex2Dlod(_RenderPatchMap, float4(uv1, 0, 0));
                GlobalValue gValue = GetGlobalValue(globalValueList);
                float3 vexWorldPos = CalTerrainVexPos(gValue, v.vertex, pix0, pix1);
                float2 terrainUV = vexWorldPos.xz / gValue.REAL_TERRAIN_SIZE + 0.5;
                float terrainHeight = tex2Dlod(_HeightMap, float4(terrainUV, 0,0));
                vexWorldPos.y = (terrainHeight - 0.5) * 2 * gValue.worldHeightScale;
                o.vertex = UnityObjectToClipPos(float4(vexWorldPos, 0));
                o.uv = TRANSFORM_TEX(terrainUV, _MainTex);
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
