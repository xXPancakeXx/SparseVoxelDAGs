using Assets.Scripts.Octree;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace Assets.Scripts.Dag
{
    public unsafe struct DagNode : IDagNode
    {
        public List<uint> children;                                 // Position in the next level

        public byte ValidMask { get; set; }
        public uint SvoParent { get; set; }

        public int ChildrenCount => children.Count;

        public int ChildrenCapacity => children.Capacity;


        public DagNode(VoxelConverterSVONode node)
        {
            ValidMask = 0;
            children = new List<uint>();
            SvoParent = 0;

            for (uint i = 0; i < 8; i++)
            {
                ValidMask |= (byte) ((node.HasChild(i) ? 1 : 0) << (int) i);
                if (node.HasChild(i))
                {
                    children.Add((uint) node.GetChildPos(i));
                }
            }
        }

        public DagNode(uint parentIdx, SvoNodeLaine node)
        {
            children = new List<uint>();
            ValidMask = node.validMask;

            SvoParent = (parentIdx);
        }

        public IEnumerable<uint> ChildrenIndexEnumerator()
        {
            foreach (var child in children)
            {
                yield return child;
            }
        }

        public uint GetChildIndex(int childIndex)
        {
            return children[childIndex];
        }

        public void SetChildIndex(int childIndex, uint nodeIdx)
        {
            children[childIndex] = nodeIdx;
        }

        public void AddChildren(uint nodeIdx)
        {
            children?.Add(nodeIdx);
        }


        public bool Equals(IDagNode other)
        {
            if (other.ValidMask != ValidMask)
                return false;
            for (int j = 0; j < children.Count; j++)
            {
                if (other.GetChildIndex(j) != children[j])
                    return false;
            }
            return true;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}