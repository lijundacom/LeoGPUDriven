using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class GPUDrivenTerrainPass : ScriptableRenderPass
{
    public GPUDrivenTerrainPass()
    {

    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if(TerrainDataManager.GetInstance().GPUCullCMDBuffer != null)
        {
            context.ExecuteCommandBuffer(TerrainDataManager.GetInstance().GPUCullCMDBuffer);
        }
    }

    
}
