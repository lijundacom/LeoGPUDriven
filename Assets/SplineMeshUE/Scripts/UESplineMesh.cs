using UnityEngine;
using System.Collections;

[System.Serializable]
public class SplineMeshParam
{
    public Vector3 StartPos = Vector3.zero;
    public Vector3 StartTangent = Vector3.forward;
    public Vector2 StartScale = Vector2.one;
    public float StartRoll = 0.0f;

    public Vector3 EndPos = Vector3.zero;
    public Vector3 EndTangent = Vector3.forward;
    public Vector2 EndScale = Vector2.one;
    public float EndRoll = 0.0f;
}


public enum MeshAxisType
{
    X,
    Y,
    Z
}

[ExecuteInEditMode]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
public class UESplineMesh : MonoBehaviour
{
    private MeshRenderer render;
    private MeshFilter mesh;
    public SplineMeshParam param = new SplineMeshParam();
    //物体本身的朝向，指的就是物体本身mesh里的vertex数据方向
    [SerializeField, SetProperty("ForwardAxis")]
    private MeshAxisType forwardAxis = MeshAxisType.Z;

    public MeshAxisType ForwardAxis
    {
        get
        {
            return forwardAxis;
        }
        set
        {
            forwardAxis = value;
            changeForward();
        }
    }

    //用于生成坐标系的向上向量，含义同设定视图坐标系的向上向量设置一样
    public Vector3 splineUpDir = Vector3.up;
    //可以参考射线定义 p(t)=min+scale*t
    private float splineMeshMinZ;
    private float splineMeshScaleZ;

    //本身坐标系与切线坐标系的方向差
    private Vector3 splineMeshDir;
    private Vector3 splineMeshX;
    private Vector3 splineMeshY;

    private bool bSplineShader = false;
    private Material splineMaterial = null;
    [SerializeField]
    private Material oldMaterial = null;


    [HideInInspector]
    public Transform start = null;
    [HideInInspector]
    public Transform end = null;
    public bool ShowPosition = true;
    public bool ShowRotation = false;

    void Start()
    {
        if (!bSplineShader)
        {
            this.Init();
        }
        this.UpdateSplineMesh();
    }

    public void Init()
    {
        start = ConfigureChildNode("Start");
        end = ConfigureChildNode("End");

        render = GetComponent<MeshRenderer>();
        mesh = GetComponent<MeshFilter>();
        Shader shader = Shader.Find("Custom/SplineMeshSurfShader");
        if (render != null && shader != null && shader.isSupported)
        {
            if (render.sharedMaterial != null && render.sharedMaterial.shader != shader)
            {
                oldMaterial = render.sharedMaterial;
                render.material = new Material(shader);
            }
            splineMaterial = render.sharedMaterial;
            bSplineShader = true;
        }
        if (param == null)
        {
            param = new SplineMeshParam();
        }
        this.changeForward();
    }

    //当ForwardAxis改变，需要改变所有显示
    public void changeForward()
    {
        if (!bSplineShader)
            return;
        var bound = render.bounds;
        //中心是位置向量，需要w=1
        Vector3 center = this.transform.worldToLocalMatrix * InterpHelp.Vector3To4(bound.center);
        //大小属于方向向量，需要w=0
        Vector3 extent = this.transform.worldToLocalMatrix * bound.extents;
        //1/物体变化方向上的长度
        splineMeshScaleZ = 0.5f / getAxisValue(extent, forwardAxis);
        //最小值
        splineMeshMinZ = (getAxisValue(center, forwardAxis)) * splineMeshScaleZ - 0.5f;

        //默认设置最大值与最小值
        var forwardSize = Vector3.Scale(extent, getAxisMask(forwardAxis));
        start.localPosition = center - forwardSize;
        end.localPosition = center + forwardSize;

        //meshdir,meshx,meshy的顺序
        splineMeshDir = Vector3.zero;
        splineMeshDir[((int)forwardAxis)] = 1;
        splineMeshX = Vector3.zero;
        splineMeshX[((int)forwardAxis + 1) % 3] = 1;
        splineMeshY = Vector3.zero;
        splineMeshY[((int)forwardAxis + 2) % 3] = 1;

        this.UpdateSplineMesh();
    }

