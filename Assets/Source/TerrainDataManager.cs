using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;

/// <summary>
/// 地图尺寸10240*10240
/// </summary>
public class TerrainDataManager 
{
    public static string HEIGHT_MAP_FILE = "Assets/Texture/terrain_stage_texture.png";
    public static string CS_GPU_DRIVEN_TERRAIN = "Assets/ComputeShader/GpuDrivenTerrain.compute";
    public static string CS_BUILD_HIZ_MAP = "Assets/ComputeShader/BuildHizMap.compute";
    public static string GPU_TERRAIN_MATERIAL = "Assets/Shader/Shader Graphs_GPUDrivenHexTerrain.mat";
    public static string MIN_MAX_HEIGHT_MAP = "Assets/Texture/HexMinMaxStageLodMap.asset";
    private static string Addr_CopyDepthTexture = "Assets/Material/Unlit_CopyDepthTexture.mat";
    private static string Addr_TerrainDebugMat = "Assets/Material/TerrainDebug.mat";

    private Texture2D _heightFieldMap;
    private ComputeShader _CS_gpuDrivenTerrain;
    private ComputeShader _CS_BuildHizMap;
    private Texture2D _minMaxHeightMap;
    private Material _TerrainMat;
    private Material _MT_CopyDepthTexture;

    public Mesh CubeMesh;


    public static Vector2Int ChunkNumInLOD0 = new Vector2Int(2048, 2048);

    public static Vector2 LeftBottomHexPtPos = new Vector2(-2047, -1771.89f);

    public static float HexRadius = 1.1547f;

    /// <summary>
    /// 为了适配node的尺寸，让node的数量是整数，需要调整terrain的尺寸。
    /// </summary>
    public static Vector2 CHUNK_ROOT_POS
    {
        get
        {
            return LeftBottomHexPtPos + new Vector2( -1 * HexRadius * math.sqrt(3) * 0.5f, 0);
        }
    }

    /// <summary>
    /// LOD级别包含 0,1,2,3,4,5
    /// </summary>
    public static int MIN_LOD = 9;

    public static int LODRange = 4;

    // <summary>
    /// LOD0 时 一个patch的尺寸是8m x 8m
    /// </summary>
    public static Vector2 LOD0_CHUNK_SIZE = new Vector2(2, 1.5f * 2 / math.sqrt(3));

    /// <summary>
    /// 一个patch的一条边有多少个顶点
    /// </summary>
    public static int PATCH_GRID_NUM = 5;

    public static float STAGE_HEIGHT = 0.5f;

    /// <summary>
    /// 用于调整四叉树的因子
    /// </summary>
    public static float LodJudgeFector = 1f;

    public static float LodDivideLength = 8f;


    public static Vector2Int HIZMapSize = new Vector2Int(2048, 1024);


    public RenderTexture HIZ_MAP;

    private static TerrainDataManager _instance;
    private TerrainDataManager()
    {

    }

    public static TerrainDataManager GetInstance()
    {
        if(_instance == null)
        {
            _instance = new TerrainDataManager();
        }
        return _instance;
    }

    public void Reset()
    {
        _NodeIndexOffsetList.Clear();
    }

    public Material TerrainMaterial
    {
        get
        {
            if(_TerrainMat == null)
            {
                var handler = Addressables.LoadAssetAsync<Material>(GPU_TERRAIN_MATERIAL);
                _TerrainMat = handler.WaitForCompletion();
            }
            return _TerrainMat;
        }
    }

    private Material _TerrainDebugMaterial;
    public Material TerrainDebugMaterial
    {
        get
        {
            if (_TerrainDebugMaterial == null)
            {
                var handler = Addressables.LoadAssetAsync<Material>(Addr_TerrainDebugMat);
                _TerrainDebugMaterial = handler.WaitForCompletion();
            }
            return _TerrainDebugMaterial;
        }
    }

    private Material _DefaultWhiteMaterial;
    public Material DefaultWhiteMaterial
    {
        get
        {
            if(_DefaultWhiteMaterial == null)
            {
                var handler = Addressables.LoadAssetAsync<Material>("Assets/Material/DefaultWhite.mat");
                _DefaultWhiteMaterial = handler.WaitForCompletion();
            }
            return _DefaultWhiteMaterial;
        }
    }

