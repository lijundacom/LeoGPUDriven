using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;


public delegate T ComputeCurveTangent<T>(float PrevTime, T PrevPoint,
                            float CurTime, T CurPoint,
                            float NextTime, T NextPoint,
                            float Tension,
                            bool bWantClamping);
public delegate T CubicInterp<T>(T PrevPoint, T PrevPointLeaveTangent,
                            T NextPoint, T NextPointArriveTange,
                            float Diff,
                            float t);
public class InterpHelp
{
    /// <summary>
    /// InterpCurve T(Vector3)的泛形求切线实例化版本
    /// </summary>
    /// <param name="PrevTime"></param>
    /// <param name="PrevPoint"></param>
    /// <param name="CurTime"></param>
    /// <param name="CurPoint"></param>
    /// <param name="NextTime"></param>
    /// <param name="NextPoint"></param>
    /// <param name="Tension"></param>
    /// <param name="bWantClamping"></param>
    /// <returns></returns>
    public static Vector3 ComputeCurveTangentVector(float PrevTime, Vector3 PrevPoint,
                            float CurTime, Vector3 CurPoint,
                            float NextTime, Vector3 NextPoint,
                            float Tension,
                            bool bWantClamping)
    {
        if (bWantClamping)
        {
            Vector3 outTangent = Vector3.zero;
            for (int i = 0; i < 3; ++i)
            {
                outTangent[i] = (1.0f - Tension) * ClampFloatTangent(PrevTime, PrevPoint[i], CurTime, CurPoint[i], NextTime, NextPoint[i]);
            }
            return outTangent;
        }
        else
        {
            var OutTan = (1.0f - Tension) * ((CurPoint - PrevPoint) + (NextPoint - CurPoint));
            float PrevToNextTimeDiff = Mathf.Max(0.0001f, NextTime - PrevTime);
            return OutTan / PrevToNextTimeDiff;
        }
    }

    public static float ClampFloatTangent(float PrevPointVal, float PrevTime, float CurPointVal, float CurTime, float NextPointVal, float NextTime)
    {
        float small = 0.0001f;
        float PrevToNextTimeDiff = Mathf.Max(small, NextTime - PrevTime);
        float PrevToCurTimeDiff = Mathf.Max(small, CurTime - PrevTime);
        float CurToNextTimeDiff = Mathf.Max(small, NextTime - CurTime);

        float OutTangentVal = 0.0f;

        float PrevToNextHeightDiff = NextPointVal - PrevPointVal;
        float PrevToCurHeightDiff = CurPointVal - PrevPointVal;
        float CurToNextHeightDiff = NextPointVal - CurPointVal;

        // Check to see if the current point is crest
        if ((PrevToCurHeightDiff >= 0.0f && CurToNextHeightDiff <= 0.0f) ||
            (PrevToCurHeightDiff <= 0.0f && CurToNextHeightDiff >= 0.0f))
        {
            // Neighbor points are both both on the same side, so zero out the tangent
            OutTangentVal = 0.0f;
        }
        else
        {
            // The three points form a slope

            // Constants
            float ClampThreshold = 0.333f;

            // Compute height deltas
            float CurToNextTangent = CurToNextHeightDiff / CurToNextTimeDiff;
            float PrevToCurTangent = PrevToCurHeightDiff / PrevToCurTimeDiff;
            float PrevToNextTangent = PrevToNextHeightDiff / PrevToNextTimeDiff;

            // Default to not clamping
            float UnclampedTangent = PrevToNextTangent;
            float ClampedTangent = UnclampedTangent;

            float LowerClampThreshold = ClampThreshold;
            float UpperClampThreshold = 1.0f - ClampThreshold;

            // @todo: Would we get better results using percentange of TIME instead of HEIGHT?
            float CurHeightAlpha = PrevToCurHeightDiff / PrevToNextHeightDiff;

            if (PrevToNextHeightDiff > 0.0f)
            {
                if (CurHeightAlpha < LowerClampThreshold)
                {
                    // 1.0 = maximum clamping (flat), 0.0 = minimal clamping (don't touch)
                    float ClampAlpha = 1.0f - CurHeightAlpha / ClampThreshold;
                    float LowerClamp = Mathf.Lerp(PrevToNextTangent, PrevToCurTangent, ClampAlpha);
                    ClampedTangent = Mathf.Min(ClampedTangent, LowerClamp);
                }

                if (CurHeightAlpha > UpperClampThreshold)
                {
                    // 1.0 = maximum clamping (flat), 0.0 = minimal clamping (don't touch)
                    float ClampAlpha = (CurHeightAlpha - UpperClampThreshold) / ClampThreshold;
                    float UpperClamp = Mathf.Lerp(PrevToNextTangent, CurToNextTangent, ClampAlpha);
                    ClampedTangent = Mathf.Min(ClampedTangent, UpperClamp);
                }
            }
            else
            {

                if (CurHeightAlpha < LowerClampThreshold)
                {
                    // 1.0 = maximum clamping (flat), 0.0 = minimal clamping (don't touch)
                    float ClampAlpha = 1.0f - CurHeightAlpha / ClampThreshold;
                    float LowerClamp = Mathf.Lerp(PrevToNextTangent, PrevToCurTangent, ClampAlpha);
                    ClampedTangent = Mathf.Max(ClampedTangent, LowerClamp);
                }

                if (CurHeightAlpha > UpperClampThreshold)
                {
                    // 1.0 = maximum clamping (flat), 0.0 = minimal clamping (don't touch)
                    float ClampAlpha = (CurHeightAlpha - UpperClampThreshold) / ClampThreshold;
                    float UpperClamp = Mathf.Lerp(PrevToNextTangent, CurToNextTangent, ClampAlpha);
                    ClampedTangent = Mathf.Max(ClampedTangent, UpperClamp);
                }
            }

            OutTangentVal = ClampedTangent;
        }

        return OutTangentVal;
    }


