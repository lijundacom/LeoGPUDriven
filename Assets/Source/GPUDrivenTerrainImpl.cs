using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

public class GPUDrivenTerrainImpl
{
    public bool DEBUG = true;
    public ComputeShader CS_GPUDrivenTerrain;
    CommandBuffer GPUDrivenCullCMDBuffer;

    public int KN_CopyInputBuffer;
    public int KN_NodeQuadLod;
    public int KN_CreateSectorLodMap;
    public int KN_FrustumCull;

    ComputeBuffer InputChunkBuffer;

    Vector4[] globalValue = new Vector4[10];
    ComputeBuffer NodeIDOffsetOfLOD;

    ComputeBuffer finalList;
    ComputeBuffer NodeBrunchList;

    uint[] NodeBrunchData;

    RenderTexture SectorLODMap;
    Texture2D MinMaxHeightMap;

    ComputeBuffer mDrawInstanceBuffer;
    ComputeBuffer mDebugInstanceArgsBuffer;
    uint[] drawInstanceData = new uint[5];
    uint[] debugInstanceArgsData = new uint[5];

    ComputeBuffer mDispatchArgsBuffer;
    uint[] CopyInputDispatchArgsData = new uint[3] { 1, 1, 1 };
    uint[] NodeQuadLodDispatchArgsData = new uint[3] { 1, 1, 1 };

    Plane[] _cameraFrustumPlanes = new Plane[6];

    Mesh quardMesh;
    Mesh cubeMesh;

    ComputeBuffer nodeBufferPing;
    ComputeBuffer nodeBufferPang;
    public void Init()
    {
        CS_GPUDrivenTerrain = TerrainDataManager.GetInstance().CS_GPUDrivenTerrain;

        if (SystemInfo.usesReversedZBuffer)
        {
            Debug.Log("EnableKeyword _REVERSE_Z");
            CS_GPUDrivenTerrain.EnableKeyword("_REVERSE_Z");
        }
        else
        {
            Debug.Log("DisableKeyword _REVERSE_Z");
            CS_GPUDrivenTerrain.DisableKeyword("_REVERSE_Z");
        }

        if(SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3)
        {
            Debug.Log("EnableKeyword _OPENGL_ES_3");
            CS_GPUDrivenTerrain.EnableKeyword("_OPENGL_ES_3");
        }
        else
        {
            Debug.Log("DisableKeyword _OPENGL_ES_3");
            CS_GPUDrivenTerrain.DisableKeyword("_OPENGL_ES_3");
        }


        GPUDrivenCullCMDBuffer = new CommandBuffer();
        GPUDrivenCullCMDBuffer.name = "GPUDrivenCull";


        Vector2 patchSizeLod0 = TerrainDataManager.LOD0_CHUNK_SIZE;//8m x 8m
        Vector2Int patchGridNum = new Vector2Int(TerrainDataManager.PATCH_GRID_NUM, TerrainDataManager.PATCH_GRID_NUM);
        quardMesh = MeshCreator.getInstance().CreateQuardMesh(patchSizeLod0, patchGridNum);
        cubeMesh = MeshCreator.getInstance().CreateCube(1.0f);

        KN_CopyInputBuffer = CS_GPUDrivenTerrain.FindKernel(ComputeShaderDefine.KN_CopyInputBuffer);
        KN_NodeQuadLod = CS_GPUDrivenTerrain.FindKernel(ComputeShaderDefine.KN_NodeQuadLod);
        KN_CreateSectorLodMap = CS_GPUDrivenTerrain.FindKernel(ComputeShaderDefine.KN_CreateSectorLodMap);
        KN_FrustumCull = CS_GPUDrivenTerrain.FindKernel(ComputeShaderDefine.KN_FrustumCull);

        InputChunkBuffer = new ComputeBuffer(100, 4);

        int totalPatchNum = 0;
        for(int i= TerrainDataManager.MIN_LOD; i>=0 ;i--)
        {
            Vector2Int patchNum = TerrainDataManager.GetInstance().GetChunkNumInLod(i);
            totalPatchNum += patchNum.x * patchNum.y;
        }


        finalList = new ComputeBuffer(TerrainDataManager.ChunkNumInLOD0.x * TerrainDataManager.ChunkNumInLOD0.y, 60, ComputeBufferType.Append);
        nodeBufferPing = new ComputeBuffer(totalPatchNum, 60, ComputeBufferType.Append);
        nodeBufferPang = new ComputeBuffer(totalPatchNum, 60, ComputeBufferType.Append);
        NodeBrunchList = new ComputeBuffer(totalPatchNum, 4);
        NodeBrunchData = new uint[totalPatchNum];
        NodeBrunchList.SetData(NodeBrunchData);

        mDispatchArgsBuffer = new ComputeBuffer(3,4,ComputeBufferType.IndirectArguments);

        NodeIDOffsetOfLOD = new ComputeBuffer((TerrainDataManager.MIN_LOD + 1),4);
        NodeIDOffsetOfLOD.SetData(TerrainDataManager.GetInstance().NodeIndexOffsetList);

        mDrawInstanceBuffer = new ComputeBuffer(5, 4,ComputeBufferType.IndirectArguments);
        drawInstanceData[0] = MeshCreator.getInstance().cacheQuadMesh.GetIndexCount(0);
        mDrawInstanceBuffer.SetData(drawInstanceData);

        if (DEBUG)
        {
            mDebugInstanceArgsBuffer = new ComputeBuffer(5, 4, ComputeBufferType.IndirectArguments);
            debugInstanceArgsData[0] = MeshCreator.getInstance().debugCubeMesh.GetIndexCount(0);
            mDebugInstanceArgsBuffer.SetData(debugInstanceArgsData);
        }

        RenderTextureDescriptor sectorLODMapDes = new RenderTextureDescriptor(TerrainDataManager.ChunkNumInLOD0.x, TerrainDataManager.ChunkNumInLOD0.y, RenderTextureFormat.R8, 0, 1);
        sectorLODMapDes.enableRandomWrite = true;       
        SectorLODMap = RenderTexture.GetTemporary(sectorLODMapDes);
        SectorLODMap.filterMode = FilterMode.Point;
        SectorLODMap.Create();

        MinMaxHeightMap = TerrainDataManager.GetInstance().TerrainMinMaxHeightMap;
    }