    //当相应子节点位置变化引起更新
    public void UpdateSplineMesh()
    {
        if (!bSplineShader)
            return;
        var dir = start.localPosition - end.localPosition;
        var sForward = start.transform.forward;
        var eForward = end.transform.forward;

        param.StartPos = start.localPosition;
        param.StartTangent = Vector3.Scale(sForward, dir);
        param.StartRoll = 0.0f;
        param.EndPos = end.localPosition;
        param.EndTangent = Vector3.Scale(eForward, dir);
        param.EndRoll = 0.0f;

        SetShaderParam();
    }

    void OnEnable()
    {
        this.Init();
    }

    void OnDisable()
    {
        if (bSplineShader)
        {
            render.material = oldMaterial;
        }
        bSplineShader = false;
    }

    //更新shader参数
    public void SetShaderParam()
    {
        if (bSplineShader && param != null)
        {
            splineMaterial.SetVector("_StartPos", param.StartPos);
            splineMaterial.SetVector("_StartTangent", param.StartTangent);
            splineMaterial.SetFloat("_StartRoll", param.StartRoll);
            splineMaterial.SetVector("_EndPos", param.EndPos);
            splineMaterial.SetVector("_EndTangent", param.EndTangent);
            splineMaterial.SetFloat("_EndRoll", param.EndRoll);

            splineMaterial.SetVector("_SplineUpDir", splineUpDir);
            splineMaterial.SetFloat("_SplineMeshMinZ", splineMeshMinZ);
            splineMaterial.SetFloat("_SplineMeshScaleZ", splineMeshScaleZ);

            splineMaterial.SetVector("_SplineMeshDir", splineMeshDir);
            splineMaterial.SetVector("_SplineMeshX", splineMeshX);
            splineMaterial.SetVector("_SplineMeshY", splineMeshY);
        }
    }

    public float getAxisValue(Vector3 position, MeshAxisType axisType)
    {
        switch (axisType)
        {
            case MeshAxisType.X:
                return position.x;
            case MeshAxisType.Z:
                return position.z;
            case MeshAxisType.Y:
            default:
                return position.y;
        }
    }

    public Vector3 getAxisMask(MeshAxisType axisType)
    {
        switch (axisType)
        {
            case MeshAxisType.X:
                return new Vector3(1, 0, 0);
            case MeshAxisType.Z:
                return new Vector3(0, 0, 1);
            case MeshAxisType.Y:
            default:
                return new Vector3(0, 1, 0);
        }
    }

    public Vector3 SplineEvalPos(Vector3 StartPos, Vector3 StartTangent, Vector3 EndPos, Vector3 EndTangent, float A)
    {
        float A2 = A * A;
        float A3 = A2 * A;

        return (((2 * A3) - (3 * A2) + 1) * StartPos) + ((A3 - (2 * A2) + A) * StartTangent) + ((A3 - A2) * EndTangent) + (((-2 * A3) + (3 * A2)) * EndPos);
    }

    public Vector3 SplineEvalDir(Vector3 StartPos, Vector3 StartTangent, Vector3 EndPos, Vector3 EndTangent, float A)
    {
        Vector3 C = (6 * StartPos) + (3 * StartTangent) + (3 * EndTangent) - (6 * EndPos);
        Vector3 D = (-6 * StartPos) - (4 * StartTangent) - (2 * EndTangent) + (6 * EndPos);
        Vector3 E = StartTangent;

        float A2 = A * A;

        return Vector3.Normalize((C * A2) + (D * A) + E);
    }

    public float SmoothStep(float A, float B, float X)
    {
        if (X < A)
        {
            return 0.0f;
        }
        else if (X >= B)
        {
            return 1.0f;
        }
        float InterpFraction = (X - A) / (B - A);
        return InterpFraction * InterpFraction * (3.0f - 2.0f * InterpFraction);
    }

    void OnDrawGizmos()//Selected()
    {
        if (!bSplineShader)
            return;
        Gizmos.color = Color.red;
        DrawChild(start);
        DrawChild(end);
    }

