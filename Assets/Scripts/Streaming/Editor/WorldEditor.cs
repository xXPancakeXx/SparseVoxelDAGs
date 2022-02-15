using Assets.Scripts.Streaming;
using Assets.Scripts.Streaming.Entities;
using System.Collections;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Streaming.Editor
{
    [CustomEditor(typeof(WorldInfo))]
    public class WorldEditor : UnityEditor.Editor
    {
        WorldInfo world;

        void OnEnable()
        {
            world = target as WorldInfo;
        }


        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();


            if (world.worldChunks == null || world.worldChunks.Length == 0) return;
            EditorGUILayout.BeginHorizontal();
            for (int y = 0; y < world.ChunkDims.y; y++)
            {
                EditorGUILayout.BeginVertical();
                for (int x = 0; x < world.ChunkDims.x; x++)
                {
                    EditorGUILayout.LabelField($"({x},{y})", GUILayout.Width(30));
                    EditorGUILayout.Toggle(world.worldChunks[Chunk.GetChunkDataIndex(world, x, y)] != null);
                }
                EditorGUILayout.EndVertical();

            }
            EditorGUILayout.EndHorizontal();

        }
    }
}