    /// <summary>
    /// InterpCurve T(Quaternion)的泛形求切线实例化版本
    /// </summary>
    /// <param name="PrevTime"></param>
    /// <param name="PrevPoint"></param>
    /// <param name="CurTime"></param>
    /// <param name="CurPoint"></param>
    /// <param name="NextTime"></param>
    /// <param name="NextPoint"></param>
    /// <param name="Tension"></param>
    /// <param name="bWantClamping"></param>
    /// <returns></returns>
    public static Quaternion ComputeCurveTangentQuat(float PrevTime, Quaternion PrevPoint,
                        float CurTime, Quaternion CurPoint,
                        float NextTime, Quaternion NextPoint,
                        float Tension,
                        bool bWantClamping)
    {
        Quaternion InvP = Quaternion.Inverse(CurPoint);
        Quaternion Part1 = Log(InvP * PrevPoint); // (InvP * PrevP).Log();
        Quaternion Part2 = Log(InvP * NextPoint);

        Quaternion PreExp = Add(Part1, Part2, -0.5f);

        var OutTan = CurPoint * Exp(PreExp);
        return OutTan;
    }

    /// <summary>
    /// InterpCurve T(Vector3)的泛形Curve Lerp版本
    /// </summary>
    /// <param name="P0">包含P0的Segment开始位置</param>
    /// <param name="T0">包含P0的Segment开始切线</param>
    /// <param name="P1">包含P0的Segment结束位置</param>
    /// <param name="T1">包含P0的Segment结束位置</param>
    /// <param name="Diff">当前段总长度</param>
    /// <param name="A">当前点在Segment中的位置 0-1</param>
    /// <returns></returns>
    public static Vector3 CubicInterpVector(Vector3 P0, Vector3 T0, Vector3 P1, Vector3 T1, float Diff, float A)
    {
        T0 *= Diff;
        T1 *= Diff;

        float A2 = A * A;
        float A3 = A2 * A;
        return (((2 * A3) - (3 * A2) + 1) * P0) + ((A3 - (2 * A2) + A) * T0) + ((A3 - A2) * T1) + (((-2 * A3) + (3 * A2)) * P1);
    }

