#ifndef TERRAIN_FUNC_UTIL_DEFINE
#define TERRAIN_FUNC_UTIL_DEFINE

#include "./DataStructDefine.cginc"

//获取某个LOD级别，Node的尺寸（1个维度）
inline float GetNodeSizeInLod(GlobalValue gvalue, int LOD)
{
    return gvalue.MAX_LOD_PATCH_SIZE * gvalue.PATCH_NUM_IN_NODE * (1 << LOD);
}

//获取某个LOD级别，Terrain在一个维度上NODE的数量。该LOD级别，包含的总共的数量是 result * result
inline int GetNodeNumInLod(GlobalValue gvalue, int LOD)
{
    return floor(gvalue.REAL_TERRAIN_SIZE / GetNodeSizeInLod(gvalue, LOD) + 0.1f);
}

//获取某个LOD级别，在一个维度上PATCH的长度（尺寸），该patch的面积是result*result
inline float GetPatchSizeInLod(GlobalValue gvalue, int LOD)
{
    return gvalue.MAX_LOD_PATCH_SIZE * (1 << LOD);
}

inline GlobalValue GetGlobalValue(float4 valueList[10])
{
    GlobalValue gvalue;
    gvalue.cameraWorldPos = float3(valueList[0].x, valueList[0].y, valueList[0].z);
    gvalue.fov = valueList[0].w;
    gvalue.MIN_LOD = valueList[1].x;
    gvalue.REAL_TERRAIN_SIZE = valueList[1].y;
    gvalue.MAX_LOD_PATCH_SIZE = valueList[1].z;
    gvalue.PATCH_GRID_NUM = valueList[1].w;
    gvalue.PATCH_NUM_IN_NODE = valueList[2].x;
    gvalue.LodJudgeFector = valueList[2].y;
    gvalue.worldHeightScale = valueList[2].z;
    gvalue.hizMapSize.x = valueList[2].w;
    gvalue.hizMapSize.y = valueList[3].x;
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

inline float2 GetNodeCenerPos(GlobalValue gvalue, uint2 nodeXY, uint LOD)
{
    float nodeSize = GetNodeSizeInLod(gvalue, LOD);
    uint nodeCount = GetNodeNumInLod(gvalue, LOD);
    float2 nodePos = nodeSize * (nodeXY + 0.5 - nodeCount * 0.5);
    return nodePos;
}

inline float2 GetPatchPosInNode(GlobalValue gvalue, uint2 xyInPatch, uint LOD)
{
    float patchSize = GetPatchSizeInLod(gvalue, LOD);
    float2 patchPos = patchSize * (xyInPatch + 0.5 - gvalue.PATCH_NUM_IN_NODE * 0.5);
    return patchPos;
}

inline NodePatchStruct CreateEmptyNodePatchStruct()
{
    NodePatchStruct nodeStruct;
    nodeStruct.NodeXY = 0;
    nodeStruct.PatchXY = 0;
    nodeStruct.LOD = 0;
    nodeStruct.LodTrans = 0;
    nodeStruct.boundMax = 0;
    nodeStruct.boundMin = 0;
    return nodeStruct;
}
#endif
