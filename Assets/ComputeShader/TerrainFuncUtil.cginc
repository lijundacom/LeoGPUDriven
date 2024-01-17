#ifndef TERRAIN_FUNC_UTIL_DEFINE
#define TERRAIN_FUNC_UTIL_DEFINE

#include "./DataStructDefine.cginc"



    inline uint ReverseMortonCode2(uint x)
    {
        x &= 0x55555555;
        x = (x ^ (x >> 1)) & 0x33333333;
        x = (x ^ (x >> 2)) & 0x0f0f0f0f;
        x = (x ^ (x >> 4)) & 0x00ff00ff;
        x = (x ^ (x >> 8)) & 0x0000ffff;
        return x;
    }

    inline uint MortonCode2(uint x)
    {
        x &= 0x0000ffff;
        x = (x ^ (x << 8)) & 0x00ff00ff;
        x = (x ^ (x << 4)) & 0x0f0f0f0f;
        x = (x ^ (x << 2)) & 0x33333333;
        x = (x ^ (x << 1)) & 0x55555555;
        return x;
    }

//获取某个LOD级别，Node的尺寸（1个维度）
inline float2 GetNodeSizeInLod(GlobalValue gvalue, int LOD)
{
    return float2(gvalue.LOD0ChunkSize.x * (1 << LOD), gvalue.LOD0ChunkSize.y * (1 << LOD));
}

//获取某个LOD级别，Terrain在一个维度上NODE的数量。该LOD级别，包含的总共的数量是 result * result
inline uint2 GetNodeNumInLod(GlobalValue gvalue, int LOD)
{
    return uint2(gvalue.LOD0ChunkNum.x >> LOD, gvalue.LOD0ChunkNum.y >> LOD);
}

inline GlobalValue GetGlobalValue(float4 valueList[10])
{
    GlobalValue gvalue;
    gvalue.cameraWorldPos = float3(valueList[0].x, valueList[0].y, valueList[0].z);
    gvalue.LodJudgeFector = valueList[0].w;

    gvalue.MIN_LOD = uint(valueList[1].x);
    gvalue.LOD_RANGE= uint(valueList[1].y); 
    gvalue.LOD0ChunkSize = float2(valueList[1].z, valueList[1].w);    
    gvalue.ChunkRootPos = float2(valueList[2].x, valueList[2].y);
    gvalue.CHUNK_GRID_NUM = uint(valueList[2].z);
    gvalue.StageHeight = valueList[2].w;
    gvalue.LOD0ChunkNum = uint2(valueList[3].x, valueList[3].y);
    gvalue.LodDivideLength = valueList[3].z;
    return gvalue;
}

inline void GetFrustumPlane(float4 valueList[10], inout float4 frustumPlane[6])
{
    frustumPlane[0] = valueList[4];
    frustumPlane[1] = valueList[5];
    frustumPlane[2] = valueList[6];
    frustumPlane[3] = valueList[7];
    frustumPlane[4] = valueList[8];
    frustumPlane[5] = valueList[9];
}

float2 GetNodeCenerPos(GlobalValue gvalue, uint2 nodeXY, uint LOD)
{
    float2 nodeSize = GetNodeSizeInLod(gvalue, LOD);
    float2 nodePos = float2(nodeSize.x * (float(nodeXY.x) + 0.5), nodeSize.y * (float(nodeXY.y) + 0.5)) + gvalue.ChunkRootPos.xy;
    return nodePos;
}

inline NodePatchStruct CreateEmptyNodePatchStruct()
{
    NodePatchStruct nodeStruct;
    nodeStruct.ChunkXY = 0;
    nodeStruct.LimitLOD = 0;
    nodeStruct.LOD = 0;
    nodeStruct.LodTrans = 0;
    nodeStruct.boundMax = 0;
    nodeStruct.boundMin = 0;
    return nodeStruct;
}
#endif
