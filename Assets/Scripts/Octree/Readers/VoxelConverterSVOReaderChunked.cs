using Utils;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using System.Runtime.InteropServices;

namespace Assets.Scripts.Octree.Readers
{
    public class VoxelConverterSVOReaderChunked : IVoxelConverterSVOReader
    {
        private const int NODE_SIZE_BYTE = 24;

        private VoxelConverterSVOInfo header;
        private FileStream fs;

        private long bufferStartNodeIndex;
        private int bufferReadNodeLength;
        private bool readBackwards;

        private int bufferSize;
        private byte[] buffer;

        public long NodesCount => (long)header.nodes;
        public long FirstBufferedNodeIndex => bufferStartNodeIndex;
        public long LastBufferedNodeIndex => bufferStartNodeIndex + bufferReadNodeLength - 1;
        public int GridDimension => header.gridLength;
        public int MaxDepth => header.MaxDepth;

        public VoxelConverterSVOReaderChunked(string path, int bufferSize, bool readBackwards)
        {
            if (bufferSize % NODE_SIZE_BYTE != 0) throw new Exception("bufferSize needs to be multiple of 24 bytes");
            if (bufferSize >= Int32.MaxValue) throw new Exception($"bufferSize needs to fit into signed integer ({Int32.MaxValue} bytes)");

            this.header = SvoInfo.ParseOctreeHeaderForceFlow(path + ".octree");
            fs = new FileStream(path + ".octreenodes", FileMode.Open);

            this.bufferSize = bufferSize;
            this.buffer = new byte[bufferSize];
            this.bufferStartNodeIndex = -1;
            this.readBackwards = readBackwards;
        }

        public VoxelConverterSVONode this[long i]
        {
            get { return GetNode(i); }
        }


        public VoxelConverterSVONode GetNode(long index)
        {
            if (index >= bufferStartNodeIndex && index < bufferStartNodeIndex + bufferReadNodeLength)
            {
                unsafe
                {
                    var nodeIndex = index - bufferStartNodeIndex;

                    VoxelConverterSVONode node;
                    fixed (byte* bufferPtr = buffer)
                    {
                        node = ((VoxelConverterSVONode*) bufferPtr)[nodeIndex];
                    }
                    return node;
                }
            }
            else
            {
                if (readBackwards) ReadChunkBackwards(index);
                else ReadChunk(index);
                
                return GetNode(index);
            }
        }

        public VoxelConverterSVOData GetData(long dataIndex)
        {
            throw new NotImplementedException();
        }

        public void ReadChunk(long index)
        {
            bufferStartNodeIndex = index;
            //var remainingBytes = fs.Length - fs.Position;
            //var readBytes = Math.Min(remainingBytes, bufferSize);

            fs.Seek(index * NODE_SIZE_BYTE, SeekOrigin.Begin);
            bufferReadNodeLength = fs.Read(buffer, 0, bufferSize) / NODE_SIZE_BYTE;
        }

        public void ReadChunkBackwards(long index)
        {
            //var remainingBytes = fs.Length - fs.Position;
            //var readBytes = Math.Min(remainingBytes, bufferSize);
            var startIndex = System.Math.Max(index * NODE_SIZE_BYTE - bufferSize + NODE_SIZE_BYTE, 0);
            bufferStartNodeIndex = startIndex / NODE_SIZE_BYTE;

            fs.Seek(startIndex, SeekOrigin.Begin);
            bufferReadNodeLength = fs.Read(buffer, 0, bufferSize) / NODE_SIZE_BYTE;
        }

        public void Dispose()
        {
            fs.Close();
        }

        
    }
}