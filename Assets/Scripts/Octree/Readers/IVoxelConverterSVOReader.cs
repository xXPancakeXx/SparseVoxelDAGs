using System;
using System.Collections;
using UnityEngine;

namespace Assets.Scripts.Octree.Readers
{
    public interface IVoxelConverterSVOReader : IDisposable
    {
        public long NodesCount { get; }
        public int GridDimension { get; }
        public int MaxDepth { get; }

        public long FirstBufferedNodeIndex { get; }
        public long LastBufferedNodeIndex { get; }

        public VoxelConverterSVONode this[long i] { get; }

        public VoxelConverterSVONode GetNode(long index);
        public VoxelConverterSVOData GetData(long dataIndex);
    }
}