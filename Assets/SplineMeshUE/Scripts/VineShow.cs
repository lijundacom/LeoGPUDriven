using UnityEngine;
using System.Collections;

public class VineShow : MonoBehaviour
{
    public AnimationCurve curve = null;
    private UESpline spline = null;
    private UESplineMesh splineMesh = null;
    // Use this for initialization
    void Start()
    {
        spline = GetComponentInChildren<UESpline>();
        splineMesh = GetComponentInChildren<UESplineMesh>();
        spline.SceneUpdate();
        if (curve == null || curve.length == 0)
        {
            curve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(6, 1));
        }
    }

    // Update is called once per frame
    void Update()
    {
        float t = Time.time % curve.keys[curve.length - 1].time;
        var growth = curve.Evaluate(t);
        float length = spline.GetSplineLenght();

        var start = 0.18f * growth;
        float scale = Mathf.Lerp(0.5f, 1.0f, growth);
        UpdateMeshParam(start * length, scale, ref splineMesh.param.StartPos, ref splineMesh.param.StartTangent);
        UpdateMeshParam(growth * length, scale, ref splineMesh.param.EndPos, ref splineMesh.param.EndTangent);
        splineMesh.SetShaderParam();
    }

    public void UpdateMeshParam(float key, float scale, ref Vector3 position, ref Vector3 direction)
    {
        var pos = this.spline.GetPosition(key);
        var dir = this.spline.GetDirection(key);

        position = splineMesh.transform.worldToLocalMatrix * InterpHelp.Vector3To4(pos);
        direction = (splineMesh.transform.worldToLocalMatrix * dir) * scale;
    }
}
