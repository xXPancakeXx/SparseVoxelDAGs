using Assets.Scripts.Voxelization;
using Assets.Scripts.Voxelization.Entities;
using System;
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
    //    public int leafvalidMask;
    //}

    //struct LeafNode : InnerNode
    public class SvoBuilderGrayFull : ISvoBuilder
    {
        private Header header;


        public int MaxDepth => header.maxDepth;
        public uint NodeCount => (uint) header.nodesPerLevel.Sum(x => x);
        public long HeaderByteSize => header.HeaderByteSize;
        public long NodeByteSize => header.GetNodeByteSize();
        public long ColorByteSize => 0;

        public class Header
        {
            public int maxDepth;
            public uint[] nodesPerLevel;

            public uint GetNodeByteSize()
            {
                var nodes = nodesPerLevel.Take(maxDepth).Sum(x => x);
                return (uint)(nodes * 8u);
            }
            
            public uint GetNodeByteSize(int depth)
            {
                var nodes = nodesPerLevel.Take(depth).Sum(x => x);
                return (uint)(nodes * 8u);
            }

            public int HeaderByteSize => 4 * (
                  1 //for the format 
                + 1 //for maxdepth 
                + nodesPerLevel.Length
            );
        }


        public void ReadHeader(BinaryReader br)
        {
            header = new Header();

            header.maxDepth = br.ReadInt32();

            header.nodesPerLevel = new uint[header.maxDepth];
            for (int i = 0; i < header.maxDepth; i++)
            {
                header.nodesPerLevel[i] = br.ReadUInt32();
            }
        }

        public void WriteHeader(BinaryWriter bw, IVoxelizer voxelizer)
        {
            bw.Write((int) SvoFormat.GrayFull);
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

            SvoNodeLaine outNode = new SvoNodeLaine();
            for (int i = 0; i < maxDepth; i++)
            {
                childIdxIterationOrder = nextChildIdxIterationOrder;
                if (i == maxDepth - 1)
                    outNode.leafMask = 255;
                else
                    nextChildIdxIterationOrder = new int[nodesPerDepth[i + 1]];

                uint currentDepthNodeCount = nodesPerDepth[i];
                uint currentDepthChildrenCounter = 0;
                uint currentDepthNodeCounter = 0;

                for (int j = 0; j < childIdxIterationOrder.Length; j++)
                {
                    var childIdx = childIdxIterationOrder[j];
                    var voxedNode = voxelizer.GetNode(i, childIdx);

                    //always this in gray voxeliztion
                    if (voxedNode is OctNode node)
                    {
                        outNode.validMask = node.validmask;
                        outNode.childrenRelativePointer = currentDepthNodeCount + currentDepthChildrenCounter - currentDepthNodeCounter;

                        //only not leaf node
                        if (i < maxDepth - 1)
                        {
                            outNode.leafMask = (byte)~outNode.validMask;
                            for (int k = 0; k < 8; k++)
                            {
                                if (node.HasChild(k))
                                    nextChildIdxIterationOrder[currentDepthChildrenCounter++] = node.children[k];
                            }
                        }
                        
                    }

                    bw.Write(*(long*)&outNode);
                    currentDepthNodeCounter++;
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


#if UNITY_EDITOR
        private string path;

        private int maxDepth;
        private string nodeCount;
        private string pointerCount;
        private ByteFormat geomByteSize;

        private uint[] depthNodesCount;
        private bool depthNodesCountFoldout = true;
        private int voxelCount;

        private int selectedMaxDepth;
      

        public void InitEditorGUI(BinaryReader br, bool enableDeepAnalysis)
        {
            var nodes = header.nodesPerLevel.Sum(x => x);

            maxDepth = header.maxDepth;
            nodeCount = $"{nodes:#,##0.}";
            pointerCount = $"{nodes:#,##0.}";
            geomByteSize = new ByteFormat(header.GetNodeByteSize());
            selectedMaxDepth = maxDepth;

            depthNodesCount = header.nodesPerLevel;
            
            if (!enableDeepAnalysis) return;
            
            var lastLeveltSartByte = header.HeaderByteSize + header.GetNodeByteSize(maxDepth-1);
            br.BaseStream.Seek(lastLeveltSartByte, SeekOrigin.Begin);
            for (int i = 0; i < header.nodesPerLevel[maxDepth-1]; i++)
            {
                var childPtr = br.ReadInt32();
                var validLeafMaskMask = br.ReadInt32();
                voxelCount += math.countbits(validLeafMaskMask & 0xFF00);
            }
        }

        public void OnEditorGUI()
        {
            EditorGUILayout.IntField("Max Depth", maxDepth);
            EditorGUILayout.EnumPopup("Format", SvoFormat.GrayFull);

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

            if (voxelCount != 0) EditorGUILayout.TextField("Voxel Count", $"{voxelCount:#,##0.}");
            
            EditorGUILayout.LabelField("______________________________________", EditorStyles.boldLabel);
            selectedMaxDepth = EditorGUILayout.IntSlider("Depth",selectedMaxDepth, 1, maxDepth);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("No Optimizations (validmask + pading + ptr)", EditorStyles.boldLabel);
            
            var nodes = depthNodesCount.Take(selectedMaxDepth - 1).Sum(x => x);
            var lastLevelNodes = depthNodesCount[selectedMaxDepth - 1];
            
            var byteSize = new ByteFormat(nodes * 8u + lastLevelNodes * 8u);
            ByteFormat.InspectorField("Overall size", byteSize);
            
            EditorGUILayout.LabelField("______________________________________", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Last Level Opt Stats (no ptrs in last level, validmask + pading)", EditorStyles.boldLabel);

            byteSize = new ByteFormat(nodes * 8u + lastLevelNodes * 4u);
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