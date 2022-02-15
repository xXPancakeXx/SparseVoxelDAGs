using Assets.Scripts.Octree;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using UnityEngine.Bindings;
using UnityEngine.Scripting;
namespace Assets.Scripts.Dag
{
    public unsafe class DagNodeColorPerPointer : IDagNode
    {
        public uint data;

        public List<(uint index, uint attributeOffset)> children;
        public uint SvoParent { get; set; }


        public uint ColorIndex
        {
            get
            {
                return data >> 8;
            }
            set
            {
                data = (value << 8) | ValidMask;
            }
        }

        public byte ValidMask
        {
            get
            {
                return (byte)(data & 0xFF);
            }
            set
            {
                data = (data & 0x00) | value;
            }
        }
        public int ChildrenCount => children.Count;
        public int ChildrenCapacity => children.Capacity;


        public DagNodeColorPerPointer(uint parentIdx, SvoNodeColor svoNode)
        {
            ValidMask = (byte) svoNode.ValidMask;
            children = new List<(uint, uint)>();

            SvoParent = (parentIdx);
        }

        public IEnumerable<uint> ChildrenIndex()
        {
            foreach (var child in children)
            {
                yield return child.index;
            }
        }

        public uint GetChildIndex(int childIndex)
        {
            return children[childIndex].index;
        }

        public void SetChildIndex(int childIndex, uint nodeIdx)
        {
            children[childIndex] = (nodeIdx, children[childIndex].attributeOffset);
        }


        public IEnumerable<uint> ChildrenIndexEnumerator()
        {
            foreach (var child in children)
            {
                yield return child.index;
            }
        }

        public bool Equals(IDagNode o)
        {
            if (o.ValidMask != ValidMask)
                return false;
            for (int j = 0; j < children.Count; j++)
            {
                if (o.GetChildIndex(j) != children[j].index)
                    return false;
            }
            return true;
        }
    }
}