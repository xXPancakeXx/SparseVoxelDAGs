using Assets.Scripts.Octree;
using System.Collections.Generic;

namespace Assets.Scripts.Dag
{
    public struct DagNodeColorPerNode : IDagNode
    {
        public uint subtreeNodeCount;

        public List<uint> children;
        public byte ValidMask { get; set; }
        public uint SvoParent { get; set; }

        public int ChildrenCount => children.Count;
        public int ChildrenCapacity => children.Capacity;

        public DagNodeColorPerNode(uint parentIdx, SvoNodeColor svoNode)
        {
            ValidMask = (byte)svoNode.ValidMask;
            children = new List<uint>();
            subtreeNodeCount = 0;
            SvoParent = parentIdx;
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