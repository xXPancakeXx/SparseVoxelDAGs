using UnityEditor;
using UnityEngine;

namespace Utils
{
    public class ByteFormat
    {
        public enum ByteUnits { B, KB, MB, GB, TB };

        public long bytes;
        public ByteUnits unit;

        public ByteFormat(long bytes, ByteUnits defaultUnit = ByteUnits.MB)
        {
            this.bytes = bytes;
            this.unit = defaultUnit;
        }

        public string GetByteSize()
        {
            string[] sizes = new string[] { "B", "KB", "MB", "GB", "TB" };
            float byteFloat = (float)bytes;

            int i;
            for (i = 0; i < (int)unit; i++)
            {
                byteFloat /= 1024.0f;
            }

            return $"{byteFloat:#,##0.##}";
        }

#if UNITY_EDITOR
        public static void InspectorField(string name, ByteFormat bytes)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.TextField(name, bytes.GetByteSize());
            bytes.unit = (ByteFormat.ByteUnits)EditorGUILayout.EnumPopup(bytes.unit, GUILayout.Width(45));
            EditorGUILayout.EndHorizontal();
        }
#endif

        public static ByteFormat operator +(ByteFormat o, ByteFormat n)
        {
            return new ByteFormat(o.bytes + n.bytes);
        }
    }
}