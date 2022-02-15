using Assets.Scripts.Octree;
using Assets.Scripts.Voxelization.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
using Unity.Mathematics;
using UnityEngine;
using Utils;

namespace Assets.Scripts.Dag.Builders
{
    public class DagBuilderColorPerNode : DagBuilderColor<DagNodeColorPerNode>
    {
        public override void ConstructDagMemory(SvoInfo octInfo)
        {
            var format = octInfo.GetFormat();
            if (format != SvoInfo.SvoFormat.ColorFull) throw new Exception("Only supported format is: ColorFull");

            using (var reader = octInfo.GetRandomFileReader())
            {
                Init(reader.MaxDepth, reader.NodeCount);
                LoadSVODepthFirst(reader, (uint parentIdx, uint idx, SvoNodeColor svoNode) =>
                {
                    var dagNode = new DagNodeColorPerNode(parentIdx, svoNode);
                    nodes[parentIdx].AddChildren(idx);
                    dagNode.SvoParent = (parentIdx);

                    return dagNode;
                });
            }

            RemoveLeafNodeColorChildren();
            Reduce();
            CountSubtree(MaxDepth - 1);

            GenerateNodeIdLevelList();
            AccumulateChildrenCountLevel();
        }

        //public override void ConstructDag(SvoColorUnmanaged<SvoNodeColor> oct)
        //{
        //    base.ConstructDag(oct);

        //    base.LoadSVODepthFirst((uint)oct.Count, oct, (uint parentIdx, uint idx, SvoNodeColor svoNode) =>
        //    {
        //        var dagNode = new DagNodeColorPerNode(parentIdx, svoNode);
        //        nodes[parentIdx].AddChildren(idx);
        //        dagNode.parents.Add(parentIdx);

        //        return dagNode;
        //    });

        //    ConstructLevels(0);
        //    Reduce();
        //    CleanupUnusedLevels();
        //    CountSubtree(MaxDepth-1);

        //    GenerateNodeIdLevelList();
        //    AccumulateChildrenCountLevel();
        //}

        public void CountSubtree(int level)
        {
            if (level < 0) return;

            foreach (var nodeIdx in nodeIdLevels[level])
            {
                ref var node = ref nodes[nodeIdx];

                uint count = 1;         //+1 for the node itsself
                if (level == MaxDepth - 1)      //Count the bits in validmask for leaf nodes because they are all nodes
                {
                    count += (uint) math.countbits((int) node.ValidMask);
                }

                foreach (var childNodeIdx in node.children)
                {
                    var childNode = nodes[childNodeIdx];

                    count += childNode.subtreeNodeCount;
                }

                node.subtreeNodeCount = count;
            }

            CountSubtree(level - 1);
        }

        private void RemoveLeafNodeColorChildren()
        {
            foreach (var nodeId in nodeIdLevels[nodeIdLevels.Count - 1])
            {
                nodes[nodeId].children.Clear();
            }
        }

        private uint CountLeafNodes()
        {
            return (uint) nodeIdLevels[nodeIdLevels.Count - 1].Count;
        }

        public override void WriteHeader(BinaryWriter bw)
        {
            bw.Write(MaxDepth);
            bw.Write(CountDagNodes());
            bw.Write(CountDagNodeChildren());
            bw.Write(CountBrickLevelChildren());
            bw.Write(colors.Length);

            for (int i = 0; i < MaxDepth; i++)
            {
                bw.Write((int)nodeIdLevels[i].Count);
            }

        }

        public int CalculateGeometryByteSize(int nodeCount, int pointerCount, int leafNodeCount)
        {
            return (((nodeCount - leafNodeCount) * 2)       
                + leafNodeCount
                + pointerCount) * 4;
        }

        public override void ReadHeader(BinaryReader br, ref DagHeader header)
        {
            var maxDepth = br.ReadInt32();
            var nodeCount = br.ReadInt32();
            var pointerCount = br.ReadInt32();
            var bricklvlChildrenCount = br.ReadInt32();
            var colorCount = br.ReadInt32();

            var nodesPerLevel = new uint[maxDepth];
            for (int i = 0; i < maxDepth; i++)
            {
                nodesPerLevel[i] = br.ReadUInt32();
            }

            var geomByteSize = CalculateGeometryByteSize(nodeCount, pointerCount, (int)nodesPerLevel[maxDepth - 1]);

            header.maxDepth = maxDepth;
            header.brickLvlChildrenCount = bricklvlChildrenCount;
            header.SetGeometryData(nodeCount, pointerCount, geomByteSize, nodesPerLevel);
            header.SetColorData(colorCount, colorCount * 4);
        }

        //public override void WriteDataToFile(BinaryWriter bw)
        //{
        //    const int NODE_HEADER_SIZE = 2;

        //    for (int level = 0; level < MaxDepth; level++)
        //    {
        //        Debug.Log($"Writing level {level} ...");

