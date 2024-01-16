using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

// Evaluate the length of a Hermite spline segment.
// This calculates the integral of |dP/dt| dt, where P(t) is the spline equation with components (x(t), y(t), z(t)).
// This isn't solvable analytically, so we use a numerical method (Legendre-Gauss quadrature) which performs very well
// with functions of this type, even with very few samples.  In this case, just 5 samples is sufficient to yield a
// reasonable result.
struct LegendreGaussCoefficient
{
    public float Abscissa;
    public float Weight;

    public LegendreGaussCoefficient(float abscissa, float weight)
    {
        this.Abscissa = abscissa;
        this.Weight = weight;
    }
}

[System.Serializable]
public class SplineNode
{
    [HideInInspector]
    public float Key = 0.0f;
    [HideInInspector]
    public GameObject node = null;
    public InterpCurveMode InterpMode = InterpCurveMode.CurveAuto;
}

[ExecuteInEditMode]
public class UESpline : MonoBehaviour
{
    public InterpCurve<Vector3> SplineInfo;
    public InterpCurve<Quaternion> SplineRotInfo;
    public InterpCurve<Vector3> SplineScaleInfo;
    public InterpCurve<float> SplineReparamTable;

    public int Steps = 4;
    public float Duration;
    public bool IsLoop = false;
    //最后顶点切线强制为0，如果是loop，以第一个顶点
    public bool StationaryEndpoints = false;
    public Vector3 DefaultUp = Vector3.up;

    public SplineNode[] childObjects;

    public bool ShowPosition = true;
    public bool ShowRotation = false;
    public bool ShowScale = false;
    public bool DrawNode = true;

    private LegendreGaussCoefficient[] LegendreGaussCoefficients =
    {
        new LegendreGaussCoefficient( 0.0f, 0.5688889f),
        new LegendreGaussCoefficient( -0.5384693f, 0.47862867f),
        new LegendreGaussCoefficient( 0.5384693f, 0.47862867f ),
        new LegendreGaussCoefficient( -0.90617985f, 0.23692688f),
        new LegendreGaussCoefficient( 0.90617985f, 0.23692688f )
    };

    void Start()
    {
        this.SceneUpdate();
    }

    /// <summary>
    /// 当节点下的gameobject变动后，重新更新所有数据
    /// </summary>
    public void SceneUpdate()
    {
        int nodeCount = this.childObjects.Length;
        if (this.SplineInfo == null || nodeCount != this.SplineInfo.Points.Count)
        {
            this.InitSpline(nodeCount);
        }
        for (int i = 0; i < nodeCount; ++i)
        {
            this.SplineInfo[i].OutVal = this.childObjects[i].node.transform.localPosition;
            this.SplineInfo[i].InterpMode = this.childObjects[i].InterpMode;
            this.SplineRotInfo[i].OutVal = this.childObjects[i].node.transform.localRotation;
            this.SplineScaleInfo[i].OutVal = this.childObjects[i].node.transform.localScale;
        }
        this.UpdateSpline();
    }

    public void InitSpline(int count)
    {
        this.SplineInfo = new InterpCurve<Vector3>(count);
        this.SplineRotInfo = new InterpCurve<Quaternion>(count);
        this.SplineScaleInfo = new InterpCurve<Vector3>(count);

        for (int i = 0; i < count; ++i)
        {
            this.SplineRotInfo[i].ArriveTangent = Quaternion.identity;
            this.SplineRotInfo[i].LeaveTangent = Quaternion.identity;
        }
    }

    public void UpdateSpline()
    {
        int numPoints = this.SplineInfo.Points.Count;
        //按顺序重新设定inVal
        for (int i = 0; i < numPoints; ++i)
        {
            this.SplineInfo[i].InVal = i;
            this.SplineRotInfo[i].InVal = i;
            this.SplineScaleInfo[i].InVal = i;
        }

        if (numPoints < 2)
            return;

        if (this.IsLoop)
        {
            this.SplineInfo.SetLoopKey(numPoints);
            this.SplineRotInfo.SetLoopKey(numPoints);
            this.SplineScaleInfo.SetLoopKey(numPoints);
        }
        else
        {
            this.SplineInfo.ClearLoopKey();
            this.SplineRotInfo.ClearLoopKey();
            this.SplineScaleInfo.ClearLoopKey();
        }

        //自动设定Tangent值
        this.SplineInfo.AutoSetTangents(0.0f, StationaryEndpoints,
            InterpHelp.ComputeCurveTangentVector, (Vector3 a, Vector3 b) => { return a - b; });
        this.SplineRotInfo.AutoSetTangents(0.0f, StationaryEndpoints,
            InterpHelp.ComputeCurveTangentQuat, InterpHelp.Subtract);
        this.SplineScaleInfo.AutoSetTangents(0.0f, StationaryEndpoints,
            InterpHelp.ComputeCurveTangentVector, (Vector3 a, Vector3 b) => { return a - b; });

        //重新设定所有插值点
        int segments = IsLoop ? numPoints : numPoints - 1;
        this.SplineReparamTable = new InterpCurve<float>();
        float accumulatedLength = 0.0f;
        for (int segmentIndex = 0; segmentIndex < segments; ++segmentIndex)
        {
            for (int step = 0; step < Steps; ++step)
            {
                float param = (float)step / Steps;
                float segmentLength = (step == 0) ? 0.0f : GetSegmentLength(segmentIndex, param);

                InterpCurveNode<float> value = new InterpCurveNode<float>();
                value.InVal = segmentLength + accumulatedLength;
                value.OutVal = segmentIndex + param;
                value.LeaveTangent = 0.0f;
                value.ArriveTangent = 0.0f;
                value.InterpMode = InterpCurveMode.Linear;
                SplineReparamTable.Points.Add(value);
            }
            accumulatedLength += GetSegmentLength(segmentIndex, 1.0f);
        }
        InterpCurveNode<float> last = new InterpCurveNode<float>();
        last.InVal = accumulatedLength;
        last.OutVal = segments;
        last.LeaveTangent = 0.0f;
        last.ArriveTangent = 0.0f;
        last.InterpMode = InterpCurveMode.Linear;
        SplineReparamTable.Points.Add(last);
    }

