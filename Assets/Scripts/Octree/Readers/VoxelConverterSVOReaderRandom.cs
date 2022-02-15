using Utils;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using System.Runtime.InteropServices;
using System.IO.MemoryMappedFiles;

namespace Assets.Scripts.Octree.Readers
{
    public class VoxelConverterSVOReaderRandom : IVoxelConverterSVOReader
    {
        const long NODE_SIZE = 24;
        const long DATA_SIZE = 8 + 12 + 12; //equals size of VoxelConverterSVOData struct

        private VoxelConverterSVOInfo header;
        private MemoryMappedFile mmfNodes;
        private MemoryMappedFile mmfData;
        private MemoryMappedViewAccessor nodeAccessor;
        private MemoryMappedViewAccessor dataAccessor;


        public long NodesCount => (long)header.nodes;
        public long FirstBufferedNodeIndex => -1;
        public long LastBufferedNodeIndex => -1;
        public int GridDimension => header.gridLength;
        public int MaxDepth => header.MaxDepth;

        public VoxelConverterSVOReaderRandom(string path)
        {
            this.header = SvoInfo.ParseOctreeHeaderForceFlow(path + ".octree");
            mmfNodes = MemoryMappedFile.CreateFromFile(path + ".octreenodes", FileMode.Open);
            mmfData = MemoryMappedFile.CreateFromFile(path + ".octreedata", FileMode.Open);
            
            nodeAccessor = mmfNodes.CreateViewAccessor(0, (long) header.nodes * NODE_SIZE);
            dataAccessor = mmfData.CreateViewAccessor(0, (long) header.data * DATA_SIZE);
        }

        public VoxelConverterSVONode this[long i]
        {
            get { return GetNode(i); }
        }


        public VoxelConverterSVONode GetNode(long index)
        {
            //const int nodesInView = 1;
            //var bytesInView = nodesInView * NODE_SIZE;

            var byteIndex = index * NODE_SIZE;
            //var offset = (index - nodesInView + 1) * nodeSize;
            //using var a = mmf.CreateViewAccessor(byteIndex, bytesInView, MemoryMappedFileAccess.Read);
            
            //using var a = mmf.CreateViewAccessor(0, nodeSize * 2, MemoryMappedFileAccess.Read);
            
            VoxelConverterSVONode node;
            nodeAccessor.Read(byteIndex, out node);
            return node;
        }

        public VoxelConverterSVOData GetData(long dataIndex)
        {
            var byteIndex = dataIndex * DATA_SIZE;

            VoxelConverterSVOData node;
            dataAccessor.Read(byteIndex, out node);
            return node;
        }

        public void Dispose()
        {
            mmfNodes.Dispose();
            nodeAccessor.Dispose();
        }
    }
}