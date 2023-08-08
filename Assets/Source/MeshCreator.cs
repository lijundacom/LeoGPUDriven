using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshCreator
{
    private static MeshCreator instance;

    private MeshCreator() 
    { 

    }

    public static MeshCreator getInstance()
    {
        if(instance == null)
        {
            instance = new MeshCreator();
        }
        return instance;
    }

    public Mesh cacheQuadMesh = null;

    public Mesh debugCubeMesh = null;

    /// <summary>
    /// 生成一个方形平面网格
    /// </summary>
    /// <param name="size">方形网格的尺寸：长x宽</param>
    /// <param name="gridNum">方形网格包含多少顶点, 强制为基数</param>
    /// <returns></returns>
    public Mesh CreateQuardMesh(Vector2 size, Vector2Int gridNum)
    {
        Mesh mesh = new Mesh();
        Vector2 grid_size = size / (gridNum - Vector2.one);

        Vector3[] vertices = new Vector3[gridNum.x * gridNum.y];
        Vector2[] uvs = new Vector2[gridNum.x * gridNum.y];
        for (int i=0;i< gridNum.x; i++)
        {
            for(int j=0;j<gridNum.y;j++)
            {
                float posx = grid_size.x * (i - gridNum.x/2);
                float posz = grid_size.y * (j - gridNum.y/2);
                Vector3 pos = new Vector3(posx, 0, posz);
                Vector2 uv = new Vector2(i*1.0f/ (gridNum.x - 1), j*1.0f/ (gridNum.y - 1));
                vertices[j * gridNum.x + i] = pos;
                uvs[j * gridNum.x + i] = uv;
            }
        }
        mesh.vertices = vertices;

        int[] indexs = new int[(gridNum.x -1) * (gridNum.y - 1) * 6];

        for (int i = 0; i < gridNum.x - 1; i++)
        {
            for (int j = 0; j < gridNum.y - 1; j++)
            {
                int tri_index = (j * (gridNum.x -1) + i);

                indexs[tri_index * 6] = j * gridNum.x + i;
                indexs[tri_index * 6 + 1] = (j + 1) * gridNum.x + i;
                indexs[tri_index * 6 + 2] = (j + 1)* gridNum.x + i + 1;

                indexs[tri_index * 6 + 3] = (j + 1) * gridNum.x + i + 1;
                indexs[tri_index * 6 + 4] = j * gridNum.x + i + 1;
                indexs[tri_index * 6 + 5] = j * gridNum.x + i;
            }
        }
        mesh.triangles = indexs;
        //mesh.uv = uvs;
        mesh.RecalculateNormals();

        cacheQuadMesh = mesh;

        return mesh;
    }

    public Mesh CreateCube(float size)
    {
        var mesh = new Mesh();
        List<Vector3> vertices = new List<Vector3>();
        float extent = size * 0.5f;

        vertices.Add(new Vector3(-extent, -extent, -extent));
        vertices.Add(new Vector3(extent, -extent, -extent));
        vertices.Add(new Vector3(extent, extent, -extent));
        vertices.Add(new Vector3(-extent, extent, -extent));

        vertices.Add(new Vector3(-extent, extent, extent));
        vertices.Add(new Vector3(extent, extent, extent));
        vertices.Add(new Vector3(extent, -extent, extent));
        vertices.Add(new Vector3(-extent, -extent, extent));

        int[] indices = new int[6 * 6];

        int[] triangles = {
                0, 2, 1, //face front
                0, 3, 2,
                2, 3, 4, //face top
                2, 4, 5,
                1, 2, 5, //face right
                1, 5, 6,
                0, 7, 4, //face left
                0, 4, 3,
                5, 4, 7, //face back
                5, 7, 6,
                0, 6, 7, //face bottom
                0, 1, 6
            };

        mesh.SetVertices(vertices);
        mesh.triangles = triangles;
        mesh.UploadMeshData(false);
        debugCubeMesh = mesh;
        return mesh;
    }
}
