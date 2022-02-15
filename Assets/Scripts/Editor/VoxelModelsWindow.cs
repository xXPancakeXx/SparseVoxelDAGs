using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Assets.Scripts.Editor
{
    public class VoxelModelsWindow : EditorWindow
    {
        private UnityEditor.Editor editor;
        private UnityEditor.Editor settingsEditor;

        private Vector2 scrollPos;
        
        [MenuItem("Window/Voxel Models")]
        static void Init()
        {
            GetWindow<VoxelModelsWindow>("Voxel Models");
        }
        
        private void OnEnable()
        {
            var so = AssetDatabase.LoadAssetAtPath<DataInfo>("Assets/Settings/Data.asset");
            var settings = AssetDatabase.LoadAssetAtPath<ForwardRendererData>("Assets/Settings/ForwardRenderer.asset");
            
            editor = UnityEditor.Editor.CreateEditor(so);
            settingsEditor = UnityEditor.Editor.CreateEditor(settings);
        }

        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            editor.OnInspectorGUI();
            settingsEditor.OnInspectorGUI();
            EditorGUILayout.EndScrollView();
        }
    }
}