    public void ClearCommandBuffer()
    {
        GPUDrivenCullCMDBuffer.Clear();
    }

    public void SetGlobaleValue()
    {
        if(mCamera != null)
        {
            globalValue[0].x = mCamera.transform.position.x;
            globalValue[0].y = mCamera.transform.position.y;
            globalValue[0].z = mCamera.transform.position.z;
        }

        globalValue[1].x = TerrainDataManager.MIN_LOD;
        globalValue[1].y = TerrainDataManager.LODRange;
        globalValue[1].z = TerrainDataManager.LOD0_CHUNK_SIZE.x;
        globalValue[1].w = TerrainDataManager.LOD0_CHUNK_SIZE.y;

        globalValue[2].x = TerrainDataManager.CHUNK_ROOT_POS.x;
        globalValue[2].y = TerrainDataManager.CHUNK_ROOT_POS.y;
        globalValue[2].z = TerrainDataManager.PATCH_GRID_NUM;
        globalValue[2].w = TerrainDataManager.STAGE_HEIGHT;

        globalValue[3].x = TerrainDataManager.ChunkNumInLOD0.x;
        globalValue[3].y = TerrainDataManager.ChunkNumInLOD0.y;
        globalValue[3].z = TerrainDataManager.LodDivideLength;


        GeometryUtility.CalculateFrustumPlanes(Camera.main, _cameraFrustumPlanes);

        for(int i=0;i<6;i++)
        {
            globalValue[4 + i].Set(_cameraFrustumPlanes[i].normal.x, _cameraFrustumPlanes[i].normal.y, _cameraFrustumPlanes[i].normal.z, _cameraFrustumPlanes[i].distance);
        }

        GPUDrivenCullCMDBuffer.SetComputeVectorArrayParam(CS_GPUDrivenTerrain, ComputeShaderDefine.globalValueList_P, globalValue);        
    }


