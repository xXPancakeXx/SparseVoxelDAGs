using Assets.Scripts.Dag.Builders;
using Assets.Scripts.Octree;
using Assets.Scripts.Voxelization.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using Utils;

namespace Assets.Scripts.Dag
{
    public unsafe class DagBuilderColorPerPointer : DagBuilderColor<DagNodeColorPerPointer>
    {
        public override DagNodeColorPerPointer CreateNode(uint index, int depth, IVoxelizerOctNode node)
        {
            throw new NotImplementedException();
        }

        public override void ConstructDagMemory(SvoInfo octInfo)
        {
            var format = octInfo.GetFormat();
            if (format != SvoInfo.SvoFormat.ColorFull) throw new Exception("Only supported format is: ColorFull");

            using (var reader = octInfo.GetRandomFileReader())
            {
                Init(reader.MaxDepth, reader.NodeCount);
                LoadSVODepthFirst(reader, (uint parentIdx, uint idx, SvoNodeColor svoNode) =>
                {
                    var dagNode = new DagNodeColorPerPointer(parentIdx, svoNode);
                    nodes[parentIdx]?.children.Add((idx, idx - parentIdx));
                    dagNode.SvoParent = parentIdx;

                    return dagNode;
                });
            }

            RemoveLeafNodeColorChildren();
            Reduce();

            GenerateNodeIdLevelList();
            AccumulateChildrenCountLevel();
        }

        private void RemoveLeafNodeColorChildren()
        {
            foreach (var nodeId in nodeIdLevels[nodeIdLevels.Count - 1])
            {
                nodes[nodeId].children.Clear();
            }
        }

        public override void WriteHeader(BinaryWriter bw)
        {
            bw.Write(MaxDepth);
            bw.Write(CountDagNodes());
            bw.Write(CountDagNodeChildren());
            bw.Write(colors.Length);

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
            var colorCount = br.ReadInt32();

            var nodesPerLevel = new uint[maxDepth];
            for (int i = 0; i < maxDepth; i++)
            {
                nodesPerLevel[i] = br.ReadUInt32();
            }

            header.maxDepth = maxDepth;
            header.SetGeometryData(nodeCount, pointerCount, (nodeCount + pointerCount * 2) * 4, nodesPerLevel);
            header.SetColorData(colorCount, colorCount * 4);
        }

        public override void WriteDataToFile(BinaryWriter bw)
        {
            var writtenNodes = 0;
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

                    int validMask = node.ValidMask;
                    bw.Write(validMask);
                    writtenNodes++;

                    for (int j = 0; j < node.children.Count; j++)
                    {
                        currLvlChildNum += 2;

                        (var childNodeId, var attributeOffset) = node.children[j];

                        var indexNodeNextLvl = GetNodeIndexLevel(childNodeId);
                        var childrenCountNextLvlBeforeChild = GetChildrenCountLevelBeforeNode(level + 1, childNodeId) * 2;
                        int currLevelReferenceCount = currLvlChildCount * 2;

                        //See OneNote dag 
                        int relativeChildPtr = (int)(currLvlNodeCount + currLevelReferenceCount - currLvlChildNum - currLvlNodeNum + indexNodeNextLvl + childrenCountNextLvlBeforeChild);
                        bw.Write(relativeChildPtr + 1);
                        bw.Write(attributeOffset);
                        writtenNodes += 2;
                    }

                    currLvlNodeNum++;
                }
            }
        }

        public override RenderData ReadFromFile(BinaryReader br)
        {
            DagHeader h = new DagHeader();
            ReadHeader(br, ref h);

            unsafe
            {
                var geometryPtr = (byte*)Marshal.AllocHGlobal(new IntPtr(h.GeometryByteSize)).ToPointer();
                br.ReadBytesLong(geometryPtr, h.GeometryByteSize, Int16.MaxValue);

                var colorPtr = (byte*)Marshal.AllocHGlobal(h.ColorByteSize).ToPointer();
                var colorData = br.ReadBytes(h.ColorByteSize);
                fixed (byte* colorDataPtr = colorData)
                {
                    UnsafeMemory.CopyMemory(new IntPtr(colorPtr), new IntPtr(colorDataPtr), (uint)h.ColorByteSize);
                }

                return new DagRenderData(DagFormat.ColorPerPointer, geometryPtr, (uint)h.GeometryByteSize, h.maxDepth, colorPtr, (uint)h.ColorByteSize);
            }
        }

    }
}