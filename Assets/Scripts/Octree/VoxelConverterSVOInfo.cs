using System;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Octree
{
    public unsafe struct VoxelConverterSVOInfo
    {
        public int gridLength;
        public ulong nodes;
        public ulong data;

        public int MaxDepth => FastLog2(gridLength);

        public VoxelConverterSVOInfo(int gridLength, ulong nodes, ulong data) : this()
        {
            this.gridLength = gridLength;
            this.nodes = nodes;
            this.data = data;
        }

        private int FastLog2(int value)
        {
            int bits = 0;
            while (value > 0)
            {
                bits++;
                value >>= 1;
            }
            return bits;
        }
    }
}