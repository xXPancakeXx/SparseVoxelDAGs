using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Assets.Scripts.Octree
{
    public unsafe interface ISvoNode
    {
        public bool HasChild(int childBit);
        public uint GetChildIndex(int childBit, uint svoNodeIdx);
    }

    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public unsafe struct SvoNodeLaine : ISvoNode
    {
        // (1, 1, 1),       (1, 1, 0),          (1, 0, 1),          (1, 0, 0),              (0, 1, 1),         (0, 1, 0),         (0, 0, 1),         (0, 0, 0)
        // right-top-back   right-top-front     right-bottom-back   right-bottom-front      left-top-back      left-top-front     left-bottom-back   left-bottom-front
        // 0                0                   0                   0                       0                  0                 0                 0

        //Get the offset from the above table and add it to the debugGridPos to determine index of voxel in grid!
#if MY_DEBUG
        public uint3 debugGridPos;
#endif
        [FieldOffset(0)]
        public uint childrenRelativePointer;
        [FieldOffset(4)]
        public byte leafMask;
        [FieldOffset(5)]
        public byte validMask;

        public static explicit operator SvoNodeLaine(uint v)
        {
            return *((SvoNodeLaine*)&v);
        }

        public bool HasChild(int childBit)
        {
            int child = (1 << childBit);

            return (validMask & child) != 0 && (leafMask & child) == 0;
        }

        public uint GetChildOffset(int childBit)
        {
            int lowerIndexLeafOctantMask = (1 << childBit) - 1;
            //zero out all higher bits than octantId (& op) and count bits which are 0 (tells you how many octants are before this one = offset)
            uint childIndexOffset = (uint)math.countbits((uint)(validMask & lowerIndexLeafOctantMask));

            return childIndexOffset;
        }

        public uint GetChildIndex(int childBit, uint parentIndex)
        {
            return parentIndex + childrenRelativePointer + GetChildOffset(childBit);
        }

        public bool IsLeaf(int childBit)
        {
            return (leafMask & (1 << childBit)) != 0;
        }
    }

    public unsafe struct SvoNodeColor : ISvoNode
    {
        public uint childrenRelativeIndex;

        //lower 8 bit validmask, rest are colorIndex bits
        public uint colorIndexValidMask;

        public uint ColorIndex
        {
            get { return colorIndexValidMask >> 8; }
            set { colorIndexValidMask = (value << 8) | ValidMask; }
        }
        public uint ValidMask
        {
            get { return colorIndexValidMask & 0xFF; }
            set { colorIndexValidMask = (colorIndexValidMask & 0xFFFFFF00) | value & 0xFF ; }
        }

        public static explicit operator SvoNodeColor(uint v)
        {
            return *((SvoNodeColor*)&v);
        }


        public bool HasChild(int childBit)
        {
            return (ValidMask & (1 << childBit)) != 0;
        }

        public uint GetChildOffset(int childBit)
        {
            int lowerIndexLeafOctantMask = (1 << childBit) - 1;
            //zero out all higher bits than octantId (& op) and count bits which are 0 (tells you how many octants are before this one = offset)
            uint childIndexOffset = (uint)math.countbits((uint)(ValidMask & lowerIndexLeafOctantMask));

            return childIndexOffset;
        }

        public uint GetChildIndex(int childBit, uint parentIndex)
        {
            return parentIndex + childrenRelativeIndex + GetChildOffset(childBit);
        }
    }
}