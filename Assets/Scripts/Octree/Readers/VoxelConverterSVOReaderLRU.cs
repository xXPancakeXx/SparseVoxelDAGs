using Utils;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Octree.Readers
{

    public class VoxelConverterSVOReaderLRU : IVoxelConverterSVOReader
    {
        private const int NODE_SIZE_BYTE = 24;

        private VoxelConverterSVOInfo header;
        private FileStream fs;
        private int bufferSize;
        private byte[] nodeBuffer = new byte[NODE_SIZE_BYTE];
        private LRUCache<long, VoxelConverterSVONode> cache;

        public long NodesCount => (long)header.nodes;
        public long FirstBufferedNodeIndex => -1;
        public long LastBufferedNodeIndex => -1;
        public int GridDimension => header.gridLength;
        public int MaxDepth => header.MaxDepth;

        public VoxelConverterSVOReaderLRU(string path, int bufferSize)
        {
            this.header = SvoInfo.ParseOctreeHeaderForceFlow(path + ".octree");
            fs = new FileStream(path + ".octreenodes", FileMode.Open);

            this.bufferSize = bufferSize;
            this.cache = new LRUCache<long, VoxelConverterSVONode>(bufferSize);
        }

        public VoxelConverterSVONode this[long i]
        {
            get { return GetNode(i); }
        }

        public VoxelConverterSVONode GetNode(long index)
        {
            if (cache.TryGet(index, out var node)) return node;

            fs.Seek(index * NODE_SIZE_BYTE, SeekOrigin.Begin);
            unsafe
            {
                var ptr = (byte*)&node;
                fs.Read(nodeBuffer, 0, NODE_SIZE_BYTE);
                fixed (byte* dp = nodeBuffer)
                {
                    UnsafeMemory.CopyMemory(new IntPtr(ptr), new IntPtr(dp), NODE_SIZE_BYTE);
                }

                cache.Add(index, node);
            }

            return node;
        }

        public VoxelConverterSVOData GetData(long dataIndex)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            fs.Close();
        }
    }
}