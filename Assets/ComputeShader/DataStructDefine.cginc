#ifndef TERRAIN_DATA_STRUCT_DEFINE
#define TERRAIN_DATA_STRUCT_DEFINE


//globalValue[1].x ---- MIN_LOD;
//globalValue[1].y ---- LOD_RANGE;
//globalValue[1].z ---- LOD0ChunkSize.x;
//globalValue[1].w ---- LOD0ChunkSize.y;
//globalValue[2].x ---- REAL_TERRAIN_SIZE.x;
//globalValue[2].y ---- REAL_TERRAIN_SIZE.y;
//globalValue[2].z ---- CHUNK_GRID_NUM;
//globalValue[2].w ---- StageHeight;
//globalValue[3].x ---- LOD0ChunkNum.x;
//globalValue[3].y ---- LOD0ChunkNum.y;
//globalValue[3].z ---- LodDivideLength;

//globalValue[4].x ---- Frustrum
//
//globalValue[9].Z ---- Frustrum

struct GlobalValue
{
    float3 cameraWorldPos;
    float LodJudgeFector;

    uint MIN_LOD;
    uint LOD_RANGE;   
    float2 LOD0ChunkSize;    
    float2 ChunkRootPos;
    uint CHUNK_GRID_NUM;
    float StageHeight;
    uint2 LOD0ChunkNum;
    float LodDivideLength;
};

//Patch and Node both use this struct
struct NodePatchStruct
{
    uint2 ChunkXY;
    uint LOD;
    uint LimitLOD;
    uint4 LodTrans;
    float3 boundMin;
    float3 boundMax;
};

struct Bound
{
    float3 minPos;
    float3 maxPos;
};

#endif
