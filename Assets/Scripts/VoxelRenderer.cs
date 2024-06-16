using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Generates meshes for chunks
/// </summary>
// TODO: Optimise so that only visible faces are shown, (max 3 sides of a cube are visible at any time)
// Chunk loading and unloading,
// Create player
// Add destruction and building
// g u n s ? ? ? 
public class VoxelRenderer : MonoBehaviour
{
    private const float colorMapTileSize = 0.33f;
    private const float uvPadding = 0.1f;

    #region mesh creation data
    private static readonly Vector3Int[] cubeVertices = new Vector3Int[8]
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
        new int[6]{5, 4, 7, 7, 6, 5}, // f 1
        new int[6]{5, 1, 0, 0, 4, 5}, // d 2
        new int[6]{3, 2, 6, 6, 7, 3}, // u 3
        new int[6]{1, 5, 6, 6, 2, 1}, // l 4
        new int[6]{4, 0, 3, 3, 7, 4}  // r 5
    };

    private static readonly Vector3Int[] cubeNormals = new Vector3Int[6]
    {
        new( 0, 0,-1), // b 0
        new( 0, 0, 1), // f 1
        new( 0,-1, 0), // d 2
        new( 0, 1, 0), // u 3
        new(-1, 0, 0), // l 4
        new( 1, 0, 0)  // r 5
    };

    private static readonly Vector2[] faceUVs = new Vector2[6] 
    {
        new(uvPadding                   , uvPadding     ), 
        new(colorMapTileSize - uvPadding, uvPadding     ), 
        new(colorMapTileSize - uvPadding, 1 - uvPadding ), 
        new(colorMapTileSize - uvPadding, 1 - uvPadding ), 
        new(uvPadding                   , 1 - uvPadding ), 
        new(uvPadding                   , uvPadding     )
    };
    /// <summary>
    /// The uvs coordinates for each vertex corresponding to the triangles on faces
    /// </summary>
    private static readonly Vector2[] faceTypeUVOffsets = new Vector2[3]
    {
        new(0,0), // top
        new(colorMapTileSize,0), // sides
        new(2*colorMapTileSize,0), // bottom
    };

    private static readonly int[] faceToFaceType = new int[6]
    {
        1, 1, 2, 0, 1, 1 // all are side/bottom, top is top
    };
    #endregion

    [Header("Main controls")]
    [SerializeField] private int chunkSize = 4;
    [SerializeField] private float perlinThreshold = 0.5f;

    [Header("For testing")]
    [SerializeField] private Material material;
    [SerializeField] private float perlinScale = 0.5f;
    [SerializeField] private int testChunksX = 3;
    [SerializeField] private int testChunksY = 3;
    [SerializeField] private int testChunksZ = 5;
    [SerializeField] private float perlinHeightOffset = 1;  
    [SerializeField] private bool drawWorldChunkEdges = true;
    [SerializeField] private bool isDebuggingData = false;

    private List<GameObject> generated = new();
    private Dictionary<Vector3Int, int[][][]> debugData = new();
    void Start()
    {
        MakeTestWorld();
    }

    #region tests
    public void MakeTestCube()
    {
        // Create cube mesh
        List<Vector3> vertices = new();
        List<int> triangles = new();
        List<Vector2> uvs = new();
        int cubeIndex = 0;
        Vector3Int chunkIndex = Vector3Int.zero;
        GenerateCubeMeshData(1, ref cubeIndex, Vector3.zero, chunkIndex, ref triangles, ref vertices, ref uvs);
        Mesh mesh = CreateMeshFromData(ref triangles, ref vertices, ref uvs);
        
        MakeChunkGameObject(chunkIndex, mesh);
    }
    public void MakeTestChunk()
    {
        Vector3Int index = Vector3Int.zero;
        Dictionary<Vector3Int,int[][][]> testChunkData = new()
        {
            { index, GenerateChunkData(index) }
        };
        Mesh mesh = GenerateChunkMeshData(index, testChunkData);
        MakeChunkGameObject(index, mesh);
    }
    
    public void MakeTestWorld()
    {
        StartCoroutine(MakeTestChunks());
    }
    private IEnumerator MakeTestChunks()
    {
        Dictionary<Vector3Int,int[][][]> chunkData = new();
        for(int x = 0; x < testChunksX; x++)
        {
            for (int y = 0; y < testChunksY; y++)
            {
                for (int z = 0; z < testChunksZ; z++)
                {
                    Vector3Int chunkIndex = new(x,y,z);
                    int[][][] data = GenerateChunkData(chunkIndex);
                    chunkData.Add(chunkIndex,data);
                }
            }
        } 
        for(int x = 0; x < testChunksX; x++)
        {
            for (int y = 0; y < testChunksY; y++)
            {
                for (int z = 0; z < testChunksZ; z++)
                {
                    yield return null;

                    Vector3Int chunkIndex = new(x,y,z);
                    int[][][] data = chunkData[chunkIndex];

                     // Optimisation : skip making chunk if all data = 0
                    if (data.All(plane => plane.All(row => row.All(block => block == 0))))
                        continue;

                    // Optimisation : skip making chunk if completely surrounded
                    // if (IsSurroundedChunk(chunkIndex, chunkData))
                    // {
                    //     Debug.Log("Skipped by surrounding");
                    //     continue;
                    // }

                    Mesh chunkMesh = GenerateChunkMeshData(chunkIndex, chunkData);
                    MakeChunkGameObject(chunkIndex, chunkMesh);
                }
            }
        }
    }
    #endregion
    #region data creation (what block types)
    int[][][] GenerateChunkData(Vector3Int chunkIndex)
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
                    float pointValue = Mathf.PerlinNoise(pos.x * perlinScale, pos.z * perlinScale) + perlinHeightOffset; 
                    data[x][y][z] = pointValue > pos.y ? 1 : 0;
                }
            }
        }
        debugData.Add(chunkIndex, data);
        return data;
    }
    private void OnDrawGizmos()
    {
        if (!isDebuggingData) return;
        if(debugData == null)
        {
            Debug.LogError("No data to debug!");
            return;
        }
        Gizmos.color = Color.green;

        foreach(var chunk in debugData)
        {
            int[][][] data = chunk.Value;
            Vector3Int chunkIndex = chunk.Key;
            for(int x = 0 ; x < data.Length; x++)
            {
                for(int y = 0 ; y < data[x].Length; y++)
                {
                    for(int z = 0 ; z < data[x][y].Length; z++)
                    {
                        if (data[x][y][z] == 0) continue;

                        Vector3 centre = new Vector3(x,y,z) + chunkIndex*chunkSize + Vector3.one * 0.5f;
                        Gizmos.DrawCube(centre, Vector3.one);
                    }
                }
            }
        }
    }

    private bool GetPerlinNoise3DPointValue(Vector3 pos)
    {
        pos = pos * perlinScale;
        float x = pos.x, y = pos.y, z = pos.z;
        float XY = Mathf.PerlinNoise(x, y);
        float YZ = Mathf.PerlinNoise(y, z);
        float ZX = Mathf.PerlinNoise(z, x);
        
        float YX = Mathf.PerlinNoise(y, z);
        float ZY = Mathf.PerlinNoise(z, y);
        float XZ = Mathf.PerlinNoise(x, z);
        
        float val = (XY + YZ + ZX + YX + ZY + XZ)/6f;
        return val > perlinThreshold;
    }
    #endregion
    #region mesh data (tris, verts, uvs, etc)
    private void GenerateCubeMeshData(int cubeType, ref int cubeIndex, Vector3 chunkOffset, Vector3 cubeOffset, ref List<int> triangles, ref List<Vector3> vertices, ref List<Vector2> uvs)
    {
        // Check if air
        if (cubeType == 0)
        {
            return;
        }

        for (int faceIndex = 0; faceIndex < cubeFaces.Length; faceIndex++)
        {
            for(int triangleIndex = 0; triangleIndex < cubeFaces[faceIndex].Length; triangleIndex++)
            {
                // Vertices
                int cubeVertexIndex = cubeFaces[faceIndex][triangleIndex];
                vertices.Add(cubeVertices[cubeVertexIndex] + new Vector3Int(0,0,0) + chunkOffset);

                // UVs
                if (faceToFaceType[faceIndex] == 0)
                    Debug.Log("Side/bottom face");
                uvs.Add(faceTypeUVOffsets[faceToFaceType[faceIndex]] + faceUVs[triangleIndex]);

                // Triangles
                triangles.Add(vertices.Count - 1);
            }
        }
    }
    private Mesh GenerateChunkMeshData(Vector3Int chunkIndex, Dictionary<Vector3Int, int[][][]> chunks)
    {
        // Calculate all the grunt data for the mesh
        List<Vector3Int> vertices = new();
        List<int> triangles = new();
        List<Vector2> uvs = new();
        int cubeIndex = 0;
        Vector3Int chunkOffset = chunkIndex * chunkSize;
        int maxIndex = chunkSize - 1;
        Vector3Int maxChunkIndex = new(testChunksX - 1, testChunksY - 1, testChunksZ - 1);

        int[][][] chunk = chunks[chunkIndex];
        for(int x = 0; x < chunkSize; x++)
        {
            for (int y = 0; y < chunkSize; y++)
            {
                for (int z = 0; z < chunkSize; z++)
                {
                    // Check if air
                    if (chunk[x][y][z] == 0)
                    {
                        continue;
                    }

                    // Conditions that match with indices for cubeFaces, answers whether the face should be drawn or not
                    bool[] cubeFaceChecks = new bool[6];
                    cubeFaceChecks[0] = (z == 0        && chunkIndex.z > 0               && chunks[chunkIndex + cubeNormals[0]][x][y][maxIndex] == 0) || (z > 0        && chunk[x][y][z - 1] == 0); // b 0
                    cubeFaceChecks[1] = (z == maxIndex && chunkIndex.z < maxChunkIndex.z && chunks[chunkIndex + cubeNormals[1]][x][y][0       ] == 0) || (z < maxIndex && chunk[x][y][z + 1] == 0); // f 1
                    cubeFaceChecks[2] = (y == 0        && chunkIndex.y > 0               && chunks[chunkIndex + cubeNormals[2]][x][maxIndex][z] == 0) || (y > 0        && chunk[x][y - 1][z] == 0); // d 2
                    cubeFaceChecks[3] = (y == maxIndex && chunkIndex.y < maxChunkIndex.y && chunks[chunkIndex + cubeNormals[3]][x][0       ][z] == 0) || (y < maxIndex && chunk[x][y + 1][z] == 0); // u 3
                    cubeFaceChecks[4] = (x == 0        && chunkIndex.x > 0               && chunks[chunkIndex + cubeNormals[4]][maxIndex][y][z] == 0) || (x > 0        && chunk[x - 1][y][z] == 0); // l 4
                    cubeFaceChecks[5] = (x == maxIndex && chunkIndex.x < maxChunkIndex.x && chunks[chunkIndex + cubeNormals[5]][0       ][y][z] == 0) || (x < maxIndex && chunk[x + 1][y][z] == 0); // r 5

                    for (int faceIndex = 0; faceIndex < cubeFaces.Length; faceIndex++)
                    {
                        if (cubeFaceChecks[faceIndex])
                        {
                            for(int triangleIndex = 0; triangleIndex < cubeFaces[faceIndex].Length; triangleIndex++)
                            {
                                // Vertices
                                int cubeVertexIndex = cubeFaces[faceIndex][triangleIndex];
                                vertices.Add(cubeVertices[cubeVertexIndex] + new Vector3Int(x,y,z) + chunkOffset);

                                // UVs
                                uvs.Add(faceTypeUVOffsets[faceToFaceType[faceIndex]] + faceUVs[triangleIndex]);

                                // Triangles
                                triangles.Add(vertices.Count - 1);
                            }
                            cubeIndex++;
                        }
                    }

                }
            }
        }
        List<Vector3> verts = vertices.Select(point => (Vector3)point).ToList();

        // Actually make the mesh
        return CreateMeshFromData(ref triangles, ref verts, ref uvs);
    }
    
    #endregion
    #region mesh creation
    public Dictionary<Vector3Int, Mesh> GenerateAllChunkMeshes(Dictionary<Vector3Int, int[][][]> chunks)
    {
        // Map each chunkIndex to a mesh
        Dictionary<Vector3Int, Mesh> chunkMeshes = new();
        foreach(var pair in chunks)
        {
            chunkMeshes.Add(pair.Key, GenerateChunkMeshData(pair.Key, chunks));
        }

        return chunkMeshes;
    }
    private Mesh CreateMeshFromData(ref List<int> triangles, ref List<Vector3> vertices, ref List<Vector2> uvs)
    {
        Mesh mesh = new()
        {
            vertices = vertices.ToArray(),
            triangles = triangles.ToArray(),
            uv = uvs.ToArray()
        };
        mesh.RecalculateNormals();
        return mesh;
    }
    private bool IsSurroundedChunk(Vector3Int chunkIndex, Dictionary<Vector3Int, int[][][]> chunks) 
    {
        // World edge check
        Vector3Int maxChunkIndex = new(testChunksX - 1, testChunksY - 1, testChunksZ - 1);
        if (chunkIndex.x == 0 || chunkIndex.y == 0 || chunkIndex.z == 0 || chunkIndex.x == maxChunkIndex.x || chunkIndex.y == maxChunkIndex.y || chunkIndex.z == maxChunkIndex.z)
        {
            return false;
        }

        // Each face check
        bool IsCoveredFace(int cubeNormalIndex, int minX, int maxX, int minY, int maxY, int minZ, int maxZ)
        {
            Vector3Int otherChunkIndex = chunkIndex + cubeNormals[0];
            int[][][] otherChunk = chunks[otherChunkIndex];
            for(int x = minX; x < maxX; x++)
            {
                for (int y = minY; y < maxY; y++)
                {
                    for (int z = minZ; z < maxZ; z++)
                    {
                        if (otherChunk[x][y][z] == 0)
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        int maxCubeIndex = chunkSize - 1;
        return IsCoveredFace(0, 0, maxCubeIndex, 0, maxCubeIndex, maxCubeIndex, maxCubeIndex + 1)   // b 0
            && IsCoveredFace(1, 0, maxCubeIndex, 0, maxCubeIndex, 0, 1)                             // f 1
            && IsCoveredFace(0, 0, maxCubeIndex, maxCubeIndex, maxCubeIndex + 1, 0, maxCubeIndex)   // d 2
            && IsCoveredFace(0, 0, maxCubeIndex, 0, 1, 0, maxCubeIndex)                             // u 3
            && IsCoveredFace(0, maxCubeIndex, maxCubeIndex + 1, 0, maxCubeIndex, 0, maxCubeIndex)   // l 4
            && IsCoveredFace(0, 0, 1, 0, maxCubeIndex, 0, maxCubeIndex);                            // r 5
    }
    #endregion
    #region game object creation/destruction
    private void MakeChunkGameObject(Vector3Int chunkIndex, Mesh mesh)
    {
        GameObject instance = new($"Chunk {chunkIndex.x} {chunkIndex.y} {chunkIndex.z}");
        instance.transform.SetParent(transform);

        MeshRenderer meshRenderer = instance.AddComponent<MeshRenderer>();
        meshRenderer.material = material;

        MeshFilter meshFilter = instance.AddComponent<MeshFilter>();
        meshFilter.mesh = mesh;

        generated.Add(instance);
    }
    public void ClearGenerated()
    {
        int maxIter = 1000;

        for (int i = 0; i < maxIter && generated.Count > 0; i++)
        {
            int index = generated.Count - 1;
            DestroyImmediate(generated[index]);

            if(!generated[index])
            {
                generated.RemoveAt(index);
            }
        }

        debugData.Clear();
    }
    #endregion
}
