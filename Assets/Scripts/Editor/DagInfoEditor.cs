using Assets.Scripts.Dag;
using System.IO;
using UnityEditor;
using UnityEngine;
using Utils;

namespace Assets.Scripts.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(DagInfo))]
    public class DagInfoEditor : UnityEditor.Editor
    {
        private SerializedProperty pathProp;

        private int maxDepth;
        private string nodeCount;
        private string pointerCount;
        private string colorCount;

        private DagFormat format;
        private ByteFormat geomByteSize;
        private ByteFormat colorByteSize;

        void OnEnable()
        {
            pathProp = serializedObject.FindProperty("path");

            var path = pathProp.stringValue + ".dag";
            if (!File.Exists(path)) return;

            using (var fs = new FileStream(path, FileMode.Open))
            using (var br = new BinaryReader(fs))
            {
                DagHeader h = new DagHeader();
                h.version = br.ReadInt32();
                h.format = (DagFormat)br.ReadInt32();

                var builder = DagInfo.GetBuilderForFormat((int)h.format);
                builder.ReadHeader(br, ref h);
                format = h.format;

                maxDepth = h.maxDepth;
                colorCount = $"{h.ColorCount:#,##0.}";
                nodeCount = $"{h.NodeCount:#,##0.}";
                pointerCount = $"{h.PointerCount:#,##0.}";

                geomByteSize = new ByteFormat(h.GeometryByteSize);
                colorByteSize = new ByteFormat(h.ColorByteSize);
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(pathProp);
            serializedObject.ApplyModifiedProperties();

            if (!File.Exists(pathProp.stringValue + ".dag")) return;

            EditorGUILayout.IntField("Max Depth", maxDepth);
            EditorGUILayout.EnumPopup("Format", format);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Geometry Data", EditorStyles.boldLabel);
            EditorGUILayout.TextField("Node Count", nodeCount);
            EditorGUILayout.TextField("Pointer Count", pointerCount);
            ByteFormat.InspectorField("Data size", geomByteSize);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Color Data", EditorStyles.boldLabel);
            EditorGUILayout.TextField("Color Count", colorCount);
            ByteFormat.InspectorField("Color size", colorByteSize);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Statistics", EditorStyles.boldLabel);
            ByteFormat.InspectorField("Overall size", geomByteSize + colorByteSize);


        }
    }
}