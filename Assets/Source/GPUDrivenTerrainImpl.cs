using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

public class GPUDrivenTerrainImpl
{
    public bool DEBUG = false;
    public ComputeShader CS_GPUDrivenTerrain;
    CommandBuffer command;

    public int KN_CopyInputBuffer;
    public int KN_NodeQuadLod;
    public int KN_CreateSectorLodMap;
    public int KN_FrustumCull;
    public int KN_CreatePatch;
    public int KN_HizCull;

    Vector4[] globalValue = new Vector4[10];
    ComputeBuffer NodeIDOffsetOfLOD;

    ComputeBuffer finalList;
    ComputeBuffer NodeBrunchList;

    RenderTexture SectorLODMap;
    Texture2D MinMaxHeightMap;

    RenderTexture mRenderPatchMap;

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
            CS_GPUDrivenTerrain.EnableKeyword("_REVERSE_Z");
        }
        else
        {
            CS_GPUDrivenTerrain.DisableKeyword("_REVERSE_Z");
        }


        command = new CommandBuffer();
        command.name = "GPUDriven";

        Vector2 patchSizeLodMin = new Vector2(TerrainDataManager.MAX_LOD_PATCH_SIZE, TerrainDataManager.MAX_LOD_PATCH_SIZE);//8m x 8m
        Vector2Int patchGridNum = new Vector2Int(TerrainDataManager.PATCH_GRID_NUM, TerrainDataManager.PATCH_GRID_NUM);
        quardMesh = MeshCreator.getInstance().CreateQuardMesh(patchSizeLodMin, patchGridNum);
        cubeMesh = MeshCreator.getInstance().CreateCube(1.0f);

        KN_CopyInputBuffer = CS_GPUDrivenTerrain.FindKernel(ComputeShaderDefine.KN_CopyInputBuffer);
        KN_NodeQuadLod = CS_GPUDrivenTerrain.FindKernel(ComputeShaderDefine.KN_NodeQuadLod);
        KN_CreateSectorLodMap = CS_GPUDrivenTerrain.FindKernel(ComputeShaderDefine.KN_CreateSectorLodMap);
        KN_FrustumCull = CS_GPUDrivenTerrain.FindKernel(ComputeShaderDefine.KN_FrustumCull);
        KN_CreatePatch = CS_GPUDrivenTerrain.FindKernel(ComputeShaderDefine.KN_CreatePatch);
        KN_HizCull = CS_GPUDrivenTerrain.FindKernel(ComputeShaderDefine.KN_HizCull);

        int nodeNumLodMin = TerrainDataManager.GetInstance().GetNodeNumInLod(TerrainDataManager.MIN_LOD);
        List<Vector2Int> nodeListLOD0List = new List<Vector2Int>();
        for (int i=0;i< nodeNumLodMin;i++)
        {
            for(int j=0;j<nodeNumLodMin;j++)
            {
                nodeListLOD0List.Add(new Vector2Int(i,j));
            }
        }


        int totalNodeNum = 0;
        for(int i= TerrainDataManager.MIN_LOD; i>=0 ;i--)
        {
            int nodeNum = TerrainDataManager.GetInstance().GetNodeNumInLod(i);
            totalNodeNum += (nodeNum * nodeNum);
        }

        int nodeNumLod0 = TerrainDataManager.GetInstance().GetNodeNumInLod(0);
        int patchNumInNode = TerrainDataManager.PATCH_NUM_IN_NODE;

        finalList = new ComputeBuffer(nodeNumLod0 * nodeNumLod0 * patchNumInNode * patchNumInNode / 4, 60, ComputeBufferType.Append);
        nodeBufferPing = new ComputeBuffer(totalNodeNum, 60, ComputeBufferType.Append);
        nodeBufferPang = new ComputeBuffer(totalNodeNum, 60, ComputeBufferType.Append);
        NodeBrunchList = new ComputeBuffer(totalNodeNum, 4);

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

        RenderTextureDescriptor sectorLODMapDes = new RenderTextureDescriptor(nodeNumLod0, nodeNumLod0, RenderTextureFormat.RFloat, 0, 1);
        sectorLODMapDes.enableRandomWrite = true;
        SectorLODMap = RenderTexture.GetTemporary(sectorLODMapDes);
        SectorLODMap.filterMode = FilterMode.Point;
        SectorLODMap.Create();

        MinMaxHeightMap = TerrainDataManager.GetInstance().TerrainMinMaxHeightMap;

        RenderTextureDescriptor renderPatchMapDesc = new RenderTextureDescriptor(512, 512, RenderTextureFormat.ARGBFloat, 0, 1);
        renderPatchMapDesc.enableRandomWrite = true;
        mRenderPatchMap = RenderTexture.GetTemporary(renderPatchMapDesc);
        mRenderPatchMap.filterMode= FilterMode.Point;
        mRenderPatchMap.Create();


        globalValue[1].x = TerrainDataManager.MIN_LOD;
        globalValue[1].y = TerrainDataManager.REAL_TERRAIN_SIZE;
        globalValue[1].z = TerrainDataManager.MAX_LOD_PATCH_SIZE;
        globalValue[1].w = TerrainDataManager.PATCH_GRID_NUM;
        globalValue[2].x = TerrainDataManager.PATCH_NUM_IN_NODE;
        globalValue[2].z = TerrainDataManager.WorldHeightScale;
        globalValue[2].w = TerrainDataManager.HIZMapSize.x;
        globalValue[3].x = TerrainDataManager.HIZMapSize.y;
    }

    public void ClearCommandBuffer()
    {
        command.Clear();
    }

    public void SetGlobaleValue()
    {
        globalValue[0].x = Camera.main.transform.position.x;
        globalValue[0].y = Camera.main.transform.position.y;
        globalValue[0].z = Camera.main.transform.position.z;
        globalValue[0].w = Camera.main.fieldOfView;
        globalValue[2].y = TerrainDataManager.LodJudgeFector;

        GeometryUtility.CalculateFrustumPlanes(Camera.main, _cameraFrustumPlanes);

        for (int i = 0; i < 6; i++)
        {
            globalValue[4 + i].Set(_cameraFrustumPlanes[i].normal.x, _cameraFrustumPlanes[i].normal.y, _cameraFrustumPlanes[i].normal.z, _cameraFrustumPlanes[i].distance);
            if(DEBUG)
            {
                //Debug.Log("FrustumPlanes:"+ i+"|"+ _cameraFrustumPlanes[i].normal + "|"+ _cameraFrustumPlanes[i].distance);
            }
        }

        command.SetComputeVectorArrayParam(CS_GPUDrivenTerrain, ComputeShaderDefine.globalValueList_P, globalValue);
    }

    /// <summary>
    /// 初始化最开始LODMin时的5x5个node
    /// </summary>
    public void CopyInputBuffer()
    {
        command.SetBufferCounterValue(nodeBufferPing, 0);
        //创建起始LOD_MIN时，5x5的nodelist，存入appendList里面
        command.SetComputeBufferParam(CS_GPUDrivenTerrain, KN_CopyInputBuffer, ComputeShaderDefine.appendList_P, nodeBufferPing);
        int nodeNumLodMin = TerrainDataManager.GetInstance().GetNodeNumInLod(TerrainDataManager.MIN_LOD);
        CopyInputDispatchArgsData[0] = (uint)nodeNumLodMin;
        CopyInputDispatchArgsData[1] = (uint)nodeNumLodMin;
        CopyInputDispatchArgsData[2] = 1;
        command.SetBufferData(mDispatchArgsBuffer, CopyInputDispatchArgsData);
        command.DispatchCompute(CS_GPUDrivenTerrain, KN_CopyInputBuffer, mDispatchArgsBuffer, 0);
        //此时，nodeBufferPing有了5x5的LOD_MIN时的待处理的Node，nodeBufferPing需要作为consumeList_P传给KN_NodeQuadLod。
    }

    /// <summary>
    /// 生成四叉树分割后的，LOD的Node列表
    /// 需要使用乒乓列表的方式来处理
    /// 用CommandBuffer统一处理多个dispatch，会比单独调用dispatch快一点。
    /// computeshader.GetData，从GPU拿数据到CPU，在手机上非常耗时。取5个int，就要耗时25ms
    /// </summary>
    public void CreateLODNodeList()
    {
        int nodeNumLodMin = TerrainDataManager.GetInstance().GetNodeNumInLod(TerrainDataManager.MIN_LOD);
        command.SetBufferCounterValue(nodeBufferPang, 0);
        command.SetBufferCounterValue(finalList, 0);
        command.SetBufferCounterValue(NodeBrunchList, 0);

        command.SetComputeBufferParam(CS_GPUDrivenTerrain, KN_NodeQuadLod, ComputeShaderDefine.finalList_P, finalList);
        command.SetComputeBufferParam(CS_GPUDrivenTerrain, KN_NodeQuadLod, ComputeShaderDefine.NodeBrunchList_P, NodeBrunchList);
        command.SetComputeBufferParam(CS_GPUDrivenTerrain, KN_NodeQuadLod, ComputeShaderDefine.NodeIDOffsetOfLOD_P, NodeIDOffsetOfLOD);

        command.SetComputeTextureParam(CS_GPUDrivenTerrain, KN_NodeQuadLod, ComputeShaderDefine.MinMaxHeightMap_P, MinMaxHeightMap);

        NodeQuadLodDispatchArgsData[0] = (uint)(nodeNumLodMin * nodeNumLodMin);
        NodeQuadLodDispatchArgsData[1] = 1;
        NodeQuadLodDispatchArgsData[2] = 1;
        command.SetBufferData(mDispatchArgsBuffer, NodeQuadLodDispatchArgsData);

        for (int i = TerrainDataManager.MIN_LOD; i >= 0; i--)
        {
            command.SetComputeIntParam(CS_GPUDrivenTerrain, ComputeShaderDefine.CURRENT_LOD_P, i);

            command.SetComputeBufferParam(CS_GPUDrivenTerrain, KN_NodeQuadLod, ComputeShaderDefine.consumeList_P, nodeBufferPing);
            command.SetComputeBufferParam(CS_GPUDrivenTerrain, KN_NodeQuadLod, ComputeShaderDefine.appendList_P, nodeBufferPang);
            command.DispatchCompute(CS_GPUDrivenTerrain, KN_NodeQuadLod, mDispatchArgsBuffer, 0);

            command.CopyCounterValue(nodeBufferPang, mDispatchArgsBuffer, 0);

            ComputeBuffer temp = nodeBufferPing;
            nodeBufferPing = nodeBufferPang;
            nodeBufferPang = temp;
        }
        if (DEBUG)
        {
            //command.CopyCounterValue(finalList, mDebugInstanceArgsBuffer, 4);
        }

    }

    /// <summary>
    /// LOD0时，一个node记为一个sector
    /// 生成一张纹理，这张纹理记载每个sector的LOD
    /// 用于在处理接缝时，获取旁边patch的LOD
    /// </summary>
    public void CreateSectorLodMap()
    {
        int nodeNumLod0 = TerrainDataManager.GetInstance().GetNodeNumInLod(0);
        command.SetComputeTextureParam(CS_GPUDrivenTerrain, KN_CreateSectorLodMap, ComputeShaderDefine.SectorLODMap_P, SectorLODMap); ;
        command.SetComputeBufferParam(CS_GPUDrivenTerrain, KN_CreateSectorLodMap, ComputeShaderDefine.NodeBrunchList_P, NodeBrunchList);
        command.DispatchCompute(CS_GPUDrivenTerrain, KN_CreateSectorLodMap, nodeNumLod0/8, nodeNumLod0/8, 1);
    }

    /// <summary>
    /// 根据LOD化后的nodelist，经过视锥体剪裁，计算出需要显示的NodeList
    /// </summary>
    public void CalFrustumCulledPatchList()
    {
        command.CopyCounterValue(finalList, mDispatchArgsBuffer, 0);
        command.SetBufferCounterValue(nodeBufferPang, 0);
        //此时LOD后的node全部存在 finalList中，作为 KN_FrustumCull 的 consumeList_P传入
        command.SetComputeBufferParam(CS_GPUDrivenTerrain, KN_FrustumCull, ComputeShaderDefine.consumeList_P, finalList);
        command.SetComputeBufferParam(CS_GPUDrivenTerrain, KN_FrustumCull, ComputeShaderDefine.appendList_P, nodeBufferPang);
        command.SetComputeTextureParam(CS_GPUDrivenTerrain, KN_FrustumCull, ComputeShaderDefine.MinMaxHeightMap_P, MinMaxHeightMap);
        command.DispatchCompute(CS_GPUDrivenTerrain, KN_FrustumCull, mDispatchArgsBuffer,0);
        if (DEBUG)
        {
            //command.CopyCounterValue(nodeBufferPang, mDebugInstanceArgsBuffer, 4);
        }
    }

    /// <summary>
    /// 将NodeList扩展成PatchList
    /// </summary>
    public void CreatePatch()
    {
        command.CopyCounterValue(nodeBufferPang, mDispatchArgsBuffer, 0);
        command.SetBufferCounterValue(finalList, 0);
        command.SetComputeBufferParam(CS_GPUDrivenTerrain, KN_CreatePatch, ComputeShaderDefine.consumeList_P, nodeBufferPang);
        command.SetComputeBufferParam(CS_GPUDrivenTerrain, KN_CreatePatch, ComputeShaderDefine.appendList_P, finalList);
        command.DispatchCompute(CS_GPUDrivenTerrain, KN_CreatePatch, mDispatchArgsBuffer, 0);
        if (DEBUG)
        {
            command.CopyCounterValue(finalList, mDebugInstanceArgsBuffer, 4);
        }
    }

    /// <summary>
    /// Hiz遮挡剔除
    /// </summary>
    public void CalHizCulledPatchList()
    { 
        Matrix4x4 openGlProjectionMatrix =  Camera.main.projectionMatrix;
        Matrix4x4 platFormProjectionMatrix = GL.GetGPUProjectionMatrix(openGlProjectionMatrix, false);
        Matrix4x4 worldToCameraMatrix = Camera.main.worldToCameraMatrix;
        Matrix4x4 VPMatrix = platFormProjectionMatrix* worldToCameraMatrix;

        command.SetComputeMatrixParam(CS_GPUDrivenTerrain, ComputeShaderDefine.VPMatrix_P, VPMatrix);
        command.CopyCounterValue(finalList ,mDispatchArgsBuffer,0);
        drawInstanceData[1] = 0;
        command.SetBufferData(mDrawInstanceBuffer, drawInstanceData);


        command.SetComputeBufferParam(CS_GPUDrivenTerrain, KN_HizCull, ComputeShaderDefine.InstanceArgs_P, mDrawInstanceBuffer);
        command.SetComputeBufferParam(CS_GPUDrivenTerrain, KN_HizCull, ComputeShaderDefine.consumeList_P, finalList);
        command.SetComputeTextureParam(CS_GPUDrivenTerrain, KN_HizCull, ComputeShaderDefine.HIZ_MAP_P, TerrainDataManager.GetInstance().HIZ_MAP);
        command.SetComputeTextureParam(CS_GPUDrivenTerrain, KN_HizCull, ComputeShaderDefine.MinMaxHeightMap_P, MinMaxHeightMap);
        command.SetComputeTextureParam(CS_GPUDrivenTerrain, KN_HizCull, ComputeShaderDefine.RenderPatchMap_P, mRenderPatchMap);
        command.SetComputeTextureParam(CS_GPUDrivenTerrain, KN_HizCull, ComputeShaderDefine.SectorLODMap_P, SectorLODMap);
        command.DispatchCompute(CS_GPUDrivenTerrain, KN_HizCull, mDispatchArgsBuffer, 0);

        //此时mRenderPatchMap 写入了经过裁剪后，剩余的patch的列表
        //mDrawInstanceBuffer 中写入了patch的列表的数量
    }

    float[] debuglist = new float[34125];

    public bool CheckRender()
    {
        if(DEBUG)
        {
            return true;
        }
        if(TerrainDataManager.GetInstance().HIZ_MAP != null
            && TerrainDataManager.GetInstance().TerrainMaterial != null
            && MinMaxHeightMap != null)
        {
            return true;
        }
        return false;
    }

    public void UpdateTerrainMaterialParams()
    {
        TerrainDataManager.GetInstance().TerrainMaterial.SetTexture(ComputeShaderDefine._RenderPatchMap_P, mRenderPatchMap);
        TerrainDataManager.GetInstance().TerrainMaterial.SetVectorArray(ComputeShaderDefine.globalValueList_P, globalValue);
    }

    public void UpdateDebugMaterialParams()
    {
        if(DEBUG)
        {
            TerrainDataManager.GetInstance().TerrainDebugMaterial.SetBuffer(ComputeShaderDefine.DebugCubeList, nodeBufferPang);
            TerrainDataManager.GetInstance().TerrainDebugMaterial.SetVectorArray(ComputeShaderDefine.globalValueList_P, globalValue);
        }
    }

    public ComputeBuffer GetDrawInstanceArgsBuffer()
    {
        if(DEBUG)
        {
            mDrawInstanceBuffer.GetData(drawInstanceData);
            Debug.Log("drawInstanceData count:" + drawInstanceData[0] + "|" + drawInstanceData[1]);
        }
        return mDrawInstanceBuffer;
    }

    public ComputeBuffer GetDebugDrawInstanceArgsBuffer()
    {
        mDebugInstanceArgsBuffer.GetData(debugInstanceArgsData);
        Debug.Log("mDebugInstanceArgsBuffer count:"+ debugInstanceArgsData[0] + "|" + debugInstanceArgsData[1]);
        return mDebugInstanceArgsBuffer;
    }

    public void ExecuteCommand()
    {
        Graphics.ExecuteCommandBuffer(command);
    }
    /// <summary>
    /// 绘制PatchInstance 组成的 Terrain
    /// </summary>
    public void DrawTerrainInstance()
    {
        Graphics.DrawMeshInstancedIndirect(quardMesh, 0
            , TerrainDataManager.GetInstance().TerrainMaterial,
            new Bounds(Vector3.zero, new Vector3(TerrainDataManager.REAL_TERRAIN_SIZE, TerrainDataManager.WorldHeightScale, TerrainDataManager.REAL_TERRAIN_SIZE))
            , GetDrawInstanceArgsBuffer());
    }

    public void DrawDebugCubeInstance()
    {
        if (DEBUG)
        {
            Graphics.DrawMeshInstancedIndirect(cubeMesh, 0
            , TerrainDataManager.GetInstance().TerrainDebugMaterial,
            new Bounds(Vector3.zero, new Vector3(TerrainDataManager.REAL_TERRAIN_SIZE, TerrainDataManager.WorldHeightScale, TerrainDataManager.REAL_TERRAIN_SIZE))
            , GetDebugDrawInstanceArgsBuffer());
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
        if (command != null) command.Dispose();

        RenderTexture.ReleaseTemporary(SectorLODMap);
        RenderTexture.ReleaseTemporary(TerrainDataManager.GetInstance().HIZ_MAP); 
        RenderTexture.ReleaseTemporary(mRenderPatchMap);
    }
   
}

