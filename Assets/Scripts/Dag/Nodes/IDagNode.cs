using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Dag
{
    public interface IDagNode : IEquatable<IDagNode>
    {
        public byte ValidMask { get; set; }

        public int ChildrenCount { get; }
        public int ChildrenCapacity { get; }

        public uint SvoParent { get; set; }


        public uint GetChildIndex(int childIndex);
        public void SetChildIndex(int childIndex, uint nodeIdx);
        public IEnumerable<uint> ChildrenIndexEnumerator();
    }
}