    /// <summary>
    /// 获取当前区域高度图，之后这里要改成流式加载
    /// </summary>
    /// <returns></returns>
    public Texture2D TerrainHeightMap
    {
        get
        {
            if (_heightFieldMap == null)
            {
                var handler = Addressables.LoadAssetAsync<Texture2D>(HEIGHT_MAP_FILE);
                _heightFieldMap = handler.WaitForCompletion();
            }
            return _heightFieldMap;
        }
    }

    public Texture2D TerrainMinMaxHeightMap
    {
        get
        { 
            if(_minMaxHeightMap == null)
            {
                var handler = Addressables.LoadAssetAsync<Texture2D>(MIN_MAX_HEIGHT_MAP);
                _minMaxHeightMap = handler.WaitForCompletion();
            }
            return _minMaxHeightMap;
        }
    }

    /// <summary>
    /// 地形的材质map
    /// </summary>
    /// <returns></returns>
    public Texture GetTerrainMateiralIDMap()
    {
        return null;
    }



    public Vector2Int GetChunkNumInLod(int LOD)
    {
        return new Vector2Int(ChunkNumInLOD0.x >> LOD, ChunkNumInLOD0.y >> LOD);
    }

    /// <summary>
    /// 获取某个LOD级别，在一个维度上PATCH的长度（尺寸），该patch的面积是result*result
    /// </summary>
    /// <param name="LOD"></param>
    /// <returns></returns>
    public Vector2 GetPatchSizeInLod(int LOD)
    {
        return LOD0_CHUNK_SIZE * (1 << LOD);
    }

    /// <summary>
    /// 所有LOD级别的NODE存储到了一个一维数组中，为了方便查找，需要记录每个LOD区间段的起始index
    /// </summary>
    /// <param name="LOD"></param>
    /// <returns></returns>
    public int GetPatchIndexOffset(int LOD)
    {
        int result = 0;
        for(int i= 0; i< LOD; i++)
        {
            Vector2Int nodenum = GetChunkNumInLod(i);
            result += (nodenum.x * nodenum.y);
        }
        return result; 
    }



    private List<int> _NodeIndexOffsetList =new List<int>();

    public List<int> NodeIndexOffsetList
    {
        get { 
            if(_NodeIndexOffsetList.Count == 0)
            {
                for (int i=0;i<=MIN_LOD;i++)
                {
                    _NodeIndexOffsetList.Add(GetPatchIndexOffset(i));
                }
            }
            return _NodeIndexOffsetList;
        }
    }



    public ComputeShader CS_GPUDrivenTerrain
    {
        get
        {
            if(_CS_gpuDrivenTerrain == null)
            {
                var handler = Addressables.LoadAssetAsync<ComputeShader>(CS_GPU_DRIVEN_TERRAIN);
                _CS_gpuDrivenTerrain = handler.WaitForCompletion();
            }
            return _CS_gpuDrivenTerrain;
        }
    }

    public void InitCS_GPUBuildHizMap(Action callback)
    {
        var handler = Addressables.LoadAssetAsync<ComputeShader>(CS_BUILD_HIZ_MAP);
        handler.Completed += (cs) =>{
            _CS_BuildHizMap = cs.Result;
            if (callback!=null) callback();
        };
    }

    public ComputeShader CS_GPUBuildHizMap
    {
        get
        {
            return _CS_BuildHizMap;
        }
    }
    

    public Material MT_CopyDepthTexture
    {
        get
        {
            if(_MT_CopyDepthTexture == null)
            {
                var handler = Addressables.LoadAssetAsync<Material>(Addr_CopyDepthTexture);
                _MT_CopyDepthTexture = handler.WaitForCompletion();
            }
            return _MT_CopyDepthTexture;
        }
    }

    public CommandBuffer GPUCullCMDBuffer;


    public void ReleaseResource()
    {
        if(_heightFieldMap) Addressables.Release(_heightFieldMap);
        if (_CS_gpuDrivenTerrain) Addressables.Release(_CS_gpuDrivenTerrain);
        if (_CS_BuildHizMap) Addressables.Release(_CS_BuildHizMap);
        if (_TerrainMat) Addressables.Release(_TerrainMat);
        if (_minMaxHeightMap) Addressables.Release(_minMaxHeightMap);
        if (_MT_CopyDepthTexture) Addressables.Release(_MT_CopyDepthTexture);
        if (_TerrainDebugMaterial) Addressables.Release(_TerrainDebugMaterial);
    }
}
