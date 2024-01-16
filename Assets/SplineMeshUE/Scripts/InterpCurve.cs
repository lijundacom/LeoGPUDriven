using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public enum InterpCurveMode
{
    Linear,
    CurveAuto,
    Constant,
    CurveUser,
    CurveBreak,
    CurevAutoClamped,
    Unknown
}

public class InterpCurveNode<T>
{
    public float InVal = 0.0f;
    public T OutVal = default(T);
    public T ArriveTangent = default(T);
    public T LeaveTangent = default(T);
    public InterpCurveMode InterpMode = InterpCurveMode.CurveAuto;

    public bool IsCurveKey()
    {
        return InterpMode == InterpCurveMode.CurveAuto
            || InterpMode == InterpCurveMode.CurveUser
            || InterpMode == InterpCurveMode.CurveBreak
            || InterpMode == InterpCurveMode.CurevAutoClamped;
    }
}

/// <summary>
/// 由于C#泛型对比C++泛形缺少很多功能，如T+T这种,C++在编译时能正确指出是否实现+。
/// 而C#就算使用泛形约束，也不能指定实现重载+的类型，然后如局部泛形实例化的功能也没有。
/// 可以使用泛形加继承来实现，父类泛形T，子类继承泛形的实例化(A : T[Vector3])来完成类似功能。
/// 在这我们不使用这种，使用另外一种把相应具体类型有关的操作用委托包装起来，这样也可以
/// 一是用来摆脱具体操作不能使用的局限，二是用来实现C++中的局部泛形实例化。 
/// 照说C#是运行时生成的泛形实例化代码，应该比C++限制更少，但是现实C#因为安全型，
/// 只能使用功能有限的泛形约束。
/// </summary>
/// <typeparam name="T"></typeparam>
public class InterpCurve<T>
{
    public List<InterpCurveNode<T>> Points = new List<InterpCurveNode<T>>();
    public bool IsLooped;
    public float LoopKeyOffset;
    public InterpCurve(int capity = 0)
    {
        for (int i = 0; i < capity; ++i)
        {
            this.Points.Add(new InterpCurveNode<T>());
        }
    }

    public InterpCurveNode<T> this[int index]
    {
        get
        {
            return this.Points[index];
        }
        set
        {
            this.Points[index] = value;
        }
    }

    public void SetLoopKey(float loopKey)
    {
        float lastInKey = Points[Points.Count - 1].InVal;
        if (loopKey < lastInKey)
        {
            IsLooped = true;
            LoopKeyOffset = loopKey - lastInKey;
        }
        else
        {
            IsLooped = false;
        }
    }

    public void ClearLoopKey()
    {
        IsLooped = false;
    }

    /// <summary>
    /// 计算当线曲线的切线
    /// </summary>
    /// <param name="tension"></param>
    /// <param name="bStationaryEndpoints"></param>
    /// <param name="computeFunc"></param>
    /// <param name="subtract"></param>
    public void AutoSetTangents(float tension, bool bStationaryEndpoints, ComputeCurveTangent<T> computeFunc,
        Func<T, T, T> subtract)
    {
        int numPoints = Points.Count;
        int lastPoint = numPoints - 1;
        for (int index = 0; index < numPoints; ++index)
        {
            int preIndex = (index == 0) ? (IsLooped ? lastPoint : 0) : (index - 1);
            int nextIndex = (index == lastPoint) ? (IsLooped ? 0 : lastPoint) : (index + 1);

            var current = Points[index];
            var pre = Points[preIndex];
            var next = Points[nextIndex];

            if (current.InterpMode == InterpCurveMode.CurveAuto
                || current.InterpMode == InterpCurveMode.CurevAutoClamped)
            {
                if (bStationaryEndpoints && (index == 0 ||
                    (index == lastPoint && !IsLooped)))
                {
                    current.ArriveTangent = default(T);
                    current.LeaveTangent = default(T);
                }
                else if (pre.IsCurveKey())
                {
                    bool bWantClamping = (current.InterpMode == InterpCurveMode.CurevAutoClamped);

                    float prevTime = (IsLooped && index == 0) ? (current.InVal - LoopKeyOffset) : pre.InVal;
                    float nextTime = (IsLooped && index == lastPoint) ? (current.InVal + LoopKeyOffset) : next.InVal;
                    T Tangent = computeFunc(prevTime, pre.OutVal, current.InVal, current.OutVal,
                        nextTime, next.OutVal, tension, bWantClamping);

                    current.ArriveTangent = Tangent;
                    current.LeaveTangent = Tangent;
                }
                else
                {
                    current.ArriveTangent = pre.ArriveTangent;
                    current.LeaveTangent = pre.LeaveTangent;
                }
            }
            else if (current.InterpMode == InterpCurveMode.Linear)
            {
                T Tangent = subtract(next.OutVal, current.OutVal);
                current.ArriveTangent = Tangent;
                current.LeaveTangent = Tangent;
            }
            else if (current.InterpMode == InterpCurveMode.Constant)
            {
                current.ArriveTangent = default(T);
                current.LeaveTangent = default(T);
            }
        }
    }

