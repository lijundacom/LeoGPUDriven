using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

public class ComputeShaderDefine
{
    public static readonly string KN_CopyInputBuffer = "CopyInputBuffer";
    public static readonly string KN_NodeQuadLod = "NodeQuadLod";
    public static readonly string KN_CreateSectorLodMap = "CreateSectorLodMap";
    public static readonly string KN_FrustumCull = "FrustumCull";
    public static readonly string KN_CreatePatch = "CreatePatch";
    public static readonly string KN_HizCull = "HizCull";

    public static readonly int globalValueList_P = Shader.PropertyToID("globalValueList");
    public static readonly int NodeIDOffsetOfLOD_P = Shader.PropertyToID("NodeIDOffsetOfLOD");
    public static readonly int CURRENT_LOD_P = Shader.PropertyToID("CURRENT_LOD");
    public static readonly int VPMatrix_P = Shader.PropertyToID("VPMatrix");
    public static readonly int finalList_P = Shader.PropertyToID("finalList");
    public static readonly int appendList_P = Shader.PropertyToID("appendList");
    public static readonly int consumeList_P = Shader.PropertyToID("consumeList");
    public static readonly int NodeBrunchList_P = Shader.PropertyToID("NodeBrunchList");
    public static readonly int SectorLODMap_P = Shader.PropertyToID("SectorLODMap");
    public static readonly int MinMaxHeightMap_P = Shader.PropertyToID("MinMaxHeightMap");
    public static readonly int HIZ_MAP_P = Shader.PropertyToID("HIZ_MAP");
    public static readonly int RenderPatchMap_P = Shader.PropertyToID("mRenderPatchMap"); 
    public static readonly int InstanceArgs_P = Shader.PropertyToID("instanceArgs");
    public static readonly int DebugArgs_P = Shader.PropertyToID("debugArgs");
    public static readonly int DebugCubeList = Shader.PropertyToID("DebugCubeList");
    public static readonly int _RenderPatchMap_P = Shader.PropertyToID("_RenderPatchMap");

    public static readonly string KN_BuildHizMap = "BuildHizMap";
    public static readonly int InputDepthMap_P = Shader.PropertyToID("inputDepthMap");
    public static readonly int InputDepthMapSize_P = Shader.PropertyToID("inputDepthMapSize");
    public static readonly int HIZ_MAP_Mip0_P = Shader.PropertyToID("HIZ_MAP_Mip0");
    public static readonly int HIZ_MAP_Mip1_P = Shader.PropertyToID("HIZ_MAP_Mip1");
    public static readonly int HIZ_MAP_Mip2_P = Shader.PropertyToID("HIZ_MAP_Mip2");
    public static readonly int HIZ_MAP_Mip3_P = Shader.PropertyToID("HIZ_MAP_Mip3");

    public static readonly RenderTargetIdentifier CameraDepthTexture = "_CameraDepthTexture";
    
}