    /// <summary>
    /// 得到每段对应位置在其中的长度
    /// </summary>
    /// <param name="Index"></param>
    /// <param name="Param"></param>
    /// <returns></returns>
    public float GetSegmentLength(int Index, float Param)
    {
        int NumPoints = SplineInfo.Points.Count;
        int LastPoint = NumPoints - 1;
        //check(Index >= 0 && ((bClosedLoop && Index<NumPoints) || (!bClosedLoop && Index<LastPoint)));
        //check(Param >= 0.0f && Param <= 1.0f);

        var StartPoint = SplineInfo.Points[Index];
        var EndPoint = SplineInfo.Points[Index == LastPoint ? 0 : Index + 1];

        //check(Index == LastPoint || (static_cast<int32>(EndPoint.InVal) - static_cast<int32>(StartPoint.InVal) == 1));

        var P0 = StartPoint.OutVal;
        var T0 = StartPoint.LeaveTangent;
        var P1 = EndPoint.OutVal;
        var T1 = EndPoint.ArriveTangent;

        // Cache the coefficients to be fed into the function to calculate the spline derivative at each sample point as they are constant.
        Vector3 Coeff1 = ((P0 - P1) * 2.0f + T0 + T1) * 3.0f;
        Vector3 Coeff2 = (P1 - P0) * 6.0f - T0 * 4.0f - T1 * 2.0f;
        Vector3 Coeff3 = T0;

        float HalfParam = Param * 0.5f;

        float Length = 0.0f;
        foreach (var legendre in LegendreGaussCoefficients)
        {
            // Calculate derivative at each Legendre-Gauss sample, and perform a weighted sum
            float Alpha = HalfParam * (1.0f + legendre.Abscissa);
            Vector3 Derivative = Vector3.Scale(((Coeff1 * Alpha + Coeff2) * Alpha + Coeff3), transform.localToWorldMatrix * transform.localScale);
            Length += Derivative.magnitude * legendre.Weight;
        }
        Length *= HalfParam;
        return Length;
    }

    public float GetSplineLenght()
    {
        if (this.SplineReparamTable != null && this.SplineReparamTable.Points.Count > 0)
        {
            int count = this.SplineReparamTable.Points.Count;
            return this.SplineReparamTable[count - 1].InVal;
        }
        return 0.0f;
    }

    public Vector3 GetPosition(float dist, bool bWorld = true)
    {
        float param = this.SplineReparamTable.Eval(dist, 0.0f, Mathf.Lerp, InterpHelp.CubicInterpFloat);
        var pos = this.SplineInfo.Eval(param, Vector3.zero, Vector3.Lerp, InterpHelp.CubicInterpVector);
        if (bWorld)
        {
            pos = this.transform.localToWorldMatrix * InterpHelp.Vector3To4(pos);
        }
        return pos;
    }

    public Vector3 GetDirection(float dist, bool bWorld = true)
    {
        float param = this.SplineReparamTable.Eval(dist, 0.0f, Mathf.Lerp, InterpHelp.CubicInterpFloat);
        var direction = this.SplineInfo.EvalDerivative(param, Vector3.zero, InterpHelp.SubtraceScale, InterpHelp.DerivativeVector);
        if (bWorld)
        {
            direction = this.transform.localToWorldMatrix * direction;
        }
        return direction;
    }

    void OnDrawGizmos()//Selected()
    {
        if (DrawNode && SplineReparamTable != null)
        {
            Vector3 preVector3 = Vector3.zero;
            float keyCount = SplineReparamTable.Points.Count;
            float lenght = GetSplineLenght();
            for (int i = 0; i < keyCount; ++i)
            {
                float distance = (float)i * lenght / keyCount;
                float param = SplineReparamTable.Eval(distance, 0.0f, Mathf.Lerp, InterpHelp.CubicInterpFloat);
                var pos = this.SplineInfo.Eval(SplineReparamTable[i].OutVal, Vector3.zero, Vector3.Lerp, InterpHelp.CubicInterpVector);
                //var rot = this.SplineRotInfo.Eval(key, Quaternion.identity, Quaternion.Lerp, CubicInterpQuat);

                var toWorld = this.transform.localToWorldMatrix;
                //直接用matrix4*4*vector3,会丢掉translation信息，因为vector3->vector4,w默认是0
                var pos4 = new Vector4(pos.x, pos.y, pos.z, 1.0f);
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(toWorld * pos4, 0.02f);
                Gizmos.color = Color.green;
                if (i > 0)
                {
                    var prePos4 = new Vector4(preVector3.x, preVector3.y, preVector3.z, 1.0f);
                    Gizmos.DrawLine(toWorld * prePos4, toWorld * pos4);
                }
                preVector3 = pos;
            }
        }
    }
}
