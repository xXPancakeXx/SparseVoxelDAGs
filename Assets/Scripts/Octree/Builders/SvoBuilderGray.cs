using Assets.Scripts.Voxelization;
using Assets.Scripts.Voxelization.Entities;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;
using Utils;
using static Assets.Scripts.Octree.SvoInfo;

namespace Assets.Scripts.Octree.Builders
{
    //struct InnerNode
    //{
    //    public int childrenRelativeIndex;
    //    public int leafvalidMask;
    //}

    //struct LeafNode
    //{
    //    public byte validmask;
    //}

    public class SvoBuilderGray : ISvoBuilder
    {
        private Header header;


        public int MaxDepth => header.maxDepth;
        public uint NodeCount => (uint) header.depthNodesCount.Sum(x => x);
        public long HeaderByteSize => header.HeaderByteSize;
        public long NodeByteSize => header.GetNodeByteSize();
        public long ColorByteSize => 0;

        public class Header
        {
            public int maxDepth;
            public uint[] depthNodesCount;

            public uint GetNodeByteSize()
            {
                var nodes = depthNodesCount.Take(maxDepth - 1).Sum(x => x);
                var lastLevelNodes = depthNodesCount[maxDepth - 1];

                return (uint)(nodes * 8u + lastLevelNodes * 1u);
            }

            public int HeaderByteSize => 4 * (
                  1 //for the format 
                + 1 //for maxdepth 
                + depthNodesCount.Length
            );
        }


        public void ReadHeader(BinaryReader br)
        {
            header = new Header();

            header.maxDepth = br.ReadInt32();

            header.depthNodesCount = new uint[header.maxDepth];
            for (int i = 0; i < header.maxDepth; i++)
            {
                header.depthNodesCount[i] = br.ReadUInt32();
            }
        }

        public void WriteHeader(BinaryWriter bw, IVoxelizer voxelizer)
        {
            bw.Write((int) SvoFormat.Gray);
            bw.Write(voxelizer.MaxDepth);
            for (int i = 0; i < voxelizer.MaxDepth; i++)
            {
                bw.Write(voxelizer.NodesPerDepth[i]);
            }
        }


        public unsafe SvoRenderData ReadFromFile(BinaryReader br)
        {
            ReadHeader(br);
            var nodeByteSize = header.GetNodeByteSize();

            byte* ptr = (byte*) Marshal.AllocHGlobal(new IntPtr(nodeByteSize)).ToPointer();
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

                for (int j = 0; j < childIdxIterationOrder.Length; j++)
                {
                    var childIdx = childIdxIterationOrder[j];
                    var voxedNode = voxelizer.GetNode(i, childIdx);

                    //always this in gray voxelization
                    if (voxedNode is OctNode node)
                    {
                        //before last 2 levels
                        if (i < maxDepth - 2)
                        {
                            var childrenRelativePointer = currentDepthNodeCount + currentDepthChildrenCounter - currentDepthNodeCounter;

                            for (int k = 0; k < 8; k++)
                            {
                                if (node.HasChild(k))
                                    nextChildIdxIterationOrder[currentDepthChildrenCounter++] = node.children[k];
                            }

                            bw.Write((int)childrenRelativePointer);
                            bw.Write((int)node.validmask);

                            currentDepthNodeCounter++;
                        }
                        //penultimate level
                        else if (i < maxDepth - 1)
                        {
                            var childrenRelativePointer = currentDepthNodeCount * 8 + currentDepthChildrenCounter - currentDepthNodeCounter * 8;

                            for (int k = 0; k < 8; k++)
                            {
                                if (node.HasChild(k))
                                    nextChildIdxIterationOrder[currentDepthChildrenCounter++] = node.children[k];
                            }

                            bw.Write((int) childrenRelativePointer);
                            bw.Write((int) node.validmask);

                            currentDepthNodeCounter++;
                        }
                        //leaf level
                        else
                        {
                            bw.Write(node.validmask);
                        }
                    }
                }
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

            var leafNodeStartIdx = header.depthNodesCount.Take(header.maxDepth - 1).Sum(x => x);
            depth = GetDepthFromNodeIdx(nodeIdx);

            if (nodeIdx < leafNodeStartIdx)
            {
                var childBaseIndex = br.ReadInt32();
                var leafValidMask = br.ReadInt32();

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
                var validMask = br.ReadByte();

                var octNode = new LeafOctNode();
                octNode.validmask = validMask;

                node = octNode;
                return true;
            }
        }

        private int GetDepthFromNodeIdx(uint nodeIdx)
        {
            uint maxNodeIdxDepth = 0;
            for (int i = 0; i < header.maxDepth; i++)
            {
                maxNodeIdxDepth += header.depthNodesCount[i];
                if (nodeIdx < maxNodeIdxDepth) return i;
            }

            throw new Exception("Bigger nodeIdx passed than there are nodes!");
        }


#if UNITY_EDITOR
        private string path;

        private int maxDepth;
        private string nodeCount;
        private string pointerCount;
        private ByteFormat geomByteSize;

        private uint[] depthNodesCount;
        private bool depthNodesCountFoldout = true;

      

        public void InitEditorGUI(BinaryReader br, bool enableDeepAnalysis)
        {
            var nodes = header.depthNodesCount.Sum(x => x);

            maxDepth = header.maxDepth;
            nodeCount = $"{nodes:#,##0.}";
            pointerCount = $"{nodes:#,##0.}";
            geomByteSize = new ByteFormat(header.GetNodeByteSize());

            depthNodesCount = header.depthNodesCount;
        }

        public void OnEditorGUI()
        {
            EditorGUILayout.IntField("Max Depth", maxDepth);
            EditorGUILayout.EnumPopup("Format", SvoFormat.Gray);

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


            EditorGUILayout.LabelField("______________________________________", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Last Level Opt Stats (no ptrs in last level, validmask + pading)", EditorStyles.boldLabel);

            var nodes = depthNodesCount.Take(maxDepth - 1).Sum(x => x);
            var lastLevelNodes = depthNodesCount[maxDepth - 1];

            var byteSize = new ByteFormat(nodes * 8u + lastLevelNodes * 4u);
            ByteFormat.InspectorField("Overall size", byteSize);

            EditorGUILayout.LabelField("______________________________________", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Last Level Opt Stats (no ptrs in last level, only validmask)", EditorStyles.boldLabel);

            byteSize = new ByteFormat(nodes * 8u + lastLevelNodes * 1u);
            ByteFormat.InspectorField("Overall size", byteSize);
        }

        
#endif
    }
}