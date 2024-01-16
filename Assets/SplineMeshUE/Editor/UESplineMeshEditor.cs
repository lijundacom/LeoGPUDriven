using UnityEngine;
using System.Collections;
using UnityEditor;

[CustomEditor(typeof(UESplineMesh))]
public class UESplineMeshEditor : Editor
{
    private UESplineMesh splineMesh = null;

    void OnEnable()
    {
        splineMesh = target as UESplineMesh;
    }

    void OnSceneGUI()
    {
        if (splineMesh == null)
            return;
        if (splineMesh.ShowPosition)
        {
            splineMesh.start.position = Handles.PositionHandle(splineMesh.start.position, splineMesh.start.rotation);
            splineMesh.end.position = Handles.PositionHandle(splineMesh.end.position, splineMesh.end.rotation);
        }
        if (splineMesh.ShowRotation)
        {
            splineMesh.start.rotation = Handles.RotationHandle(splineMesh.start.rotation, splineMesh.start.position);
            splineMesh.end.rotation = Handles.RotationHandle(splineMesh.end.rotation, splineMesh.end.position);
        }
        splineMesh.UpdateSplineMesh();
    }
}
