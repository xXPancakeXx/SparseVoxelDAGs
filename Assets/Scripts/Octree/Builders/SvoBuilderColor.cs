using Assets.Scripts.Voxelization;
using Assets.Scripts.Voxelization.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Utils;
using static Assets.Scripts.Octree.SvoInfo;

namespace Assets.Scripts.Octree.Builders
{
    //struct InnerNode
    //{
    //    public int childrenRelativeIndex;
    //    public int validMaskColorIndices;
    //}

    //struct LeafNode
    //{
    //    public int validMask;
    //    public int colorIndex;    1 to 8 colorIndices (depends on validMask)
    //}
    public class SvoBuilderColor : ISvoBuilder
    {
        private Header header;
        private Dictionary<int, int> colors;

        public int MaxDepth => header.maxDepth;
        public uint NodeCount => (uint)header.nodesPerLevel.Sum(x => x);
        public long HeaderByteSize => header.HeaderByteSize;
        public long NodeByteSize => header.GetNodeByteSize();
        public long ColorByteSize => header.GetColorByteSize();

        public class Header
        {
            public int maxDepth;
            public uint colorCount;

            public uint[] nodesPerLevel;

            public uint GetNodeByteSize()
            {
                var nodes = nodesPerLevel.Take(maxDepth - 1).Sum(x => x);
                var lastLevelNodes = nodesPerLevel[maxDepth - 1];

                return (uint)(nodes * 8u + lastLevelNodes * 8u);
            }

            public uint GetColorByteSize()
            {
                return colorCount * 4u;
            }

            public int HeaderByteSize => 
                4 * (
                      1 //for the format 
                    + 1 //for maxdepth 
                    + 1 //for colorcount
                    + nodesPerLevel.Length
                );
        }


        public void ReadHeader(BinaryReader br)
        {
            header = new Header();

            header.maxDepth = br.ReadInt32();
            header.colorCount = br.ReadUInt32();

            header.nodesPerLevel = new uint[header.maxDepth];
            for (int i = 0; i < header.maxDepth; i++)
            {
                header.nodesPerLevel[i] = br.ReadUInt32();
            }
        }

        public void WriteHeader(BinaryWriter bw, IVoxelizer voxelizer)
        {
            QuantitizeColors(voxelizer);

            bw.Write((int)SvoFormat.Color);
            bw.Write(voxelizer.MaxDepth);
            bw.Write(colors.Count);

            for (int i = 0; i < voxelizer.MaxDepth; i++)
            {
                bw.Write(voxelizer.NodesPerDepth[i]);
            }
        }

        public unsafe SvoRenderData ReadFromFile(BinaryReader br)
        {
            ReadHeader(br);
            var nodeByteSize = header.GetNodeByteSize();

            byte* ptr = (byte*)Marshal.AllocHGlobal(new IntPtr(nodeByteSize)).ToPointer();
            br.ReadBytesLong(ptr, nodeByteSize, Int16.MaxValue);

            return new SvoRenderData(SvoFormat.GrayFull, ptr, nodeByteSize, header.maxDepth);
        }

        public unsafe void WriteToFile(BinaryWriter bw, IVoxelizer voxelizer)
        {
            var maxDepth = voxelizer.MaxDepth;
            var requiredSizeBytes = voxelizer.NodeCount * sizeof(SvoNodeLaine);
            var nodesPerDepth = voxelizer.NodesPerDepth;

            Debug.Log("Required Size: " + requiredSizeBytes);
            WriteHeader(bw, voxelizer);

            int[] childIdxIterationOrder;
            int[] nextChildIdxIterationOrder = new int[] { 0 };

            for (int i = 0; i < maxDepth; i++)
            {
                childIdxIterationOrder = nextChildIdxIterationOrder;
                if (i != maxDepth - 1)
                    nextChildIdxIterationOrder = new int[nodesPerDepth[i + 1]];

                uint currentDepthNodeCount = nodesPerDepth[i];
                uint currentDepthChildrenCounter = 0;
                uint currentDepthNodeCounter = 0;
                uint nextDepthChildrenCount = 0;

                for (int j = 0; j < childIdxIterationOrder.Length; j++)
                {
                    var childIdx = childIdxIterationOrder[j];
                    var voxedNode = voxelizer.GetNode(i, childIdx);

                    //tree node
                    if (voxedNode is OctNode node)
                    {
                        SvoNodeColor outNode = new SvoNodeColor();

                        outNode.ValidMask = node.validmask;

                        if (i < maxDepth - 2)
                        {
                            outNode.childrenRelativeIndex = currentDepthNodeCount + currentDepthChildrenCounter - currentDepthNodeCounter;
                            for (int k = 0; k < 8; k++)
                            {
                                if (node.HasChild(k))
                                    nextChildIdxIterationOrder[currentDepthChildrenCounter++] = node.children[k];
                            }
                        }
                        //next lvl is leaf level
                        else
                        {
                            outNode.childrenRelativeIndex = currentDepthNodeCount + currentDepthChildrenCounter - currentDepthNodeCounter + nextDepthChildrenCount;

                            //TODO: Count leaf level children and add it to relative ptr
                            for (int k = 0; k < 8; k++)
                            {
                                if (node.HasChild(k))
                                {
                                    nextChildIdxIterationOrder[currentDepthChildrenCounter++] = node.children[k];
                                    //nextDepthChildrenCount += (uint) math.countbits((int) voxelizer.LeafNodes[node.children[k]].validmask);
                                }
                            }
                        }
                        bw.Write(*(long*)&outNode);
                    }
                    //leaf node
                    else if (voxedNode is LeafOctNode leafNode)
                    {
                        bw.Write((int) leafNode.validmask);
                        for (int k = 0; k < 8; k++)
                        {
                            if (leafNode.HasChild(k))
                            {
                                var colIdx = (uint)colors[leafNode.colors[k]];
                                bw.Write(colIdx);
                            }
                        }

                        continue;
                    }

                    currentDepthNodeCounter++;
                }
            }

            var colorStrideSize = 3 + 1;      //3 bytes + 1 byte padding
            var colorsByteSize = colors.Count * colorStrideSize;
            if (colorsByteSize >= 1 << 24) Debug.LogWarning("More than 2^24 colors contained in octree. This will lead to overflows");

            foreach (var col in colors)
            {
                var cc = col.Key;
                var colInt = *(int*)&cc;

                bw.Write(colInt);
            }
        }



