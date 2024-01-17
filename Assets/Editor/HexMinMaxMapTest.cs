using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class HexMinMaxMapTest : EditorWindow
{

    public Texture2D mHeightMap;

    public Texture2D HexStageMap;

    public Vector3Int XYMip;

    public Vector2Int HexPt;

    [MenuItem("GPUDriven/HexMinMaxMapTest")]
    public static void CreateEditorWindow()
    {
        EditorWindow.GetWindow(typeof(HexMinMaxMapTest));
    }

    public void OnGUI()
    {
        EditorGUILayout.BeginVertical();
        mHeightMap = EditorGUILayout.ObjectField(new GUIContent("高度图"), mHeightMap, typeof(Texture2D), true) as Texture2D;
        
        XYMip = EditorGUILayout.Vector3IntField(new GUIContent("输出MinMaxHeighMap mip0的分辨率"), XYMip);

        if (GUILayout.Button("Test"))
        {
            Test();
        }

        HexStageMap = EditorGUILayout.ObjectField(new GUIContent("Hex Stage Map"), HexStageMap, typeof(Texture2D), true) as Texture2D;
        HexPt = EditorGUILayout.Vector2IntField(new GUIContent("HexPt"), HexPt);

        if(GUILayout.Button("Test Hex Stage Map"))
        {
            TestHexStageMap();
        }
        EditorGUILayout.EndVertical();
    }

    public void TestHexStageMap()
    {
        Color color0, color1, color2;

        if ((HexPt.y & 0x01) == 0)
        {
            Vector2Int hexID0 = new Vector2Int(HexPt.x, HexPt.y);
            Vector2Int hexID1 = new Vector2Int(HexPt.x, HexPt.y + 1);
            Vector2Int hexID2 = new Vector2Int(HexPt.x - 1, HexPt.y + 1);

            color0 = HexStageMap.GetPixel(hexID0.x, hexID0.y);
            color1 = HexStageMap.GetPixel(hexID1.x, hexID1.y);
            color2 = HexStageMap.GetPixel(hexID2.x, hexID2.y);
        }
        else
        {
            Vector2Int hexID0 = new Vector2Int(HexPt.x, HexPt.y);
            Vector2Int hexID1 = new Vector2Int(HexPt.x - 1, HexPt.y);
            Vector2Int hexID2 = new Vector2Int(HexPt.x, HexPt.y + 1);

            color0 = HexStageMap.GetPixel(hexID0.x, hexID0.y);
            color1 = HexStageMap.GetPixel(hexID1.x, hexID1.y);
            color2 = HexStageMap.GetPixel(hexID2.x, hexID2.y);
        }

        int stage0, stage1, stage2;

        stage0 = (int)(color0.r * 255 - 127);
        stage1 = (int)(color1.r * 255 - 127);
        stage2 = (int)(color2.r * 255 - 127);

        int minStage = math.min(stage0, math.min(stage1, stage2));
        int maxStage = math.max(stage0, math.max(stage1, stage2));
        uint LOD = 0;
        if (minStage == maxStage)
        {
            LOD = 1;
        }
        uint MAXBit = (1 << 16) - 1;
        float result = (((uint)(minStage + 31) << 10) | ((uint)(maxStage + 31) << 4) | LOD) * (1.0f / MAXBit);
        Debug.Log("minStage:"+ minStage + ", maxStage:"+ maxStage + ", LODLimit:"+ LOD + ",result:"+ result);
    }

    public void Test()
    {
        
        StringBuilder sb = new StringBuilder();
        Color color = mHeightMap.GetPixel(XYMip.x, XYMip.y, XYMip.z);
        var data0 = DecodeData(color);
        sb.AppendLine("minStage:"+ data0.x + ",maxStage:"+ data0.y + ", LimitLOD: "+ data0.z + "color:"+ color.r);

        Color color1 = mHeightMap.GetPixel(XYMip.x * 2, XYMip.y * 2, XYMip.z - 1);
        Color color2 = mHeightMap.GetPixel(XYMip.x * 2+ 1, XYMip.y * 2, XYMip.z - 1);
        Color color3 = mHeightMap.GetPixel(XYMip.x * 2, XYMip.y * 2+ 1, XYMip.z - 1);
        Color color4 = mHeightMap.GetPixel(XYMip.x * 2+ 1, XYMip.y * 2+ 1, XYMip.z - 1);

        var data1 = DecodeData(color1);
        var data2 = DecodeData(color2);
        var data3 = DecodeData(color3);
        var data4 = DecodeData(color4);

        sb.AppendLine("minStage:" + data1.x + ",maxStage:" + data1.y + ", LimitLOD: " + data1.z + "color:" + color1.r);
        sb.AppendLine("minStage:" + data2.x + ",maxStage:" + data2.y + ", LimitLOD: " + data2.z + "color:" + color2.r);
        sb.AppendLine("minStage:" + data3.x + ",maxStage:" + data3.y + ", LimitLOD: " + data3.z + "color:" + color3.r);
        sb.AppendLine("minStage:" + data4.x + ",maxStage:" + data4.y + ", LimitLOD: " + data4.z + "color:" + color4.r);
        Debug.Log(sb.ToString());
    }

    public Vector3Int DecodeData(Color color)
    {
        uint data = (uint)math.round(color.r * ((1 << 16) - 1));
        uint minStage = (data >> 10) & 0x3f;
        uint maxStage = (data >> 4) & 0x3f;
        uint LimitLOD = data & 0xf;
        return new Vector3Int((int)minStage - 31, (int)maxStage - 31, (int)LimitLOD);
    }
}
