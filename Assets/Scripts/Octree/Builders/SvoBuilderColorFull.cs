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

    //Adds one extra level at the bottom to store a node with the colorIndex
    //maxDepth + 1 but the last level is not raytraced it is only used for colorStorage

    //struct LeafNode
    //{
    //    public int childrenRelativeIndex;
    //    public int colorIndex;    
    //}
    public class SvoBuilderColorFull : ISvoBuilder
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

            public long GetNodeByteSize()
            {
                var nodes = nodesPerLevel.Take(maxDepth).Sum(x => x);
                var lastLevelNodes = nodesPerLevel[maxDepth];

                return nodes * 8u + lastLevelNodes * 8u;
            }
            
            public long GetNodeByteSize(int depth)
            {
                var nodes = nodesPerLevel.Take(depth).Sum(x => x);
                var lastLevelNodes = nodesPerLevel[maxDepth];

                return nodes * 8u + lastLevelNodes * 8u;
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

            header.nodesPerLevel = new uint[header.maxDepth + 1];
            for (int i = 0; i < header.maxDepth + 1; i++)
            {
                header.nodesPerLevel[i] = br.ReadUInt32();
            }
        }

        public void WriteHeader(BinaryWriter bw, IVoxelizer voxelizer)
        {
            QuantitizeColorsAndCountColorIndexNodes(voxelizer, out var colorNodesCount);

            bw.Write((int)SvoFormat.ColorFull);
            bw.Write(voxelizer.MaxDepth);
            bw.Write(colors.Count);

            for (int i = 0; i < voxelizer.MaxDepth; i++)
            {
                bw.Write(voxelizer.NodesPerDepth[i]);
            }

            bw.Write((uint)colorNodesCount);
        }

        public unsafe SvoRenderData ReadFromFile(BinaryReader br)
        {
            ReadHeader(br);
            var nodeByteSize = header.GetNodeByteSize();
            var colorByteSize = header.GetColorByteSize();

            if (nodeByteSize > uint.MaxValue) throw new Exception("Data can not be bigger than 2GB to visualize");

            byte* ptr = (byte*)Marshal.AllocHGlobal(new IntPtr(nodeByteSize)).ToPointer();
            br.ReadBytesLong(ptr, nodeByteSize, Int16.MaxValue);

            byte* colorPtr = (byte*)Marshal.AllocHGlobal(new IntPtr(colorByteSize)).ToPointer();
            br.ReadBytesLong(colorPtr, nodeByteSize, Int16.MaxValue);

            return new SvoRenderData(SvoFormat.ColorFull, ptr, (uint) nodeByteSize, header.maxDepth, colorPtr, colorByteSize);
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
            List<int> leafLevelColorIdx = new List<int>();

            for (int i = 0; i < maxDepth; i++)
            {
                childIdxIterationOrder = nextChildIdxIterationOrder;
                if (i != maxDepth - 1)
                    nextChildIdxIterationOrder = new int[nodesPerDepth[i + 1]];

                uint currentDepthNodeCount = nodesPerDepth[i];
                uint currentDepthChildrenCounter = 0;
                uint currentDepthNodeCounter = 0;

                SvoNodeColor outNode = new SvoNodeColor();
                for (int j = 0; j < childIdxIterationOrder.Length; j++)
                {
                    var childIdx = childIdxIterationOrder[j];
                    var voxedNode = voxelizer.GetNode(i, childIdx);

                    //tree node
                    if (voxedNode is ColorOctNode node)
                    {
                        outNode.ValidMask = node.validmask;
                        outNode.childrenRelativeIndex = currentDepthNodeCount + currentDepthChildrenCounter - currentDepthNodeCounter;

                        for (int k = 0; k < 8; k++)
                        {
                            if (node.HasChild(k))
                                nextChildIdxIterationOrder[currentDepthChildrenCounter++] = node.children[k];
                        }
                        
                        bw.Write(*(long*)&outNode);
                    }
                    //leaf node
                    else if (voxedNode is LeafOctNode leafNode)
                    {
                        outNode.ValidMask = leafNode.validmask;
                        outNode.childrenRelativeIndex = currentDepthNodeCount + currentDepthChildrenCounter - currentDepthNodeCounter;

                        for (int k = 0; k < 8; k++)
                        {
                            if (leafNode.HasChild(k))
                            {
                                var colIdx = colors[leafNode.colors[k]];
                                leafLevelColorIdx.Add(colIdx);
                                currentDepthChildrenCounter++;
                            }
                        }

                        bw.Write(*(long*)&outNode);
                    }

                    currentDepthNodeCounter++;
                }
            }

            for (int i = 0; i < leafLevelColorIdx.Count; i++)
            {
                SvoNodeColor leafColorNode = new SvoNodeColor();

                leafColorNode.ColorIndex = (uint)leafLevelColorIdx[i];
                bw.Write(*(long*)&leafColorNode);
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
            var validMaskColorIdx = br.ReadInt32();
            if (nodeIdx < leafNodeStartIdx)
            {
                var octNode = new OctNode();
                octNode.validmask = (byte)(validMaskColorIdx & 0xFF);

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
                octNode.validmask = (byte)(validMaskColorIdx & 0xFF);

                var streamPos = br.BaseStream.Position;

                //int childCount = 0;
                for (int i = 0; i < 8; i++)
                {
                    var baseChildIdx = nodeIdx + childBaseIndex;
                    br.BaseStream.Seek(baseChildIdx * 4, SeekOrigin.Begin);

                    if (octNode.HasChild(i))
                    {
                        //var childIdx = nodeIdx + childBaseIndex + childCount++;
                        //br.BaseStream.Seek(childIdx * 4, SeekOrigin.Begin);

                        octNode.colors[i] = br.ReadInt32() >> 8;
                    }
                }

                br.BaseStream.Seek(streamPos, SeekOrigin.Begin);


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


        public unsafe void QuantitizeColorsAndCountColorIndexNodes(IVoxelizer vox, out uint colorNodesCount)
        {
            colors = new Dictionary<int, int>();
            colorNodesCount = 0;

            for (int i = 0; i < vox.NodesPerDepth[vox.MaxDepth - 1]; i++)
            {
                var node = (LeafOctNode)vox.GetNode(vox.MaxDepth - 1, i);
                for (int k = 0; k < 8; k++)
                {
                    int col = node.colors[k];
                    Color32 colCol = *(Color32*)&col;
                    if (node.HasChild(k))
                    {
                        colorNodesCount++;
                        if (!colors.ContainsKey(col))
                        {
                            colors.Add(col, colors.Count);
                        }
                    }
                }
            }
        }


#if UNITY_EDITOR
        private string path;

        private int maxDepth;
        private string nodeCount;
        private string pointerCount;
        private ByteFormat geomByteSize;

        private uint[] nodesPerLevel;
        private bool depthNodesCountFoldout = true;


        private string colorCount;
        private ByteFormat colorByteSize;
        private bool colorFoldout = false;
        private (Color32 color, int nodeCount)[] colorEditor;
        private int voxelCount;

        private int selectedMaxDepth;
        

        public void InitEditorGUI(BinaryReader br, bool enableDeepAnalysis)
        {
            var nodes = header.nodesPerLevel.Sum(x => x);

            maxDepth = header.maxDepth;
            nodeCount = $"{nodes:#,##0.}";
            pointerCount = $"{nodes:#,##0.}";
            geomByteSize = new ByteFormat(header.GetNodeByteSize());
            nodesPerLevel = header.nodesPerLevel;

            colorCount = $"{header.colorCount:#,##0.}";
            colorByteSize = new ByteFormat(header.GetColorByteSize());
            selectedMaxDepth = header.maxDepth;

            if (!enableDeepAnalysis) return;
            
            colorEditor = new (Color32, int)[header.colorCount];
            for (int i = 0; i < NodeCount; i++)
            {
                var ptr = br.ReadInt32();
                var validColorIndex = br.ReadInt32();
                var colIdx = validColorIndex >> 8;
            
                colorEditor[colIdx].nodeCount++; // = colorEditor[colIdx].color++;
            }
            
            
            var colorStartByte = header.HeaderByteSize + header.GetNodeByteSize();
            br.BaseStream.Seek(colorStartByte, SeekOrigin.Begin);
            for (int i = 0; i < header.colorCount; i++)
            {
                var colInt = br.ReadInt32();
                unsafe
                {
                    colorEditor[i] = (*(Color32*)&colInt, colorEditor[i].nodeCount);
                }
            }
            
            var lastLeveltSartByte = header.HeaderByteSize + header.GetNodeByteSize(header.maxDepth-1);
            br.BaseStream.Seek(lastLeveltSartByte, SeekOrigin.Begin);
            for (int i = 0; i < header.nodesPerLevel[maxDepth-1]; i++)
            {
                var childPtr = br.ReadInt32();
                var validLeafMaskMask = br.ReadInt32();
                voxelCount += math.countbits(validLeafMaskMask & 0x00FF);
            }
        }

        public void OnEditorGUI()
        {
            EditorGUILayout.EnumPopup("Format", SvoFormat.ColorFull);
            EditorGUILayout.TextField("Max Depth", maxDepth + " (+ 1)");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Geometry Data", EditorStyles.boldLabel);
            EditorGUILayout.TextField("Node Count", nodeCount);
            EditorGUILayout.TextField("Pointer Count", pointerCount);
            ByteFormat.InspectorField("Data size", geomByteSize);

            depthNodesCountFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(depthNodesCountFoldout, "Nodes per level");
            uint accNodeCount = 0;
            for (int i = 0; i < nodesPerLevel.Length&& depthNodesCountFoldout; i++)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginHorizontal();

                if (i < nodesPerLevel.Length - 1)
                {
                    EditorGUILayout.LabelField($"Level {i + 1}", GUILayout.Width(100));
                    EditorGUILayout.TextField($"{nodesPerLevel[i]:#,##0.}");
                    EditorGUILayout.TextField($"{(accNodeCount += nodesPerLevel[i]):#,##0.}");
                }
                else
                {
                    EditorGUILayout.LabelField($"Color level", GUILayout.Width(100));
                    EditorGUILayout.TextField($"{nodesPerLevel[i]:#,##0.}");
                    EditorGUILayout.TextField($"{(accNodeCount += nodesPerLevel[i]):#,##0.}");
                }

                EditorGUILayout.EndHorizontal();
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();


            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Color Data", EditorStyles.boldLabel);
            EditorGUILayout.TextField("Color Count", colorCount);
            ByteFormat.InspectorField("Color size", colorByteSize);

            if (colorEditor != null)
            {
                colorFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(colorFoldout, "Color Table");
                for (int i = 0; i < colorEditor.Length && colorFoldout; i++)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.BeginHorizontal();

                    EditorGUILayout.ColorField(colorEditor[i].color);
                    EditorGUILayout.LabelField(colorEditor[i].nodeCount.ToString(), GUILayout.Width(100));

                    EditorGUILayout.EndHorizontal();
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
            }
            if (voxelCount != 0) EditorGUILayout.TextField("Voxel Count", $"{voxelCount:#,##0.}");
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Statistics", EditorStyles.boldLabel);
            ByteFormat.InspectorField("Overall size", geomByteSize + colorByteSize);


            EditorGUILayout.LabelField("______________________________________", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Bricked Color Levels Opt Stats (no ptrs in last level, validmask + color indices(determined from vm))", EditorStyles.boldLabel);
            
            //
            selectedMaxDepth = EditorGUILayout.IntSlider("Depth",selectedMaxDepth, 1, maxDepth);

            var nodes = nodesPerLevel.Take(selectedMaxDepth-1).Sum(x => x);
            var lastLevelNodes = nodesPerLevel[selectedMaxDepth-1];
            var brickedLevelNodes = nodesPerLevel[selectedMaxDepth];
            
            var byteSize = new ByteFormat(nodes * 8u + (lastLevelNodes+brickedLevelNodes) * 4u);
            ByteFormat.InspectorField("Overall size", byteSize);
        }
#endif

    }
}