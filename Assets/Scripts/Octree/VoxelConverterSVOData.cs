using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Octree
{
    public unsafe struct VoxelConverterSVOData
    {
        public ulong mortonOrder;
        public float3 color;
        public float3 normal;


        public Color32 ConvertColor32(int colorBits)
        {
            if (colorBits * 3 > 32) throw new System.Exception("Cannnot exceed 32 bits per color");

            float3 cc = math.clamp(this.color, 0, 1);
            return new Color32((byte)(cc.x * 255f), (byte)(cc.y * 255f), (byte)(cc.z * 255f), 255);
        }
    }
}