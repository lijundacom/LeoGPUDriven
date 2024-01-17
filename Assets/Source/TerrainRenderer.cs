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

    public Camera mTerrainCamera;

    public Mesh cubeMesh;

    // Start is called before the first frame update
    void Start()
    {
        Application.targetFrameRate = 200;

        TerrainDataManager.GetInstance().Reset();

        TerrainDataManager.GetInstance().CubeMesh = cubeMesh;

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
            mGPUDrivenTerrainImpl.UpdateViewChunk(mTerrainCamera);

            //mGPUDrivenTerrainImpl.DrawTerrainInstance();

            mGPUDrivenTerrainImpl.DrawDebugCubeInstance();
        }
    }

    private void OnDestroy()
    {
        TerrainDataManager.GetInstance().ReleaseResource();

        mGPUDrivenTerrainImpl.Dispose();
    }

}
