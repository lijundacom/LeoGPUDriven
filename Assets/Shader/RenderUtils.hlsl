#ifndef RENDER_UTIL_DEFINE
#define RENDER_UTIL_DEFINE

#include "../ComputeShader/DataStructDefine.cginc"
#include "../ComputeShader/TerrainFuncUtil.cginc"

inline void FixLODConnectSeam(inout float4 vertex, uint2 PatchXYInNode, uint NodeLOD, uint4 LOADTrans, GlobalValue gValue)
{
    float patchSize = GetPatchSizeInLod(gValue, 0);
    float patchGridSize = patchSize / (gValue.PATCH_GRID_NUM - 1);
    int2 vexIndex = floor((vertex.xz + patchSize * 0.5 + 0.01) / patchGridSize);
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
    
    if (vexIndex.x == gValue.PATCH_GRID_NUM - 1 && LOADTrans.z > 0)
    {
        uint step = 1 << LOADTrans.z;
        uint stepIndex = vexIndex.y % step;
        if (stepIndex != 0)
        {
            vertex.z -= patchGridSize * stepIndex;
        }
        return;
    }
    
    if (vexIndex.y == gValue.PATCH_GRID_NUM - 1 && LOADTrans.w > 0)
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

inline float3 CalTerrainVexPos(GlobalValue gValue, float4 vexPos, float4 pix0, float4 pix1)
{
    uint2 NodeXY = pix0.xy;
    uint PatchIndex = pix0.z;
    uint2 PatchXY = uint2(PatchIndex / 100, PatchIndex % 100);
    uint LOD = pix0.w;
    uint4 LOD_Trans = pix1;
    
    float2 nodePos = GetNodeCenerPos(gValue, NodeXY, LOD);
    float2 patchPosInNode = GetPatchPosInNode(gValue, PatchXY, LOD);
    float2 patchWorldPos = nodePos + patchPosInNode;
    FixLODConnectSeam(vexPos, PatchXY, LOD, LOD_Trans, gValue);
    float scale = 1 << LOD;
    float3 vexWorldPos = float3(patchWorldPos.x, 0, patchWorldPos.y) + vexPos.xyz * float3(scale, 1, scale);
    return vexWorldPos;
}

#endif