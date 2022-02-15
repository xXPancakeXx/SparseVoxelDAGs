using Assets.Scripts.Streaming.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Utils;

namespace Assets.Scripts.Streaming.Editor
{
    [CustomEditor(typeof(WorldStreamingController))]
    public class WorldStreamingContollerEditor : UnityEditor.Editor
    {
        WorldStreamingController c;

        void OnEnable()
        {
            c = target as WorldStreamingController;
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (c.World == null) return;

            var bf1 = new ByteFormat((long) c.World.MaxActiveChunkCount * c.World.ChunkSizeInt * 4);
            var bf2 = new ByteFormat(c.World.ChunkDims.x * c.World.ChunkDims.y * 4, ByteFormat.ByteUnits.KB);

            EditorGUILayout.IntField("Max active chunks", c.World.MaxActiveChunkCount);
            ByteFormat.InspectorField("Chunk buffer size", bf1);
            ByteFormat.InspectorField("Chunk info buffer size", bf2);

            EditorGUILayout.LabelField($"Loaded chunks", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            for (int x = 0; x < c.World.ChunkDims.x; x++)
            {
                EditorGUILayout.BeginVertical();
                for (int y = c.World.ChunkDims.y - 1; y >= 0; y--)
                {
                    EditorGUILayout.LabelField($"({x},{y})", GUILayout.Width(30));
                    EditorGUILayout.Toggle(c.World.LoadedChunkSlots.ContainsKey(new int2(x, y)));
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndHorizontal();

        }
    }
}