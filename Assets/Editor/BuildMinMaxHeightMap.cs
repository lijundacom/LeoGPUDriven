using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;
using static Codice.Client.BaseCommands.BranchExplorer.Layout.BrExLayout;

public class BuildMinMaxHeightMap : EditorWindow
{
    public Texture2D mHeightMap;

    public int mOutputMipCount;

    public Vector2Int mOutputTextureSize;

    public string mOutputFileName;

    [MenuItem("GPUDriven/BuildMinMaxHeightMap")]
    public static void CreateEditorWindow()
    {
        EditorWindow.GetWindow(typeof(BuildMinMaxHeightMap));
    }

    public void OnGUI()
    {
        EditorGUILayout.BeginVertical();
        mHeightMap = EditorGUILayout.ObjectField(new GUIContent("高度图"), mHeightMap, typeof(Texture2D), true) as Texture2D;
        mOutputFileName = EditorGUILayout.TextField(new GUIContent("MinMaxHeighMap输出文件名，不需后缀"), mOutputFileName);
        mOutputTextureSize = EditorGUILayout.Vector2IntField(new GUIContent("输出MinMaxHeighMap mip0的分辨率"), mOutputTextureSize);
        mOutputMipCount = EditorGUILayout.IntField(new GUIContent("输出的mip层数"), mOutputMipCount);
        if (GUILayout.Button("生成"))
        {
            BuildMinMaxMap();
        }
        EditorGUILayout.EndVertical();
    }

    RenderTexture outputRT;

    List<RenderTexture> rtlist = new List<RenderTexture>();
    /// <summary>
    /// 1280x1280、640x640 320x320 160x160 80x80 40x40 20x20 10x10 5x5
    /// </summary>
    private void BuildMinMaxMap()
    {
        rtlist.Clear();

        RenderTextureDescriptor outputRTDesc = new RenderTextureDescriptor(mOutputTextureSize.x, mOutputTextureSize.y, RenderTextureFormat.RGFloat, 0, 1);
        outputRTDesc.autoGenerateMips = false;
        outputRTDesc.useMipMap = true;
        outputRTDesc.mipCount = mOutputMipCount;
        outputRTDesc.enableRandomWrite = true;
        outputRT = RenderTexture.GetTemporary(outputRTDesc);
        outputRT.filterMode = FilterMode.Point;
        outputRT.Create();

        ComputeShader computeShader = CS_BuildMinMaxHeightMap;
        int KN_BuildMinMaxHeightMapByHeightMap = computeShader.FindKernel("BuildMinMaxHeightMapByHeightMap");
        int KN_BuildMinMaxHeightMapByMinMaxHeightMap = computeShader.FindKernel("BuildMinMaxHeightMapByMinMaxHeightMap");

        computeShader.SetInts("srcTexSize",new int[2] { mHeightMap.width, mHeightMap.height });
        computeShader.SetInts("destTexSize", new int[2] { mOutputTextureSize.x, mOutputTextureSize.y});


        computeShader.SetTexture(KN_BuildMinMaxHeightMapByHeightMap, Shader.PropertyToID("heightMap"), mHeightMap);
        computeShader.SetTexture(KN_BuildMinMaxHeightMapByHeightMap, Shader.PropertyToID("outputMinMaxHeightMap"), outputRT, 0);

        computeShader.Dispatch(KN_BuildMinMaxHeightMapByHeightMap, mOutputTextureSize.x/16, mOutputTextureSize.y/16, 1);
        
        for (int i=1;i< mOutputMipCount;i++)
        {
            Vector2Int destTexSize = new Vector2Int(mOutputTextureSize.x >> i, mOutputTextureSize.y >>i);

            RenderTextureDescriptor inputRTDesc = new RenderTextureDescriptor(destTexSize.x * 2, destTexSize.y * 2, RenderTextureFormat.RGFloat, 0, 1);
            inputRTDesc.enableRandomWrite = true;
            inputRTDesc.autoGenerateMips = false;
            RenderTexture inputRT = RenderTexture.GetTemporary(inputRTDesc);
            inputRT.filterMode = FilterMode.Point;
            inputRT.Create();

            Graphics.CopyTexture(outputRT,0,i-1, inputRT,0,0);

            computeShader.SetInts("srcTexSize", new int[2] { destTexSize.x * 2, destTexSize.y * 2 });
            computeShader.SetInts("destTexSize", new int[2] { destTexSize.x, destTexSize.y });

            computeShader.SetTexture(KN_BuildMinMaxHeightMapByMinMaxHeightMap, Shader.PropertyToID("inputMinMaxHeightMap"), inputRT);
            computeShader.SetTexture(KN_BuildMinMaxHeightMapByMinMaxHeightMap, Shader.PropertyToID("outputMinMaxHeightMap"), outputRT, i);

            computeShader.Dispatch(KN_BuildMinMaxHeightMapByMinMaxHeightMap, destTexSize.x, destTexSize.y, 1);

            rtlist.Add(inputRT);
        }

        Texture2D texture2D = new Texture2D(outputRT.width, outputRT.height, TextureFormat.RGFloat, mOutputMipCount, false);
        texture2D.filterMode = FilterMode.Point;

        List<int> readResult = new List<int>();
        for (int i = 0; i < mOutputMipCount; i++)
        {
            ReadRenderTexture(outputRT, texture2D, i, mOutputMipCount, readResult, () => {
                AssetDatabase.CreateAsset(texture2D,"Assets/Texture/"+mOutputFileName + ".asset");
                AssetDatabase.Refresh();
                Dispose();
            });
        }
    }

    private void ReadRenderTexture(RenderTexture renderTexture, Texture2D tex2D, int mip, int mipcount,List<int> readResult, Action callback)
    {
        AsyncGPUReadback.Request(renderTexture, mip, (req) => {
            tex2D.SetPixelData(req.GetData<Vector2>(), mip);
            readResult.Add(mip);
            if(readResult.Count == mipcount) { 
                callback();
            }
        });
    }

    private void Dispose()
    {
        Addressables.Release(CS_BuildMinMaxHeightMap);
        RenderTexture.ReleaseTemporary(outputRT);
        foreach (var rt in rtlist)
        {
            RenderTexture.ReleaseTemporary(rt);
        }
    }

    private string BuildMinMaxHeightMapCS = "Assets/ComputeShader/BuildMinMaxHeightMap.compute";
    public ComputeShader CS_BuildMinMaxHeightMap
    {
        get
        {
            var handler = Addressables.LoadAssetAsync<ComputeShader>(BuildMinMaxHeightMapCS);
            ComputeShader cs = handler.WaitForCompletion();
            return cs;
        }
    }
}