    private Camera mCamera;

    private Vector4[] CalCameraFrustum(Camera camera)
    {
        mCamera = camera;

        Vector3[] frustumCorners = new Vector3[4];
        camera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), camera.farClipPlane, Camera.MonoOrStereoscopicEye.Mono, frustumCorners);

        //视锥体与地面相交的梯形的4个顶点坐标
        Vector4[] intersectons = new Vector4[4];//顺序：左下、左上、右上、右下
        for (int i = 0; i < 4; i++)
        {
            var worldSpaceCorner = camera.transform.TransformVector(frustumCorners[i]);
            var rayDir = worldSpaceCorner.normalized;
            Vector3 intersection = FMath.GetIntersectionOfRayAndPlane(camera.transform.position, rayDir, Vector3.up, Vector3.zero);
            intersectons[i] = intersection;
        }

        if (DEBUG)
        {
            Vector3 cameraDir = camera.transform.forward.normalized;
            Vector3 CenterIntersection = FMath.GetIntersectionOfRayAndPlane(camera.transform.position, cameraDir, Vector3.up, Vector3.zero);

            for (int i = 0; i < 4; i++)
            {
                UnityEngine.Debug.DrawLine(intersectons[i % 4], intersectons[(i + 1) % 4], Color.red);
                UnityEngine.Debug.DrawLine(intersectons[i], CenterIntersection, Color.yellow);
            }
        }

        return intersectons;
    }

    Vector4[] mIntersections;

    public int GetBaseLOD(Camera camera)
    {
        return (int)math.floor(math.log2(camera.transform.position.y / TerrainDataManager.LodDivideLength));
    }

    BoundsInt mViewChunks = new BoundsInt();

    public bool mViewChunkIsDirty = false;

    public uint[] mInputPatchList;

    public void UpdateViewChunk(Camera camera)
    {
        mIntersections = CalCameraFrustum(camera);

        int baseLOD = GetBaseLOD(camera);

        InputMinLOD = math.clamp(baseLOD + TerrainDataManager.LODRange, 0, TerrainDataManager.MIN_LOD);


        Vector2 BasePathSize = TerrainDataManager.GetInstance().GetPatchSizeInLod(InputMinLOD);

        Vector2 chunkRootPos = TerrainDataManager.CHUNK_ROOT_POS;

        Vector2Int maxChunkNum = TerrainDataManager.GetInstance().GetChunkNumInLod(InputMinLOD);

        //视锥体与地面相交的梯形的4个顶点的BaseMip级别下的Tile坐标
        Vector2Int BottomLeftTile = new Vector2Int((int)((mIntersections[0].x - chunkRootPos.x) / BasePathSize.x), (int)((mIntersections[0].z - chunkRootPos.y) / BasePathSize.y));
        Vector2Int TopLeftTile = new Vector2Int((int)((mIntersections[1].x - chunkRootPos.x) / BasePathSize.x), (int)((mIntersections[1].z - chunkRootPos.y) / BasePathSize.y));
        Vector2Int TopRightTile = new Vector2Int((int)((mIntersections[2].x - chunkRootPos.x) / BasePathSize.x), (int)((mIntersections[2].z - chunkRootPos.y) / BasePathSize.y));
        Vector2Int BottomRightTile = new Vector2Int((int)((mIntersections[3].x - chunkRootPos.x) / BasePathSize.x), (int)((mIntersections[3].z - chunkRootPos.y) / BasePathSize.y));

        Vector3 CenterIntersection = FMath.GetIntersectionOfRayAndPlane(mCamera.transform.position, mCamera.transform.forward.normalized, Vector3.up, Vector3.zero);
        //人眼看屏幕时，总盯着真正的屏幕中心偏上一些的地方看
        Vector3 LODCenter = CenterIntersection + 0.25f * BasePathSize.y * Vector3.forward;
        GPUDrivenCullCMDBuffer.SetComputeVectorParam(CS_GPUDrivenTerrain, "_LODCenter", LODCenter);

        BoundsInt curBound = new BoundsInt();

        curBound.xMin = math.max(TopLeftTile.x, 0);
        curBound.xMax = math.min(TopRightTile.x, maxChunkNum.x);
        curBound.zMin = math.max(BottomRightTile.y, 0);
        curBound.zMax = math.min(TopRightTile.y, maxChunkNum.y);

        mInputPatchList = new uint[(curBound.xMax - curBound.xMin + 1) *(curBound.zMax - curBound.zMin + 1)];

        int index = 0;
        for (uint i = (uint)curBound.xMin; i <= curBound.xMax; i++)
        {
            for (uint j = (uint)curBound.zMin; j <= curBound.zMax; j++)
            {
                uint vAdress = FMath.MortonCode2(i) | (FMath.MortonCode2(j) << 1);
                mInputPatchList[index] = vAdress;
                index++;
            }
        }

        TerrainDataManager.GetInstance().GPUCullCMDBuffer = null;
        //if (baseLOD != BaseLOD || curBound != mViewChunks)
        {
            mViewChunks = curBound;
            TerrainDataManager.GetInstance().GPUCullCMDBuffer = GPUDrivenCullCMDBuffer;
            CopyInputBuffer();
            CreateLODChunkList();
            //CreateSectorLodMap();
        }
        CalFrustumCulledPatchList();
    }


    public int InputMinLOD;

    /// <summary>
    /// 初始化最开始LODMin时的5x5个node
    /// </summary>
    private void CopyInputBuffer()
    {
        GPUDrivenCullCMDBuffer.SetBufferCounterValue(nodeBufferPing, 0);
        GPUDrivenCullCMDBuffer.SetComputeBufferParam(CS_GPUDrivenTerrain, KN_CopyInputBuffer, ComputeShaderDefine.appendList_P, nodeBufferPing);//结果存储到nodeBufferPing中
        GPUDrivenCullCMDBuffer.SetComputeIntParam(CS_GPUDrivenTerrain, "InputLOD", InputMinLOD);
        GPUDrivenCullCMDBuffer.SetBufferData(InputChunkBuffer, mInputPatchList);
        GPUDrivenCullCMDBuffer.SetComputeBufferParam(CS_GPUDrivenTerrain, KN_CopyInputBuffer, "InputChunkList", InputChunkBuffer);
        GPUDrivenCullCMDBuffer.DispatchCompute(CS_GPUDrivenTerrain, KN_CopyInputBuffer, mInputPatchList.Length, 1,1);
        if(DEBUG)
        {
            //GPUDrivenCullCMDBuffer.CopyCounterValue(nodeBufferPing, mDebugInstanceArgsBuffer, 4);
        }
    }

    /// <summary>
    /// 生成四叉树分割后的，LOD的Node列表
    /// 需要使用乒乓列表的方式来处理
    /// 用CommandBuffer统一处理多个dispatch，会比单独调用dispatch快一点。
    /// computeshader.GetData，从GPU拿数据到CPU，在手机上非常耗时。取5个int，就要耗时25ms
    /// </summary>
    public void CreateLODChunkList()
    {
        GPUDrivenCullCMDBuffer.SetBufferCounterValue(nodeBufferPang, 0);
        GPUDrivenCullCMDBuffer.SetBufferCounterValue(finalList, 0);
        GPUDrivenCullCMDBuffer.SetBufferData(NodeBrunchList, NodeBrunchData);//clear
        
        //GPUDrivenCullCMDBuffer.SetBufferCounterValue(NodeBrunchList, 0);

        GPUDrivenCullCMDBuffer.SetComputeBufferParam(CS_GPUDrivenTerrain, KN_NodeQuadLod, ComputeShaderDefine.finalList_P, finalList);
        GPUDrivenCullCMDBuffer.SetComputeBufferParam(CS_GPUDrivenTerrain, KN_NodeQuadLod, ComputeShaderDefine.NodeBrunchList_P, NodeBrunchList);
        GPUDrivenCullCMDBuffer.SetComputeBufferParam(CS_GPUDrivenTerrain, KN_NodeQuadLod, ComputeShaderDefine.NodeIDOffsetOfLOD_P, NodeIDOffsetOfLOD);

        GPUDrivenCullCMDBuffer.SetComputeTextureParam(CS_GPUDrivenTerrain, KN_NodeQuadLod, ComputeShaderDefine.MinMaxHeightMap_P, MinMaxHeightMap);

        NodeQuadLodDispatchArgsData[0] = (uint)mInputPatchList.Length;
        NodeQuadLodDispatchArgsData[1] = 1;
        NodeQuadLodDispatchArgsData[2] = 1;
        GPUDrivenCullCMDBuffer.SetBufferData(mDispatchArgsBuffer, NodeQuadLodDispatchArgsData);

        for (int i = InputMinLOD; i >= 0; i--)
        {
            GPUDrivenCullCMDBuffer.SetComputeIntParam(CS_GPUDrivenTerrain, ComputeShaderDefine.CURRENT_LOD_P, i);

            GPUDrivenCullCMDBuffer.SetComputeBufferParam(CS_GPUDrivenTerrain, KN_NodeQuadLod, ComputeShaderDefine.consumeList_P, nodeBufferPing);
            GPUDrivenCullCMDBuffer.SetComputeBufferParam(CS_GPUDrivenTerrain, KN_NodeQuadLod, ComputeShaderDefine.appendList_P, nodeBufferPang);
            GPUDrivenCullCMDBuffer.DispatchCompute(CS_GPUDrivenTerrain, KN_NodeQuadLod, mDispatchArgsBuffer, 0);

            GPUDrivenCullCMDBuffer.CopyCounterValue(nodeBufferPang, mDispatchArgsBuffer, 0);

            ComputeBuffer temp = nodeBufferPing;
            nodeBufferPing = nodeBufferPang;
            nodeBufferPang = temp;
        }
        if (DEBUG)
        {
            //GPUDrivenCullCMDBuffer.CopyCounterValue(finalList, mDebugInstanceArgsBuffer, 4);
        }

    }

    /// <summary>
    /// LOD0时，一个node记为一个sector
    /// 生成一张纹理，这张纹理记载每个sector的LOD
    /// 用于在处理接缝时，获取旁边patch的LOD
    /// </summary>
    public void CreateSectorLodMap()
    {
        Vector2Int nodeNumLod0 = TerrainDataManager.GetInstance().GetChunkNumInLod(0);
        GPUDrivenCullCMDBuffer.SetComputeTextureParam(CS_GPUDrivenTerrain, KN_CreateSectorLodMap, ComputeShaderDefine.SectorLODMap_P, SectorLODMap); ;
        GPUDrivenCullCMDBuffer.SetComputeBufferParam(CS_GPUDrivenTerrain, KN_CreateSectorLodMap, ComputeShaderDefine.NodeBrunchList_P, NodeBrunchList);
        GPUDrivenCullCMDBuffer.DispatchCompute(CS_GPUDrivenTerrain, KN_CreateSectorLodMap, nodeNumLod0.x/32, nodeNumLod0.y/32, 1);
    }

    /// <summary>
    /// 根据LOD化后的nodelist，经过视锥体剪裁，计算出需要显示的NodeList
    /// </summary>
    public void CalFrustumCulledPatchList()
    {
        GPUDrivenCullCMDBuffer.CopyCounterValue(finalList, mDispatchArgsBuffer, 0);
        GPUDrivenCullCMDBuffer.SetBufferCounterValue(nodeBufferPang, 0);
        //此时LOD后的node全部存在 finalList中，作为 KN_FrustumCull 的 consumeList_P传入
        GPUDrivenCullCMDBuffer.SetComputeBufferParam(CS_GPUDrivenTerrain, KN_FrustumCull, ComputeShaderDefine.consumeList_P, finalList);
        GPUDrivenCullCMDBuffer.SetComputeBufferParam(CS_GPUDrivenTerrain, KN_FrustumCull, ComputeShaderDefine.appendList_P, nodeBufferPang);
        GPUDrivenCullCMDBuffer.SetComputeTextureParam(CS_GPUDrivenTerrain, KN_FrustumCull, ComputeShaderDefine.MinMaxHeightMap_P, MinMaxHeightMap);
        GPUDrivenCullCMDBuffer.DispatchCompute(CS_GPUDrivenTerrain, KN_FrustumCull, mDispatchArgsBuffer,0);
        if (DEBUG)
        {
            GPUDrivenCullCMDBuffer.CopyCounterValue(nodeBufferPang, mDebugInstanceArgsBuffer, 4);
        }
    }

    public bool CheckRender()
    {
        if(DEBUG)
        {
            return true;
        }
        if(TerrainDataManager.GetInstance().TerrainMaterial != null
            && MinMaxHeightMap != null)
        {
            return true;
        }
        return false;
    }

    public ComputeBuffer GetDrawInstanceArgsBuffer()
    {
        if(DEBUG)
        {
            mDrawInstanceBuffer.GetData(drawInstanceData);
            //Debug.Log("drawInstanceData count:" + drawInstanceData[0] + "|" + drawInstanceData[1]);
        }
        return mDrawInstanceBuffer;
    }

    public ComputeBuffer GetDebugDrawInstanceArgsBuffer()
    {
        mDebugInstanceArgsBuffer.GetData(debugInstanceArgsData);
        //Debug.Log("mDebugInstanceArgsBuffer count:"+ debugInstanceArgsData[0] + "|" + debugInstanceArgsData[1]);
        return mDebugInstanceArgsBuffer;
    }

    /// <summary>
    /// 绘制PatchInstance 组成的 Terrain
    /// </summary>
    public void DrawTerrainInstance()
    {
        GPUDrivenCullCMDBuffer.SetGlobalBuffer("ChunkList", nodeBufferPang);
        GPUDrivenCullCMDBuffer.CopyCounterValue(nodeBufferPang, mDrawInstanceBuffer, 4);
        GPUDrivenCullCMDBuffer.SetGlobalVectorArray("globalValueList2", globalValue);
        GPUDrivenCullCMDBuffer.DrawMeshInstancedIndirect(quardMesh, 0, TerrainDataManager.GetInstance().TerrainMaterial
            , 0, GetDrawInstanceArgsBuffer());
    }

    public void DrawDebugCubeInstance()
    {
        if (DEBUG)
        {
            GPUDrivenCullCMDBuffer.SetGlobalBuffer(ComputeShaderDefine.DebugCubeList, nodeBufferPang);
            GPUDrivenCullCMDBuffer.SetGlobalVectorArray(ComputeShaderDefine.globalValueList_P, globalValue);
            GPUDrivenCullCMDBuffer.DrawMeshInstancedIndirect(TerrainDataManager.GetInstance().CubeMesh, 
                0, TerrainDataManager.GetInstance().TerrainDebugMaterial, -1,GetDebugDrawInstanceArgsBuffer(), 0);
        }
    }

    public void Dispose()
    {
        if(finalList != null) finalList.Dispose();
        if (nodeBufferPing != null) nodeBufferPing.Dispose();
        if (nodeBufferPang != null) nodeBufferPang.Dispose();
        if (NodeBrunchList != null) NodeBrunchList.Dispose();
        if (NodeIDOffsetOfLOD != null) NodeIDOffsetOfLOD.Dispose();
        if (mDrawInstanceBuffer != null) mDrawInstanceBuffer.Dispose();
        if (mDispatchArgsBuffer != null) mDispatchArgsBuffer.Dispose();
        if(DEBUG && mDebugInstanceArgsBuffer != null) mDebugInstanceArgsBuffer.Dispose();
        if (GPUDrivenCullCMDBuffer != null) GPUDrivenCullCMDBuffer.Dispose();

        RenderTexture.ReleaseTemporary(SectorLODMap);

        TerrainDataManager.GetInstance().GPUCullCMDBuffer = null;
    }
   
}