    /// <summary>
    /// InterpCurve T(Quaternion)的泛形Curve Lerp版本
    /// </summary>
    /// <param name="P0">包含P0的Segment开始位置</param>
    /// <param name="T0">包含P0的Segment开始切线</param>
    /// <param name="P1">包含P0的Segment结束位置</param>
    /// <param name="T1">包含P0的Segment结束位置</param>
    /// <param name="Diff">当前段总长度</param>
    /// <param name="A">当前点在Segment中的位置 0-1</param>
    /// <returns></returns>
    public static Quaternion CubicInterpQuat(Quaternion P0, Quaternion T0, Quaternion P1, Quaternion T1, float Diff, float A)
    {
        T0 = new Quaternion(T0.x * Diff, T0.x * Diff, T0.x * Diff, T0.x * Diff);
        T1 = new Quaternion(T1.x * Diff, T1.x * Diff, T1.x * Diff, T1.x * Diff);

        var Q1 = Quaternion.Slerp(P0, P1, A);
        var Q2 = Quaternion.Slerp(T0, T1, A);

        var result = Quaternion.Slerp(Q1, Q2, 2.0f * A * (1.0f - A));
        return result;
    }

    /// <summary>
    /// 四元数对数，表示为with W=0 and V=theta*v.
    /// 纯四元数（pure quaternion），其表达式为qw=(0,wx,wy,wz)
    /// </summary>
    /// <param name="self"></param>
    /// <returns></returns>
    public static Quaternion Log(Quaternion self)
    {
        Quaternion result = new Quaternion();
        if (Mathf.Abs(self.w) < 1.0f)
        {
            float angle = Mathf.Acos(self.w);
            float sinAngle = Mathf.Sin(angle);
            if (Mathf.Abs(sinAngle) >= 1.0e-7f)
            {
                float scale = angle / sinAngle;
                result.x = scale * self.x;
                result.y = scale * self.y;
                result.z = scale * self.z;
                return result;
            }
        }
        result.x = self.x;
        result.y = self.y;
        result.z = self.z;

        return result;
    }

    /// <summary>
    /// 四元数的指数，其为四元数对数的逆运算
    /// </summary>
    /// <param name="self"></param>
    /// <returns></returns>
    public static Quaternion Exp(Quaternion self)
    {
        float Angle = Mathf.Sqrt(self.x * self.x + self.y * self.y + self.z * self.z);
        float SinAngle = Mathf.Sin(Angle);

        Quaternion Result;
        Result.w = Mathf.Cos(Angle);

        if (Mathf.Abs(SinAngle) >= 1.0e-7)
        {
            float Scale = SinAngle / Angle;
            Result.x = Scale * self.x;
            Result.y = Scale * self.y;
            Result.z = Scale * self.z;
        }
        else
        {
            Result.x = self.x;
            Result.y = self.y;
            Result.z = self.z;
        }
        return Result;
    }
    public static Quaternion Subtract(Quaternion a, Quaternion b)
    {
        return new Quaternion(a.x - b.x, a.y - b.y, a.z - b.z, a.w - b.w);
    }

    public static Vector3 SubtraceScale(Vector3 a, Vector3 b, float scale)
    {
        return (a - b) / scale;
    }

    public static Vector3 DerivativeVector(Vector3 P0, Vector3 T0, Vector3 P1, Vector3 T1, float Diff, float A)
    {
        T0 *= Diff;
        T1 *= Diff;

        var a = 6.0f * P0 + 3.0f * T0 + 3.0f * T1 - 6.0f * P1;
        var b = -6.0f * P0 - 4.0f * T0 - 2.0f * T1 + 6.0f * P1;
        var c = T0;

        float A2 = A * A;
        return (a * A2) + (b * A) + c;
    }
    
    public static float CubicInterpFloat(float P0, float T0, float P1, float T1, float Diff, float A)
    {
        T0 *= Diff;
        T1 *= Diff;

        float A2 = A * A;
        float A3 = A2 * A;
        return (((2 * A3) - (3 * A2) + 1) * P0) + ((A3 - (2 * A2) + A) * T0) + ((A3 - A2) * T1) + (((-2 * A3) + (3 * A2)) * P1);

    }

    public static Quaternion Add(Quaternion A, Quaternion B, float scale = 1.0f)
    {
        return new Quaternion((A.x + B.x) * scale, (A.y + B.y) * scale, (A.z + B.z) * scale, (A.w + B.w) * scale);
    }

    public static Vector4 Vector3To4(Vector3 vec3)
    {
        return new Vector4(vec3.x, vec3.y, vec3.z, 1.0f);
    }
}
