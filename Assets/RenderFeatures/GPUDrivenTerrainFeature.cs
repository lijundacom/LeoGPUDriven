using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Rendering.Universal;

public class GPUDrivenTerrainFeature : ScriptableRendererFeature
{
    GPUDrivenTerrainPass mGPUDrivenTerrainPass;
    //GPUDriveDrawInstancePass mGPUDriveDrawInstancePass;

    public override void Create()
    {
        mGPUDrivenTerrainPass = new GPUDrivenTerrainPass();
        mGPUDrivenTerrainPass.renderPassEvent = RenderPassEvent.AfterRenderingTransparents;

        //mGPUDriveDrawInstancePass = new GPUDriveDrawInstancePass();
        //mGPUDriveDrawInstancePass.renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.isSceneViewCamera || renderingData.cameraData.isPreviewCamera)
        {
            //return;
        }

        if (renderingData.cameraData.camera.name != "Main Camera")
        {
            //return;
        }
        renderer.EnqueuePass(mGPUDrivenTerrainPass);
        //renderer.EnqueuePass(mGPUDriveDrawInstancePass);
    }

    protected override void Dispose(bool disposing)
    {

    }

}
