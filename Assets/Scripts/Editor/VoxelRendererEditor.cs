using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(VoxelRenderer))]
public class VoxelRendererEditor : Editor
{
    public override void OnInspectorGUI()
    {
        VoxelRenderer myComponent = (VoxelRenderer)target;
        
        DrawDefaultInspector();
        GUILayout.BeginHorizontal();
        if(GUILayout.Button("Make cube"))
        {
        	myComponent.MakeTestCube();
        }
        if(GUILayout.Button("Make chunk"))
        {
            myComponent.MakeTestChunk();
        }
        if(GUILayout.Button("Make world"))
        {
            myComponent.MakeTestWorld();
        }
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if(GUILayout.Button("Clear"))
        {
            myComponent.ClearGenerated();
        }
        if (GUILayout.Button("HARD CLEAR"))
        {
            int maxIter = 1000;
            Transform thing = myComponent.transform;

            for (int i = 0; i < maxIter && thing.childCount > 0; i++)
            {
                DestroyImmediate(thing.GetChild(0).gameObject);
            }
        }
        GUILayout.EndHorizontal();
    }

}
