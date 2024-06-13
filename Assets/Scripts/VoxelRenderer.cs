using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Generates meshes for chunks
/// TODO: 1. Join adjacent vertices (within chunk)
/// TODO: 2. Dont add faces that wont be visible (within chunk)
/// </summary>
public class VoxelRenderer : MonoBehaviour
{
    private const int chunkSize = 4;

    private static readonly Vector3[] cubeVertices = new Vector3[8]
    {
        
        new(1, 0, 0), // rdb 0
        new(0, 0, 0), // ldb 1
        new(0, 1, 0), // lub 2
        new(1, 1, 0), // rub 3
        new(1, 0, 1), // rdf 4
        new(0, 0, 1), // ldf 5
        new(0, 1, 1), // luf 6
        new(1, 1, 1)  // ruf 7
    };

    /// <summary>
    /// Contains the array for making triangles, values corresponding to indices to the cubeVertices array
    /// </summary>
    private static readonly int[][] cubeFaces = new int[6][]
    {
        new int[6]{0, 1, 2, 2, 3, 0}, // b 0
        new int[6]{6, 5, 4, 4, 7, 6}, // f 1
        new int[6]{5, 1, 0, 0, 4, 5}, // d 2
        new int[6]{3, 2, 6, 6, 7, 3}, // u 3
        new int[6]{1, 5, 6, 6, 2, 1}, // l 4
        new int[6]{4, 0, 3, 3, 7, 4}, // r 5
    };

    [Header("For testing")]
    [SerializeField] private Material material;
    [SerializeField] private float perlinScale = 0.5f;
    [SerializeField] private int testChunksX = 3;
    [SerializeField] private int testChunksZ = 5;
    
    void Start()
    {
        MakeTestCube();
        //MakeTestChunks();
    }

    private void MakeTestCube()
    {
        // Create cube mesh
        List<Vector3> vertices = new();
        List<int> triangles = new();
        int cubeIndex = 0;
        AddCubeMeshData(1, ref cubeIndex, Vector3.zero, Vector3.zero, ref triangles, ref vertices);
        Mesh mesh = CreateMeshFromData(ref triangles, ref vertices);
        
        MakeChunkGameObject(mesh);

    }
    private void MakeTestChunks()
    {
        const int y = 0;
        Dictionary<Vector3Int, int[][][]> testChunks = new();
        for(int x = 0; x < testChunksX; x++)
        {
            for (int z = 0; z < testChunksZ; z++)
            {
                Vector3Int chunkIndex = new(x,y,z);
                int[][][] data = GenerateChunkData(chunkIndex);
                testChunks.Add(chunkIndex, data);
            }
        } 
        Dictionary<Vector3Int, Mesh> chunkMeshes = GenerateAllChunkMeshes(testChunks);

        foreach (KeyValuePair<Vector3Int, Mesh> chunkMeshPair in chunkMeshes)
        {
            MakeChunkGameObject(chunkMeshPair.Value);
        }
    }

    private void MakeChunkGameObject(Mesh mesh)
    {
        GameObject instance = new();
            
        MeshRenderer meshRenderer = instance.AddComponent<MeshRenderer>();
        meshRenderer.material = material;

        MeshFilter meshFilter = instance.AddComponent<MeshFilter>();
        meshFilter.mesh = mesh;
    }

    int[][][] GenerateChunkData(Vector3 chunkIndex)
    {
        int[][][] data = new int[chunkSize][][];
        for(int x = 0; x < chunkSize; x++)
        {
            data[x] = new int[chunkSize][];
            for (int y = 0; y < chunkSize; y++)
            {
                data[x][y] = new int[chunkSize];
                for (int z = 0; z < chunkSize; z++)
                {
                    float height = Mathf.PerlinNoise(x * perlinScale, y * perlinScale);
                    data[x][y][z] = height > y ? 1 : 0;
                }
            }
        }
        return data;
    }

    public Dictionary<Vector3Int, Mesh> GenerateAllChunkMeshes(Dictionary<Vector3Int, int[][][]> chunks)
    {
        // Map each chunkIndex to a mesh
        Dictionary<Vector3Int, Mesh> chunkMeshes = new();
        
        foreach(var pair in chunks)
        {
            chunkMeshes.Add(pair.Key, GenerateChunkMesh(pair.Key, pair.Value));
        }

        return chunkMeshes;
    }

    private Mesh GenerateChunkMesh(Vector3Int chunkIndex, int[][][] chunk)
    {
        // Calculate all the grunt data for the mesh
        List<Vector3> vertices = new();
        List<int> triangles = new();
        int cubeIndex = 0;
        Vector3 chunkOffset = chunkIndex * chunkSize;

        for(int x = 0; x < chunkSize; x++)
        {
            for (int y = 0; y < chunkSize; y++)
            {
                for (int z = 0; z < chunkSize; z++)
                {
                    AddCubeMeshData(chunk[x][y][z], ref cubeIndex, chunkOffset, new(x, y, z), ref triangles, ref vertices);
                }
            }
        }

        // Actually make the mesh
        return CreateMeshFromData(ref triangles, ref vertices);
    }

    private void AddCubeMeshData(int cubeType, ref int cubeIndex, Vector3 chunkOffset, Vector3 cubeOffset, ref List<int> triangles, ref List<Vector3> vertices)
    {
        //Check if air
        if (cubeType == 0)
        {
            return;
        }

        // Vertices
        vertices.AddRange(cubeVertices.Select(corner => corner + chunkOffset + cubeOffset));

        // Triangles
        int cubeTrisOffset = 6 * cubeIndex;
        triangles.AddRange(cubeFaces.SelectMany(faceTris => faceTris.Select(index => index + cubeTrisOffset)));

        cubeIndex++;
    }

    private Mesh CreateMeshFromData(ref List<int> triangles, ref List<Vector3> vertices)
    {
        Mesh mesh = new()
        {
            vertices = vertices.ToArray(),
            triangles = triangles.ToArray()
        };
        mesh.RecalculateNormals();
        mesh.Optimize();
        return mesh;
    }
}