    private void DrawChild(Transform trans)
    {
        var toWorld = this.transform.localToWorldMatrix;
        if (trans != null)
            Gizmos.DrawSphere(trans.position, 0.02f);
    }

    private Transform ConfigureChildNode(string name)
    {
        Transform node = transform.Find(name);
        if (node == null)
        {
            node = new GameObject(name).transform;
        }
        node.parent = transform;
        node.localScale = Vector3.one;
        node.localPosition = Vector3.zero;
        node.localRotation = Quaternion.identity;

        return node;
    }
}

//Vector3[] vertices = mesh.sharedMesh.vertices;
//int count = vertices.Length;
//for (int i = 0; i < count; i++)
//{
//    var vert = vertices[i];
//    float MeshMinZ = getAxisValue(center, ForwardAxis) - getAxisValue(extent, ForwardAxis);
//    float MeshRangeZ = 2.0f * getAxisValue(extent, ForwardAxis);
//    float DistanceAlong = getAxisValue(vert, forwardAxis);
//    float t = (DistanceAlong - MeshMinZ) / MeshRangeZ;
//    float t2 = Vector3.Dot(vert, splineMeshDir);

//    Matrix4x4 sliceTransform = calcSliceTransform(t2);
//    var slceVertex = sliceTransform * vert;
//}
//mesh.sharedMesh.vertices = vertices;

//private Matrix4x4 calcSliceTransform(float YPos)
//{
//    float t = YPos * splineMeshScaleZ - splineMeshMinZ;
//    float smoothT = SmoothStep(0, 1, t);
//    //frenet理论

//    //当前位置的顶点与方向根据起点与终点的设置插值
//    Vector3 splinePos = SplineEvalPos(param.StartPos, param.StartTangent, param.EndPos, param.EndTangent, t);
//    Vector3 splineDir = SplineEvalDir(param.StartPos, param.StartTangent, param.EndPos, param.EndTangent, t);

//    //根据SplineDir与当前_SplineUpDir 计算当前坐标系(过程类似视图坐标系的建立)
//    Vector3 baseXVec = Vector3.Normalize(Vector3.Cross(splineUpDir, splineDir));
//    Vector3 baseYVec = Vector3.Normalize(Vector3.Cross(splineDir, baseXVec));

//    // Apply roll to frame around spline
//    float useRoll = Mathf.Lerp(param.StartRoll, param.EndRoll, smoothT);
//    float sinAng = Mathf.Sin(useRoll);
//    float cosAng = Mathf.Cos(useRoll);
//    Vector3 xVec = (cosAng * baseXVec) - (sinAng * baseYVec);
//    Vector3 yVec = (cosAng * baseYVec) + (sinAng * baseXVec);

//    Vector2 UseScale = Vector2.Lerp(param.StartScale, param.EndScale, YPos);

//    // Build overall transform
//    Vector4 dir = splineDir;
//    Vector4 left = xVec;
//    Vector4 up = yVec;
//    Vector4 pos4 = splinePos;
//    pos4.w = 1.0f;
//    Matrix4x4 sliceTransform = new Matrix4x4();
//    switch (ForwardAxis)
//    {
//        case MeshAxisType.X:
//            sliceTransform.SetColumn(0, dir);
//            sliceTransform.SetColumn(1, left);
//            sliceTransform.SetColumn(2, up);
//            sliceTransform.SetColumn(3, pos4);
//            break;
//        case MeshAxisType.Y:
//            sliceTransform.SetColumn(0, up);
//            sliceTransform.SetColumn(1, dir);
//            sliceTransform.SetColumn(2, left);
//            sliceTransform.SetColumn(3, pos4);
//            break;
//        case MeshAxisType.Z:
//            sliceTransform.SetColumn(0, left);
//            sliceTransform.SetColumn(1, up);
//            sliceTransform.SetColumn(2, dir);
//            sliceTransform.SetColumn(3, pos4);
//            break;
//        default:
//            break;
//    }
//    return sliceTransform;
//}