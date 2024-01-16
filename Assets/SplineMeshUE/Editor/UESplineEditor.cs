using UnityEngine;
using System.Collections;
using UnityEditor;

[CustomEditor(typeof(UESpline))]
public class UESplineEditor : Editor
{
    private UESpline spline = null;

    void OnEnable()
    {
        spline = target as UESpline;
    }

    void OnSceneGUI()
    {        
        var childCount = spline.transform.childCount;
        bool bChange = spline.childObjects == null || spline.childObjects.Length != childCount;
        if (bChange)
            spline.childObjects = new SplineNode[childCount];
        for (int i = 0; i < childCount; i++)
        {
            if (bChange)
                spline.childObjects[i] = new SplineNode();
            if (spline.childObjects[i].node == null)
                spline.childObjects[i].node = spline.transform.GetChild(i).gameObject;
            if (spline.ShowPosition)
                spline.childObjects[i].node.transform.position = Handles.PositionHandle(spline.childObjects[i].node.transform.position, spline.childObjects[i].node.transform.rotation);
            if (spline.ShowRotation)
                spline.childObjects[i].node.transform.rotation = Handles.RotationHandle(spline.childObjects[i].node.transform.rotation, spline.childObjects[i].node.transform.position);
            if (spline.ShowScale)
                spline.childObjects[i].node.transform.localScale = Handles.ScaleHandle(spline.childObjects[i].node.transform.localScale,
                    spline.childObjects[i].node.transform.position, spline.childObjects[i].node.transform.rotation, 5.0f);
        }
        spline.SceneUpdate();
    }
}