    /// <summary>
    /// 根据当前inVale对应的Node与InterpCurveMode来得到在对应Node上的值
    /// </summary>
    /// <param name="inVal"></param>
    /// <param name="defalutValue"></param>
    /// <param name="lerp"></param>
    /// <param name="cubicInterp"></param>
    /// <returns></returns>
    public T Eval(float inVal, T defalutValue, Func<T, T, float, T> lerp, CubicInterp<T> cubicInterp)
    {
        int numPoints = Points.Count;
        int lastPoint = numPoints - 1;

        if (numPoints == 0)
            return defalutValue;
        int index = GetPointIndexForInputValue(inVal);
        if (index < 0)
            return this[0].OutVal;
        // 如果当前索引是最后索引
        if (index == lastPoint)
        {
            if (!IsLooped)
            {
                return Points[lastPoint].OutVal;
            }
            else if (inVal >= Points[lastPoint].InVal + LoopKeyOffset)
            {
                // Looped spline: last point is the same as the first point
                return Points[0].OutVal;
            }
        }

        //check(Index >= 0 && ((bIsLooped && Index < NumPoints) || (!bIsLooped && Index < LastPoint)));
        bool bLoopSegment = (IsLooped && index == lastPoint);
        int nextIndex = bLoopSegment ? 0 : (index + 1);

        var prevPoint = Points[index];
        var nextPoint = Points[nextIndex];
        //当前段的总长度
        float diff = bLoopSegment ? LoopKeyOffset : (nextPoint.InVal - prevPoint.InVal);

        if (diff > 0.0f && prevPoint.InterpMode != InterpCurveMode.Constant)
        {
            float Alpha = (inVal - prevPoint.InVal) / diff;
            //check(Alpha >= 0.0f && Alpha <= 1.0f);

            if (prevPoint.InterpMode == InterpCurveMode.Linear)
            {
                return lerp(prevPoint.OutVal, nextPoint.OutVal, Alpha);
            }
            else
            {
                return cubicInterp(prevPoint.OutVal, prevPoint.LeaveTangent, nextPoint.OutVal, nextPoint.ArriveTangent, diff, Alpha);
            }
        }
        else
        {
            return Points[index].OutVal;
        }
    }

    /// <summary>
    /// 因为Points可以保证所有点让InVal从小到大排列，故使用二分查找
    /// </summary>
    /// <param name="InValue"></param>
    /// <returns></returns>
    private int GetPointIndexForInputValue(float InValue)
    {
        int NumPoints = Points.Count;
        int LastPoint = NumPoints - 1;
        //check(NumPoints > 0);
        if (InValue < Points[0].InVal)
        {
            return -1;
        }

        if (InValue >= Points[LastPoint].InVal)
        {
            return LastPoint;
        }

        int MinIndex = 0;
        int MaxIndex = NumPoints;

        while (MaxIndex - MinIndex > 1)
        {
            int MidIndex = (MinIndex + MaxIndex) / 2;

            if (Points[MidIndex].InVal <= InValue)
            {
                MinIndex = MidIndex;
            }
            else
            {
                MaxIndex = MidIndex;
            }
        }
        return MinIndex;
    }

    public T EvalDerivative(float InVal, T Default, Func<T, T, float, T> subtract, CubicInterp<T> cubicInterp)
    {
        int NumPoints = Points.Count;
        int LastPoint = NumPoints - 1;

        // If no point in curve, return the Default value we passed in.
        if (NumPoints == 0)
        {
            return Default;
        }

        // Binary search to find index of lower bound of input value
        int Index = GetPointIndexForInputValue(InVal);

        // If before the first point, return its tangent value
        if (Index == -1)
        {
            return Points[0].LeaveTangent;
        }

        // If on or beyond the last point, return its tangent value.
        if (Index == LastPoint)
        {
            if (!IsLooped)
            {
                return Points[LastPoint].ArriveTangent;
            }
            else if (InVal >= Points[LastPoint].InVal + LoopKeyOffset)
            {
                // Looped spline: last point is the same as the first point
                return Points[0].ArriveTangent;
            }
        }

        // Somewhere within curve range - interpolate.
        //check(Index >= 0 && ((bIsLooped && Index < NumPoints) || (!bIsLooped && Index < LastPoint)));
        bool bLoopSegment = (IsLooped && Index == LastPoint);
        int NextIndex = bLoopSegment ? 0 : (Index + 1);

        var PrevPoint = Points[Index];
        var NextPoint = Points[NextIndex];

        float Diff = bLoopSegment ? LoopKeyOffset : (NextPoint.InVal - PrevPoint.InVal);

        if (Diff > 0.0f && PrevPoint.InterpMode != InterpCurveMode.Constant)
        {
            if (PrevPoint.InterpMode == InterpCurveMode.Linear)
            {
                //return (NextPoint.OutVal - PrevPoint.OutVal) / Diff;
                return subtract(NextPoint.OutVal, PrevPoint.OutVal, Diff);
            }
            else
            {
                float Alpha = (InVal - PrevPoint.InVal) / Diff;

                //check(Alpha >= 0.0f && Alpha <= 1.0f);
                //turn FMath::CubicInterpDerivative(PrevPoint.OutVal, PrevPoint.LeaveTangent * Diff, NextPoint.OutVal, NextPoint.ArriveTangent * Diff, Alpha) / Diff;
                return cubicInterp(PrevPoint.OutVal, PrevPoint.LeaveTangent, NextPoint.OutVal, NextPoint.ArriveTangent, Diff, Alpha);
            }
        }
        else
        {
            // Derivative of a constant is zero
            return default(T);
        }
    }


}