        public unsafe bool ReadBreadth(BinaryReader br, out IVoxelizerOctNode node, out int depth)
        {
            uint nodeIdx = (uint)((br.BaseStream.Position - header.HeaderByteSize) / 8);
            if (nodeIdx >= NodeCount)
            {
                node = default;
                depth = -1;
                return false;
            }

            var leafNodeStartIdx = header.nodesPerLevel.Take(header.maxDepth - 1).Sum(x => x);
            depth = GetDepthFromNodeIdx(nodeIdx);

            var childBaseIndex = br.ReadInt32();
            var leafValidMask = br.ReadInt32();
            if (nodeIdx < leafNodeStartIdx)
            {
                var octNode = new OctNode();
                octNode.validmask = (byte)((leafValidMask >> 8) & 0xFF);

                int childCount = 0;
                for (int i = 0; i < 8; i++)
                {
                    if (octNode.HasChild(i)) octNode.children[i] = (int)(nodeIdx + childBaseIndex + childCount++);
                }

                node = octNode;
                return true;
            }
            else
            {
                var octNode = new LeafOctNode();
                octNode.validmask = (byte)((leafValidMask >> 8) & 0xFF);

                node = octNode;
                return true;
            }
        }

        private int GetDepthFromNodeIdx(uint nodeIdx)
        {
            uint maxNodeIdxDepth = 0;
            for (int i = 0; i < header.maxDepth; i++)
            {
                maxNodeIdxDepth += header.nodesPerLevel[i];
                if (nodeIdx < maxNodeIdxDepth) return i;
            }

            throw new Exception("Bigger nodeIdx passed than there are nodes!");
        }


        public unsafe void QuantitizeColors(IVoxelizer vox)
        {
            colors = new Dictionary<int, int>();

            for (int i = 0; i < vox.NodesPerDepth[vox.MaxDepth-1]; i++)
            {
                var node = (LeafOctNode) vox.GetNode(vox.MaxDepth - 1, i);
                for (int k = 0; k < 8; k++)
                {
                    int col = node.colors[k];

                    if (!colors.ContainsKey(col))
                        colors.Add(col, colors.Count);
                }
            }
        }


#if UNITY_EDITOR
        private string path;

        private int maxDepth;
        private string nodeCount;
        private string pointerCount;
        private ByteFormat geomByteSize;

        private uint[] depthNodesCount;
        private bool depthNodesCountFoldout = true;

        private string colorCount;
        private ByteFormat colorByteSize;



        public void InitEditorGUI(BinaryReader br, bool enableDeepAnalysis)
        {
            var nodes = header.nodesPerLevel.Sum(x => x);

            maxDepth = header.maxDepth;
            nodeCount = $"{nodes:#,##0.}";
            pointerCount = $"{nodes:#,##0.}";
            geomByteSize = new ByteFormat(header.GetNodeByteSize());
            depthNodesCount = header.nodesPerLevel;

            colorCount = $"{header.colorCount:#,##0.}";
            colorByteSize = new ByteFormat(header.GetColorByteSize());
        }

        public void OnEditorGUI()
        {
            EditorGUILayout.IntField("Max Depth", maxDepth);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Geometry Data", EditorStyles.boldLabel);
            EditorGUILayout.TextField("Node Count", nodeCount);
            EditorGUILayout.TextField("Pointer Count", pointerCount);
            ByteFormat.InspectorField("Data size", geomByteSize);

            depthNodesCountFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(depthNodesCountFoldout, "Nodes per level");
            uint accNodeCount = 0;
            for (int i = 0; i < depthNodesCount?.Length && depthNodesCountFoldout; i++)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.LabelField($"Level {i + 1}", GUILayout.Width(100));
                EditorGUILayout.TextField($"{depthNodesCount[i]:#,##0.}");
                EditorGUILayout.TextField($"{(accNodeCount += depthNodesCount[i]):#,##0.}");

                EditorGUILayout.EndHorizontal();
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();


            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Color Data", EditorStyles.boldLabel);
            EditorGUILayout.TextField("Color Count", colorCount);
            ByteFormat.InspectorField("Color size", colorByteSize);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Statistics", EditorStyles.boldLabel);
            ByteFormat.InspectorField("Overall size", geomByteSize + colorByteSize);
        }
#endif

    }
}