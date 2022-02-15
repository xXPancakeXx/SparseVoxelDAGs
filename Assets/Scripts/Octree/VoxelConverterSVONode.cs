using System;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Octree
{
    public unsafe struct VoxelConverterSVONode
    {
        public const byte NOCHILD = 255;
        public const byte NODATA = 0;
        public static readonly byte[] LEAF = new byte[] { NOCHILD, NOCHILD, NOCHILD, NOCHILD, NOCHILD, NOCHILD, NOCHILD, NOCHILD };

        public ulong data;
        public ulong childrenBase;
        public fixed byte childrenOffset[8];

        // Check if this Node has a child at position i
        public bool HasChild(uint i)
        {
            //return false;
            return !(childrenOffset[i] == NOCHILD);
        }

        // Get the full index of the child at position i
        public ulong GetChildPos(uint i)
        {
            //return 0;
            if (childrenOffset[i] == NOCHILD)
            {
                return 0;
            }
            else
            {
                return childrenBase + childrenOffset[i];
            }
        }

        // If this node doesn't have data and is a leaf node, it's a null node
        public bool IsNull()
        {
            return IsLeaf() && !HasData();
        }

        // If this node doesn;t have any children, it's a leaf node
        public bool IsLeaf()
        {
            //return true;

            fixed (byte* ptr = childrenOffset)
            fixed (byte* leafArr = LEAF)
            {
                if (UnsafeMemory.memcmp(ptr, leafArr, 8 * sizeof(byte)) == 0)
                {
                    return true;
                }
                return false;
            }
        }

        // If the data pointer is NODATA, there is no data
        public bool HasData()
        {
            return !(data == NODATA);
        }

        public override string ToString()
        {
            return $"isLeaf: {IsLeaf()}, hasData: {HasData()}";
        }
    }
}