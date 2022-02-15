using UnityEngine;
using UnityEditor.AssetImporters;
using UnityEditor;
using Assets.Scripts.Octree;
using System.IO;
using Utils;
using Assets.Scripts.Dag;
using System.Linq;
using Unity.Mathematics;

[CustomEditor(typeof(DagImporter))]
public class DagImporterEditor : ScriptedImporterEditor
{
    private DagHeader h;
    private string path;

    private int maxDepth;
    private string nodeCount;
    private string pointerCount;
    private string colorCount;
    private int colorCountSvo;
    
    private DagFormat format;
    private ByteFormat geomByteSize;
    private ByteFormat colorByteSize;

    private bool depthNodesCountFoldout = true;
    private uint[] nodesPerLevel;
    private int[] pointersPerLevel;
    
    private bool enableDeepAnalysis = false;
    
    private bool theoreticalStatsFoldout = true;


    public override void OnEnable()
    {
        var dag = assetTarget as DagInfo;
        path = dag.Path;

        if (!File.Exists(path)) return;

        using (var fs = new FileStream(path, FileMode.Open))
        using (var br = new BinaryReader(fs))
        {
            h = new DagHeader();
            h.version = br.ReadInt32();
            h.format = (DagFormat)br.ReadInt32();

            var builder = DagInfo.GetBuilderForFormat((int)h.format);
            builder.ReadHeader(br, ref h);
            format = h.format;

            maxDepth = h.maxDepth;
            colorCount = $"{h.ColorCount:#,##0.}";
            nodeCount = $"{h.NodeCount:#,##0.}";
            pointerCount = $"{h.PointerCount:#,##0.}";

            geomByteSize = new ByteFormat((uint)h.GeometryByteSize);
            colorByteSize = new ByteFormat((uint)h.ColorByteSize);

            nodesPerLevel = h.NodesPerLevel;

            if (enableDeepAnalysis)
            {
                pointersPerLevel = new int[h.maxDepth];
                for (int l = 0; l < h.maxDepth; l++)
                {
                    for (int i = 0; i < h.NodesPerLevel[l]; i++)
                    {
                        var validMask = br.ReadInt32() & 0xFF;
                        var childrenCount = math.countbits(validMask);
                        
                        pointersPerLevel[l] += childrenCount;
                        br.ReadBytes(childrenCount* 4);
                    }
                }
            }
        }

        base.OnEnable();
    }

    public override void OnInspectorGUI()
    {
        EditorGUILayout.TextField("Path", path);

        if (!File.Exists(path)) return;

        if (GUILayout.Button("Enable deep analysis"))
        {
            enableDeepAnalysis = true;
            OnEnable();
        }
        
        EditorGUILayout.IntField("Max Depth", maxDepth);
        EditorGUILayout.EnumPopup("Format", format);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Geometry Data", EditorStyles.boldLabel);
        EditorGUILayout.TextField("Node Count", nodeCount);
        EditorGUILayout.TextField("Pointer Count", pointerCount);
        ByteFormat.InspectorField("Data size", geomByteSize);


        depthNodesCountFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(depthNodesCountFoldout, "Nodes per level");
        uint accNodeCount = 0;
        int accPointerCount = 0;
        
        for (int i = 0; i < nodesPerLevel.Length && depthNodesCountFoldout; i++)
        {
            EditorGUI.indentLevel++;

            if (i == 0)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"", GUILayout.Width(100));
                EditorGUILayout.TextField($"Nodes");
                if (enableDeepAnalysis) EditorGUILayout.TextField($"Pointers");
                EditorGUILayout.TextField($"Accumulated Nodes");
                if (enableDeepAnalysis) EditorGUILayout.TextField($"Accumulated Pointers");
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Level {i + 1}", GUILayout.Width(100));
            EditorGUILayout.TextField($"{nodesPerLevel[i]:#,##0.}");
            if (enableDeepAnalysis) EditorGUILayout.TextField($"{(pointersPerLevel[i]):#,##0.}");
            EditorGUILayout.TextField($"{(accNodeCount += nodesPerLevel[i]):#,##0.}");
            if (enableDeepAnalysis) EditorGUILayout.TextField($"{(accPointerCount += pointersPerLevel[i]):#,##0.}");

            EditorGUILayout.EndHorizontal();
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Color Data", EditorStyles.boldLabel);
        EditorGUILayout.TextField("Color Count", colorCount);
        ByteFormat.InspectorField("Color size", colorByteSize);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Statistics", EditorStyles.boldLabel);
        ByteFormat.InspectorField("Overall size", geomByteSize + colorByteSize);

        EditorGUILayout.Space();
        theoreticalStatsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(theoreticalStatsFoldout, "Theoretical statistics");
        if (theoreticalStatsFoldout)
        {
            EditorGUI.indentLevel++;
        
            //Theoretical stats for other representations
            if (h.format == DagFormat.ColorPerNode || h.format == DagFormat.ColorPerPointer)
            {
                EditorGUILayout.LabelField("______________________________________", EditorStyles.boldLabel);
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Color Per Pointer DAG Stats", EditorStyles.boldLabel);

                var byteSize = new ByteFormat(4 * (h.PointerCount * 2 + h.NodeCount + h.brickLvlChildrenCount));
                EditorGUILayout.TextField("Node Count", $"{h.NodeCount:#,##0.}");
                EditorGUILayout.TextField("Pointer Count", $"{(h.PointerCount + h.brickLvlChildrenCount):#,##0.}");
                ByteFormat.InspectorField("Overall size", byteSize);

                EditorGUILayout.LabelField("______________________________________", EditorStyles.boldLabel);
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Unbricked DAG Color Per Node Stats", EditorStyles.boldLabel);

                byteSize = new ByteFormat(4 * (h.PointerCount + h.NodeCount * 2 + h.brickLvlChildrenCount * 2));
                EditorGUILayout.TextField("Node Count", $"{(h.NodeCount + h.brickLvlChildrenCount):#,##0.}");
                ByteFormat.InspectorField("Overall size", byteSize);
            
            
                // EditorGUILayout.LabelField("______________________________________", EditorStyles.boldLabel);
                // EditorGUILayout.Space();
                // EditorGUILayout.LabelField("Color Quantization", EditorStyles.boldLabel);
                //
                // colorCountSvo = EditorGUILayout.IntField("Colors in SVO", colorCountSvo);
                // long bitsQuantitizedColSize = (int) math.ceil(math.log2(colorCountSvo)) * h.NodeCount + colorCountSvo * 24;
                //
                // byteSize = new ByteFormat(bitsQuantitizedColSize);
                // ByteFormat.InspectorField("Quantitized color", byteSize);

            }
        
            EditorGUI.indentLevel--;
        }
        


        base.ApplyRevertGUI();
    }
}