using UnityEngine;
using UnityEditor.AssetImporters;
using UnityEditor;
using Assets.Scripts.Octree;
using System.IO;
using Utils;
using Assets.Scripts.Octree.Builders;

[CustomEditor(typeof(SvoImporter))]
public class SvoImporterEditor : ScriptedImporterEditor
{
    private string path;
    private ISvoBuilder builder;
    private bool enableDeepAnalysis;

    public override void OnEnable()
    {
        var svoInfo = assetTarget as SvoInfo;
        path = svoInfo.Path;

        using (var fs = new FileStream(path, FileMode.Open))
        using (var br = new BinaryReader(fs))
        {
            this.builder = svoInfo.GetBuilderFromFile(br);
            builder.InitEditorGUI(br, enableDeepAnalysis);
        }

        base.OnEnable();
    }

    public override void OnInspectorGUI()
    {
        EditorGUILayout.TextField("Path", path);

        if (GUILayout.Button("Enable deep analysis"))
        {
            enableDeepAnalysis = true;
            OnEnable();
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.Space();

        if (this.builder == null) return;
            builder.OnEditorGUI();

        base.ApplyRevertGUI();
    }
}