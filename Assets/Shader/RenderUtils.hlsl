#ifndef RENDER_UTIL_DEFINE
#define RENDER_UTIL_DEFINE

#include "../ComputeShader/DataStructDefine.cginc"
#include "../ComputeShader/TerrainFuncUtil.cginc"

float4 globalValueList2[10];

StructuredBuffer<NodePatchStruct> ChunkList;

void setup()
{              

}

void CalHexPtHeight_float(float value,out float height)
{
    GlobalValue gValue = GetGlobalValue(globalValueList2);
    height = (value * 255 - 127)* gValue.StageHeight;
}

void CalHexPtUV_float(uint2 StageTextureSize, float2 worldPosXZ,float2 HexOriginWorldPos, float hex_radius , out float2 UV)
{
    
    float2 localPos = worldPosXZ.xy - (HexOriginWorldPos.xy - float2(hex_radius * sqrt(3) * 0.5, 0));
    float2 quadSize = float2(hex_radius * sqrt(3),hex_radius * 3);
    int2 quadXY = int2(localPos.x/quadSize.x, localPos.y /quadSize.y);
    if(any(quadXY.xy < 0) || any(quadXY.xy >= StageTextureSize.xy))
    {
        UV = float2(0,0);
        return;
    }
    
    float2 quadRootLocalPos = float2(quadXY.x * quadSize.x, quadXY.y * quadSize.y);

    float2 pos0 = quadRootLocalPos + float2(quadSize.x * 0.5, 0);
    float2 pos1 = quadRootLocalPos + float2(quadSize.x, quadSize.y * 0.5);
    float2 pos2 = quadRootLocalPos + float2(quadSize.x * 0.5, quadSize.y);
    float2 pos3 = quadRootLocalPos + float2(0, quadSize.y * 0.5);

    float distance0 = distance(localPos, pos0);
    float distance1 = distance(localPos, pos1);
    float distance2 = distance(localPos, pos2);
    float distance3 = distance(localPos, pos3);
        
    uint2 HexPtXY;    

    if(distance0 < min(distance1, min(distance2, distance3)))
    {
        HexPtXY.x = quadXY.x;
        HexPtXY.y = quadXY.y * 2;
    }
    else if(distance1 < min(distance0, min(distance2, distance3)))
    {
        HexPtXY.x = quadXY.x;
        HexPtXY.y = quadXY.y * 2 + 1;
    }
    else if(distance2 < min(distance1, min(distance0, distance3)))
    {
        HexPtXY.x = quadXY.x;
        HexPtXY.y = quadXY.y * 2 + 2;
    }
    else
    {
        HexPtXY.x = quadXY.x - 1;
        HexPtXY.y = quadXY.y * 2 + 1;
    }

    HexPtXY.x = clamp(HexPtXY.x, 0, StageTextureSize.x);
    HexPtXY.y = clamp(HexPtXY.y, 0, StageTextureSize.y);

    UV = float2((HexPtXY.x + 0.5)/StageTextureSize.x, (HexPtXY.y + 0.5)/StageTextureSize.y);

    
}

inline void FixLODConnectSeam(inout float4 vertex, uint2 PatchXYInNode, uint NodeLOD, uint4 LOADTrans, GlobalValue gValue)
{
    float2 patchSize = GetNodeSizeInLod(gValue, 0);
    float2 patchGridSize = patchSize.xy / (gValue.CHUNK_GRID_NUM - 1);
    uint2 vexIndex = uint2((vertex.xz + patchSize * 0.5 + 0.01) / patchGridSize);
    if (vexIndex.x == 0 && LOADTrans.x > 0)
    {
        uint step = 1 << LOADTrans.x;
        uint stepIndex = vexIndex.y % step;
        if (stepIndex != 0)
        {
            vertex.z -= patchGridSize * stepIndex;
        }
        return;
    }
    
    if (vexIndex.y == 0 && LOADTrans.y > 0)
    {
        uint step = 1 << LOADTrans.y;
        uint stepIndex = vexIndex.x % step;
        if (stepIndex != 0)
        {
            vertex.x -= patchGridSize * stepIndex;
        }
        return;
    }
    
    if (vexIndex.x == gValue.CHUNK_GRID_NUM - 1 && LOADTrans.z > 0)
    {
        uint step = 1 << LOADTrans.z;
        uint stepIndex = vexIndex.y % step;
        if (stepIndex != 0)
        {
            vertex.z -= patchGridSize * stepIndex;
        }
        return;
    }
    
    if (vexIndex.y == gValue.CHUNK_GRID_NUM - 1 && LOADTrans.w > 0)
    {
        uint step = 1 << LOADTrans.w;
        uint stepIndex = vexIndex.x % step;
        if (stepIndex != 0)
        {
            vertex.x -= patchGridSize * stepIndex;
        }
        return;
    }
}


inline float4 CalTerrainVexPos(GlobalValue gValue, float4 vexPos, NodePatchStruct data)
{    
    float2 ChunkWorldPos = GetNodeCenerPos(gValue, data.ChunkXY, data.LOD);
    FixLODConnectSeam(vexPos, data.ChunkXY, data.LOD, data.LodTrans, gValue);
    uint scale = 1 << data.LOD;
    float4 vexWorldPos = float4(ChunkWorldPos.x, 0, ChunkWorldPos.y, 0) + vexPos.xyzw * float4(scale, 1, scale, 1);
    return vexWorldPos;
}

void CalVertexClipPos_float(float4 vertexPos,uint instanceID,out float4 worldPos)
{
    GlobalValue gValue = GetGlobalValue(globalValueList2);
    NodePatchStruct chunkData = ChunkList[instanceID];
    worldPos = CalTerrainVexPos(gValue, vertexPos, chunkData);
}




#endif