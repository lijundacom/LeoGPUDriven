#ifndef TERRAIN_DATA_STRUCT_DEFINE
#define TERRAIN_DATA_STRUCT_DEFINE

//globalValue[0] ---- Camera.main.transform.position.x;
//globalValue[1] ---- Camera.main.transform.position.y;
//globalValue[2] ---- Camera.main.transform.position.z;
//globalValue[3] ---- Camera.main.fieldOfView;
//globalValue[4] ---- TerrainDataManager.MIN_LOD;
//globalValue[5] ---- TerrainDataManager.REAL_TERRAIN_SIZE;
//globalValue[6] ---- TerrainDataManager.MAX_LOD_PATCH_SIZE;
//globalValue[7] ---- TerrainDataManager.PATCH_GRID_NUM;
//globalValue[8] ---- TerrainDataManager.PATCH_NUM_IN_NODE;
//globalValue[9] ---- TerrainDataManager.LodJudgeFector;

struct GlobalValue
{
    float3 cameraWorldPos;
    float fov;
    
    int MIN_LOD;
    float REAL_TERRAIN_SIZE;
    float MAX_LOD_PATCH_SIZE;
    int PATCH_GRID_NUM;
    
    int PATCH_NUM_IN_NODE;
    float LodJudgeFector;
    float worldHeightScale;
    uint2 hizMapSize;
};

//Patch and Node both use this struct
struct NodePatchStruct
{
    uint2 NodeXY;
    uint2 PatchXY;
    uint LOD;
    int4 LodTrans;
    float3 boundMin;
    float3 boundMax;
};

struct Bound
{
    float3 minPos;
    float3 maxPos;
};

#endif
