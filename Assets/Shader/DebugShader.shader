Shader "Unlit/DebugShader"
{
    Properties
    {

    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "LightMode" = "UniversalPipeline"}
        LOD 100
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha
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

            StructuredBuffer<NodePatchStruct> DebugCubeList;
            uniform float4 globalValueList[10];

            struct appdata
            {
                float4 vertex : POSITION;
                uint instanceID : SV_InstanceID;
};

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 color : TEXCOORD1;
};


            v2f vert (appdata v)
            {
                v2f o;
                float4 inVertex = v.vertex;
                NodePatchStruct nodePatchStruct = DebugCubeList[v.instanceID];
                uint2 NodeXY = nodePatchStruct.ChunkXY;
                uint LOD = nodePatchStruct.LOD;
                GlobalValue gValue = GetGlobalValue(globalValueList);
                float2 nodePos = GetNodeCenerPos(gValue, NodeXY, LOD);                
                float3 center = float3(nodePos.x, 0, nodePos.y);
                float3 scale = nodePatchStruct.boundMax - nodePatchStruct.boundMin;
                inVertex.xyz = inVertex.xyz * scale * 0.9;
                inVertex.xyz = inVertex.xyz + center;
                o.vertex = UnityObjectToClipPos(inVertex.xyz);
                //o.vertex = UnityObjectToClipPos(v.vertex);
                if (LOD == 0)
                {
                    o.color = float3(1, 0, 0);
                }
                else
                {
                    o.color = float3(1, (5 - LOD)*0.2 , 0);
                }
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return float4(i.color, 0.5);
            }
            ENDCG
        }
    }
}
