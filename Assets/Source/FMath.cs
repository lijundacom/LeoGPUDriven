using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class FMath
{
    public static UInt32 ReverseMortonCode2(UInt32 x)
    {
        x &= 0x55555555;
        x = (x ^ (x >> 1)) & 0x33333333;
        x = (x ^ (x >> 2)) & 0x0f0f0f0f;
        x = (x ^ (x >> 4)) & 0x00ff00ff;
        x = (x ^ (x >> 8)) & 0x0000ffff;
        return x;
    }

    public static UInt32 MortonCode2(UInt32 x)
    {
        x &= 0x0000ffff;
        x = (x ^ (x << 8)) & 0x00ff00ff;
        x = (x ^ (x << 4)) & 0x0f0f0f0f;
        x = (x ^ (x << 2)) & 0x33333333;
        x = (x ^ (x << 1)) & 0x55555555;
        return x;
    }

    /// <summary>
    /// 判断射线与平面的交点
    /// </summary>
    /// <param name="rayOrign"></param>
    /// <param name="rayDir"></param>
    /// <param name="planeNormal"></param>
    /// <param name="pointOnPlane"></param>
    /// <returns></returns>
    public static Vector3 GetIntersectionOfRayAndPlane(Vector3 rayOrign, Vector3 rayDir, Vector3 planeNormal, Vector3 pointOnPlane)
    {
        float d = Vector3.Dot(pointOnPlane - rayOrign, planeNormal) / Vector3.Dot(rayDir.normalized, planeNormal);
        return d * rayDir + rayOrign;
    }
}
