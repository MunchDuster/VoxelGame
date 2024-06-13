using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.VisualScripting;
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
    [SerializeField] private int testChunksY = 3;
    [SerializeField] private int testChunksZ = 5;
    
    void Start()
    {
        //MakeTestCube();
        //MakeTestChunk();
        StartCoroutine(MakeTestChunks());
    }

    private void MakeTestCube()
    {
        // Create cube mesh
        List<Vector3> vertices = new();
        List<int> triangles = new();
        int cubeIndex = 0;
        Vector3Int chunkIndex = Vector3Int.zero;
        AddCubeMeshData(1, ref cubeIndex, Vector3.zero, chunkIndex, ref triangles, ref vertices);
        Mesh mesh = CreateMeshFromData(ref triangles, ref vertices);
        
        MakeChunkGameObject(chunkIndex, mesh);
    }
    private void MakeTestChunk()
    {
        Vector3Int index = Vector3Int.zero;
        int[][][] testChunkData = GenerateChunkData(index);
        Mesh mesh = GenerateChunkMesh(index, testChunkData);
        MakeChunkGameObject(index, mesh);
    }
    private IEnumerator MakeTestChunks()
    {
        for(int x = 0; x < testChunksX; x++)
        {
            for (int y = 0; y < testChunksY; y++)
            {
                for (int z = 0; z < testChunksZ; z++)
                {
                    yield return null;

                    Vector3Int chunkIndex = new(x,y,z);
                    int[][][] data = GenerateChunkData(chunkIndex);

                    // Optimisation : skip making chunk mesh and gameobject if all data = 0
                    if (data.All(plane => plane.All(row => row.All(block => block == 0))))
                        continue;

                    Mesh chunkMesh = GenerateChunkMesh(chunkIndex, data);
                    MakeChunkGameObject(chunkIndex, chunkMesh);
                }
            }
        } 
    }

    private void MakeChunkGameObject(Vector3Int chunkIndex, Mesh mesh)
    {
        GameObject instance = new($"Chunk {chunkIndex.x} {chunkIndex.y} {chunkIndex.z}");
        instance.transform.SetParent(transform);

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
                    Vector3 pos = chunkIndex * chunkSize + new Vector3(x,y,z);
                    float maxHeight = Mathf.PerlinNoise(pos.x * perlinScale, pos.z * perlinScale);
                    float curHeight = (float)pos.y / (chunkSize * testChunksY);
                    data[x][y][z] = curHeight < maxHeight ? 1 : 0;
                }
            }
        }
        return data;
    }

    public Dictionary<Vector3Int, Mesh> GenerateAllChunkMeshes(Dictionary<Vector3Int, int[][][]> chunks)
    {
        // Map each chunkIndex to a mesh
        Dictionary<Vector3Int, Mesh> chunkMeshes = new();
        IList<KeyValuePair<Vector3Int, int[][][]>> chunksList = chunks.AsReadOnlyList();
        for(int i = 0; i < chunksList.Count; i++)
        {
            chunkMeshes.Add(chunksList[i].Key, GenerateChunkMesh(chunksList[i].Key, chunksList[i].Value));
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
        const int maxIndex = chunkSize - 1;
        const int startIndex = 0;

        for(int x = 0; x < chunkSize; x++)
        {
            for (int y = 0; y < chunkSize; y++)
            {
                for (int z = 0; z < chunkSize; z++)
                {
                    //Check if air
                    if (chunk[x][y][z] == 0)
                    {
                        continue;
                    }

                    // Vertices
                    Vector3 cubeOffset = new(x,y,z);
                    vertices.AddRange(cubeVertices.Select(corner => corner + chunkOffset + cubeOffset));

                    // Triangles
                    int cubeTrisOffset = 8 * cubeIndex;

                    // Conditions that match with indices for cubeFaces
                    bool[] cubeFaceChecks = {
                        z == startIndex || chunk[x][y][z - 1] == 0, // b 0
                        z == maxIndex   || chunk[x][y][z + 1] == 0, // f 1
                        y == startIndex || chunk[x][y - 1][z] == 0, // d 2
                        y == maxIndex   || chunk[x][y + 1][z] == 0, // u 3
                        x == startIndex || chunk[x - 1][y][z] == 0, // l 4
                        x == maxIndex   || chunk[x + 1][y][z] == 0  // r 5
                    };

                    for (int i = 0; i < cubeFaces.Length; i++)
                    {
                        Debug.Log($"{i}: {cubeFaceChecks[i]}");
                        if (cubeFaceChecks[i]) 
                        {
                            triangles.AddRange(cubeFaces[i].Select(index => index + cubeTrisOffset));
                        }
                    }

                    cubeIndex++;
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
        int cubeTrisOffset = 8 * cubeIndex;
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
