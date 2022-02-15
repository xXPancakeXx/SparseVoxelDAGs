using Assets.Scripts.Octree;
using Assets.Scripts.Voxelization.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using Utils;

namespace Assets.Scripts.Dag.Builders
{
    public class DagBuilderGray : DagBuilder<DagNode, SvoNodeLaine>
    {

        public override void ConstructDagMemory(SvoInfo octInfo)
        {
            var format = octInfo.GetFormat();
            if (format != SvoInfo.SvoFormat.GrayFull) throw new Exception("Only supported format is: GrayFull");

            using (var reader = octInfo.GetSeqFileReader())
            {
                Init(reader.MaxDepth, reader.NodeCount);
                ConstructSvo(reader);
            }

            PopulateParents();
            Reduce();

            GenerateNodeIdLevelList();
            AccumulateChildrenCountLevel();
        }

        public unsafe override DagNode CreateNode(uint index, int depth, IVoxelizerOctNode node)
        {
            var dagNode = new DagNode();
            dagNode.SvoParent = 0;
            dagNode.children = new List<uint>();

            if (node is OctNode octNode)
            {
                dagNode.ValidMask = octNode.validmask;
                for (int i = 0; i < 8; i++)
                {
                    if (octNode.HasChild(i))
                        dagNode.AddChildren((uint)octNode.children[i]);
                }

                return dagNode;
            }
            else if (node is LeafOctNode leafNode)
            {
                dagNode.ValidMask = leafNode.validmask;
                for (int i = 0; i < 8; i++)
                {
                    if (leafNode.HasChild(i))
                        dagNode.AddChildren((uint)octNode.children[i]);
                }

                return dagNode;
            }
            
            throw new Exception("Failed to create dag node!");
        }

        public int CalculateGeometryByteSize(int nodeCount, int pointerCount)
        {
            return (nodeCount + pointerCount) * 4;
        }


        public override void WriteHeader(BinaryWriter bw)
        {
            bw.Write(MaxDepth);
            bw.Write(CountDagNodes());
            bw.Write(CountDagNodeChildren());

            for (int i = 0; i < MaxDepth; i++)
            {
                bw.Write((int)nodeIdLevels[i].Count);
            }

        }
        public override void ReadHeader(BinaryReader br, ref DagHeader header)
        {
            var maxDepth = br.ReadInt32();
            var nodeCount = br.ReadInt32();
            var pointerCount = br.ReadInt32();

            var nodesPerLevel = new uint[maxDepth];
            for (int i = 0; i < maxDepth; i++)
            {
                nodesPerLevel[i] = br.ReadUInt32();
            }

            header.maxDepth = maxDepth;
            header.SetGeometryData(nodeCount, pointerCount, (nodeCount + pointerCount) * 4, nodesPerLevel);
        }
        public override void WriteToFile(BinaryWriter bw)
        {
            for (int level = 0; level < MaxDepth; level++)
            {
                Debug.Log($"Writing level {level} ...");

                var currLvlNodeCount = nodeIdLevels[level].Count;
                var currLvlChildCount = GetChildrenCountLevel(level);

                var currLvlNodeNum = 0;
                var currLvlChildNum = 0;
                for (int i = 0; i < currLvlNodeCount; i++)
                {
                    uint nodeId = nodeIdLevels[level][i];
                    var node = nodes[nodeId];

                    bw.Write((int)node.ValidMask);

                    for (int j = 0; j < node.ChildrenCount; j++)
                    {
                        //Write empty pointers in last level
                        if (level >= MaxDepth - 1)
                        {
                            bw.Write((int) 0);
                            continue;
                        }

                        currLvlChildNum++;

                        var childNodeId = node.GetChildIndex(j);
                        var indexNodeNextLvl = GetNodeIndexLevel(childNodeId);
                        var childrenCountNextLvlBeforeChild = GetChildrenCountLevelBeforeNode(level + 1, childNodeId);

                        //See OneNote dag 
                        int relativeChildPtr = (int)(currLvlNodeCount + currLvlChildCount 
                            - currLvlChildNum - currLvlNodeNum + indexNodeNextLvl + childrenCountNextLvlBeforeChild);
                        bw.Write(relativeChildPtr);
                    }


                    currLvlNodeNum++;
                }
            }
        }

        public override RenderData ReadFromFile(BinaryReader br)
        {
            DagHeader h = new DagHeader();
            ReadHeader(br, ref h);

            const int BYTES_PER_NODE = 4;

            uint nodeByteSize = (uint)((h.NodeCount + h.PointerCount) * BYTES_PER_NODE);

            unsafe
            {
                byte* geometryPtr = (byte*) Marshal.AllocHGlobal(new IntPtr(nodeByteSize)).ToPointer();
                br.ReadBytesLong(geometryPtr, nodeByteSize, Int16.MaxValue);

                return new DagRenderData(DagFormat.Gray, geometryPtr, (uint)h.GeometryByteSize, h.maxDepth);
            }
        }

        public unsafe void ReadFromFile<T>(BinaryReader br, NativeArray<T> cbArray, out int maxDepth) where T : unmanaged
        {
            DagHeader h = new DagHeader();
            ReadHeader(br, ref h);

            if (cbArray.Length < h.GeometryByteSize / sizeof(T)) 
                throw new Exception($"Bigger chunk size required for chunk with size: {h.GeometryByteSize:##0.#} B");

            maxDepth = h.maxDepth;
            br.ReadBytesLong((byte*)NativeArrayUnsafeUtility.GetUnsafePtr(cbArray), h.GeometryByteSize, Int16.MaxValue);
        }

        public unsafe void ReadFromFile(BinaryReader br, int[] cbArray, out int maxDepth)
        {
            DagHeader h = new DagHeader();
            ReadHeader(br, ref h);

            if (cbArray.Length < h.GeometryByteSize / sizeof(int))
                throw new Exception($"Bigger chunk size required for chunk with size: {h.GeometryByteSize:##0.#} B");

            maxDepth = h.maxDepth;
            fixed (int* ptr = cbArray)
            {
                br.ReadBytesLong((byte*)ptr, h.GeometryByteSize, Int16.MaxValue);
            }
        }
    }
}