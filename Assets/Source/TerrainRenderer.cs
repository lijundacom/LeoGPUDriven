using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;
using UnityEngine.TerrainUtils;
using UnityEngine.UIElements;

public class TerrainRenderer : MonoBehaviour
{
    GPUDrivenTerrainImpl mGPUDrivenTerrainImpl = new GPUDrivenTerrainImpl();

    public float LodJudgeFector = 100f;

    

    // Start is called before the first frame update
    void Start()
    {
        Application.targetFrameRate = 200;

        TerrainDataManager.GetInstance().Reset();

        mGPUDrivenTerrainImpl.Init();
    }

    private void OnPreRender()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        TerrainDataManager.LodJudgeFector = LodJudgeFector;

        if (mGPUDrivenTerrainImpl.CheckRender())
        {
            mGPUDrivenTerrainImpl.ClearCommandBuffer();

            mGPUDrivenTerrainImpl.SetGlobaleValue();

            //初始化LODMIN时5x5的node
            mGPUDrivenTerrainImpl.CopyInputBuffer();

            //生成四叉树LOD化之后的Node列表
            mGPUDrivenTerrainImpl.CreateLODNodeList();

            //生成记录LOD的sector列表
            mGPUDrivenTerrainImpl.CreateSectorLodMap();

            //视锥裁剪
            mGPUDrivenTerrainImpl.CalFrustumCulledPatchList();

            //Node扩展成Patch
            mGPUDrivenTerrainImpl.CreatePatch();

            //Hiz遮挡剔除
            mGPUDrivenTerrainImpl.CalHizCulledPatchList();

            //执行commandbuffer
            mGPUDrivenTerrainImpl.ExecuteCommand();

            //将ComputeShader的计算结果更新到地形渲染shader
            mGPUDrivenTerrainImpl.UpdateTerrainMaterialParams();

            //绘制地形
            mGPUDrivenTerrainImpl.DrawTerrainInstance();

            //mGPUDrivenTerrainImpl.UpdateDebugMaterialParams();

            //mGPUDrivenTerrainImpl.DrawDebugCubeInstance();
        }
    }

    private void OnDestroy()
    {
        TerrainDataManager.GetInstance().ReleaseResource();

        mGPUDrivenTerrainImpl.Dispose();
    }

}