        //        var currLvlNodeCount = nodeIdLevels[level].Count * NODE_HEADER_SIZE;
        //        var currLvlChildCount = GetChildrenCountLevel(level);

        //        var currLvlNodeNum = 0;
        //        var currLvlChildNum = 0;
        //        for (int i = 0; i < nodeIdLevels[level].Count; i++)
        //        {
        //            uint nodeId = nodeIdLevels[level][i];
        //            var node = nodes[nodeId];

        //            bw.Write((int)node.ValidMask);
        //            if (level < MaxDepth - 1) bw.Write((int)node.subtreeNodeCount);

        //            for (int j = 0; j < node.children.Count; j++)
        //            {
        //                currLvlChildNum++;

        //                var childNodeId = node.children[j];
        //                var indexNodeNextLvl = GetNodeIndexLevel(childNodeId);
        //                if (level < MaxDepth - 1) indexNodeNextLvl *= NODE_HEADER_SIZE;

        //                var childrenCountNextLvlBeforeChild = GetChildrenCountLevelBeforeNode(level + 1, childNodeId);

        //                //See OneNote dag 
        //                int relativeChildPtr = (int)(currLvlNodeCount + currLvlChildCount - currLvlChildNum - currLvlNodeNum + indexNodeNextLvl + childrenCountNextLvlBeforeChild);
        //                bw.Write(relativeChildPtr);
        //            }

        //            currLvlNodeNum+= NODE_HEADER_SIZE;
        //        }
        //    }
        //}

        public override void WriteDataToFile(BinaryWriter bw)
        {
            var leafNodeDepth = MaxDepth - 1;  //We only write pointers till the penultimate level because all the child information is already stored in the parent

            for (int level = 0; level < leafNodeDepth - 1; level++)
            {
                Debug.Log($"Writing level {level} ...");

                const int NODE_HEADER_SIZE = 2;
                WriteLevel(bw, level, NODE_HEADER_SIZE, NODE_HEADER_SIZE);
            }

            Debug.Log($"Writing level {leafNodeDepth - 1} ...");

            //Write penultimate level in respect to last level (no childpointers and headers are of size 1)
            WriteLevel(bw, leafNodeDepth - 1, 2, 1);


            Debug.Log($"Writing level {leafNodeDepth} ...");

            //Write the last level without pointers and without the subtreeNodeCount because we can infer it from the validmask
            for (int i = 0; i < nodeIdLevels[leafNodeDepth].Count; i++)
            {

                uint nodeId = nodeIdLevels[leafNodeDepth][i];
                var node = nodes[nodeId];

                bw.Write((int)node.ValidMask);
            }
        }

        private void WriteLevel(BinaryWriter bw, int level, int currLevelHeaderMultiplier, int nextLevelHeaderMultiplier)
        {
            int currLvlNodeNum = 0;
            int currLvlChildNum = 0;
            int currLvlNodeCount = nodeIdLevels[level].Count * currLevelHeaderMultiplier;
            int currLvlChildCount = GetChildrenCountLevel(level);
            for (int i = 0; i < nodeIdLevels[level].Count; i++)
            {
                uint nodeId = nodeIdLevels[level][i];
                var node = nodes[nodeId];

                bw.Write((int)node.ValidMask);
                bw.Write((int)node.subtreeNodeCount);

                for (int j = 0; j < node.children.Count; j++)
                {
                    currLvlChildNum++;

                    var childNodeId = node.children[j];
                    var indexNodeNextLvl = GetNodeIndexLevel(childNodeId) * nextLevelHeaderMultiplier;
                    var childrenCountNextLvlBeforeChild = GetChildrenCountLevelBeforeNode(level + 1, childNodeId);

                    //See OneNote dag 

                    int relativeChildPtr;
                    relativeChildPtr = (int)(
                        currLvlNodeCount + currLvlChildCount - currLvlChildNum - currLvlNodeNum
                        + indexNodeNextLvl + childrenCountNextLvlBeforeChild
                        - 1
                    );

                    bw.Write(relativeChildPtr);
                }

                currLvlNodeNum += currLevelHeaderMultiplier;
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

                byte* colorPtr = (byte*)Marshal.AllocHGlobal(h.ColorByteSize).ToPointer();
                var colorData = br.ReadBytes(h.ColorByteSize);
                fixed (byte* colorDataPtr = colorData)
                {
                    UnsafeMemory.CopyMemory(new IntPtr(colorPtr), new IntPtr(colorDataPtr), (uint)h.ColorByteSize);
                }

                return new DagRenderData(DagFormat.ColorPerNode, geometryPtr, (uint)h.GeometryByteSize, h.maxDepth, colorPtr, (uint)h.ColorByteSize);
            }
        }

        public override DagNodeColorPerNode CreateNode(uint index, int depth, IVoxelizerOctNode node)
        {
            throw new NotImplementedException();
        }
    }
}