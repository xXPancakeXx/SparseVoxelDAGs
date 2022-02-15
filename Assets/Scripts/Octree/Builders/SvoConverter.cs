using Assets.Scripts.Octree.Readers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using Utils;
using static Assets.Scripts.Octree.SvoInfo;

namespace Assets.Scripts.Octree.Builders
{
    public class SvoConverter
    {
        private IVoxelConverterSVOReader reader;
        private BinaryWriter outWriter;

        private uint[] nodesPerDepth;
        private int maxDepth;

        public SvoConverter(IVoxelConverterSVOReader reader, BinaryWriter outWriter)
        {
            this.reader = reader;
            this.outWriter = outWriter;
        }


        public unsafe void ConvertFormatBreadth()
        {
            maxDepth = reader.MaxDepth - 1;

            uint requiredSizeBytes; nodesPerDepth = new uint[maxDepth];
            using (var sw = new SW("Collecting node count recursive per level took:"))
            {
                requiredSizeBytes = CalcSizeRecursive<SvoNodeLaine>(reader.NodesCount - 1, 0);
            }

            outWriter.Write((int)SvoFormat.GrayFull);
            outWriter.Write(maxDepth);
            for (int i = 0; i < maxDepth; i++)
            {
                outWriter.Write(nodesPerDepth[i]);
            }

        
            Debug.Log("Required Size: " + requiredSizeBytes);

            //WriteOctreeHeader(outWriter, new SvoHeader
            //{
            //    version = 3,
            //    maxDepth = maxDepth,
            //    nodes = (uint)(requiredSizeBytes / sizeof(SvoNodeLaine)),
            //    nodeType = NodeType.GrayLaine,
            //    order = HierachyOrder.Breadth,
            //    depthNodesCount = nodesPerDepth
            //});

            ulong biggestRelativePtr = 0;
            ulong currentPtrOffset = 0;
            ulong depthStartPtrOffset = 0;

            var toVisitNodesList = new LinkedList<(long impIndex, short depth)>();
            toVisitNodesList.AddFirst((reader.NodesCount - 1, 0));

            //all nodes of this depth
            uint currentDepthNodeCount = 0;
            //keeps track of the current node index in this depth
            uint currentDepthNodeCounter = 0;
            //keeps track of the number of children in this depth
            uint currentDepthChildrenCounter = 0;

            short currentDepth = -1;

            while (toVisitNodesList.Count > 0)
            {
                var currentNode = toVisitNodesList.First.Value;
                toVisitNodesList.RemoveFirst();
                var impNode = reader[currentNode.impIndex];

                //Next depth reached, assign
                if (currentDepth < currentNode.depth)
                {
                    currentDepth = currentNode.depth;
                    //Get nodes at this depth and check if we are in the last depth level
                    currentDepthNodeCount = nodesPerDepth[currentDepth]; //nodesPerDepth.ContainsKey(currentDepth) ? nodesPerDepth[currentDepth] : 0;
                    currentDepthChildrenCounter = 0;
                    currentDepthNodeCounter = 0;

                    depthStartPtrOffset = currentPtrOffset;

                    Debug.Log($"Reformatting level {currentDepth} / {maxDepth}\n Level size: {toVisitNodesList.Count}");
                }

                var node = new SvoNodeLaine();

                //(nodes in depth) + (already added children nodes) - (index of node in this depth)
                ulong offset = currentDepthNodeCount + currentDepthChildrenCounter - currentDepthNodeCounter;
                node.childrenRelativePointer = (uint)offset;
                if (offset > biggestRelativePtr) biggestRelativePtr = offset;

                byte childBitOffset = 1;
                for (uint i = 0; i < 8; i++)
                {
                    if (impNode.HasChild(i))
                    {
                        node.validMask |= childBitOffset;
                        if (currentDepth < maxDepth - 1)
                        {
                            long childIndex = (long)impNode.GetChildPos(i);
                            toVisitNodesList.AddLast((childIndex, (short)(currentNode.depth + 1)));
                            currentDepthChildrenCounter++;
                        }
                        else
                        {
                            node.leafMask |= childBitOffset;
                        }
                    }
                    else
                    {

                        node.leafMask |= childBitOffset;
                    }

                    childBitOffset <<= 1;
                }

                currentDepthNodeCounter++;
                //ptr[currentPtrOffset++] = node;
                outWriter.Write(*(long*)&node);
            }
        }

        //public unsafe void ConvertFormatBreadthFullAlloc()
        //{
        //    if (reader.NodesCount > uint.MaxValue) throw new Exception("too many nodes!");

        //    maxDepth = reader.MaxDepth - 1;

        //    uint requiredSizeBytes; nodesPerDepth = new uint[maxDepth];
        //    using (var sw = new SW("Collecting node count recursive per level took:"))
        //    {
        //        requiredSizeBytes = CalcSizeRecursive<SvoNodeLaine>(reader.NodesCount - 1, 0);
        //    }
        //    Debug.Log("Required Size: " + requiredSizeBytes);


        //    WriteOctreeHeader(outWriter, new SvoHeader
        //    {
        //        version = 3,
        //        maxDepth = maxDepth,
        //        nodes = (uint)(requiredSizeBytes / sizeof(SvoNodeLaine)),
        //        nodeType = NodeType.GrayLaine,
        //        order = HierachyOrder.Breadth,
        //        depthNodesCount = nodesPerDepth
        //    });

        //    ulong biggestRelativePtr = 0;
        //    uint[] nextDepthNodes = new uint[] { (uint)reader.NodesCount - 1 };
        //    for (short currentDepth = 0; currentDepth < maxDepth; currentDepth++)
        //    {
        //        uint[] currentDepthNodes = nextDepthNodes;
        //        if (currentDepth < maxDepth - 1) nextDepthNodes = new uint[nodesPerDepth[currentDepth + 1]];

        //        uint currentDepthChildrenCounter = 0;    //keeps track of the current node index in this depth
        //        uint currentDepthNodeCounter = 0;        //keeps track of the number of children in this depth
        //        Debug.Log($"Reformatting level {currentDepth} / {maxDepth - 1}\n Level size: {currentDepthNodes.Length}");

        //        for (int j = 0; j < currentDepthNodes.Length; j++)
        //        {
        //            var cNodeIdx = currentDepthNodes[j];
        //            var impNode = reader[cNodeIdx];
        //            var node = new SvoNodeLaine();

        //            //(nodes in depth) + (already added children nodes) - (index of node in this depth)
        //            ulong offset = nodesPerDepth[currentDepth] + currentDepthChildrenCounter - currentDepthNodeCounter;
        //            node.childrenRelativePointer = (uint)offset;
        //            if (offset > biggestRelativePtr) biggestRelativePtr = offset;

        //            byte childBitOffset = 1;
        //            for (uint i = 0; i < 8; i++)
        //            {
        //                if (impNode.HasChild(i))
        //                {
        //                    node.validMask |= childBitOffset;
        //                    if (currentDepth < maxDepth - 1)
        //                    {
        //                        nextDepthNodes[currentDepthChildrenCounter] = (uint)impNode.GetChildPos(i);
        //                        currentDepthChildrenCounter++;
        //                    }
        //                    else
        //                    {
        //                        node.leafMask |= childBitOffset;
        //                    }
        //                }
        //                else
        //                {

        //                    node.leafMask |= childBitOffset;
        //                }

        //                childBitOffset <<= 1;
        //            }

        //            currentDepthNodeCounter++;
        //            outWriter.Write(*(long*)&node);
        //        }
        //    }

        //}

        //public static unsafe SvoUnmanaged<SvoNodeLaine> ConvertFormat(VoxelConverterSVOInfo info, VoxelConverterSVONode* importedNodes)
        //{
        //    //The size of DenseOctreeLeafNode
        //    var nodeSize = sizeof(SvoNodeLaine);
        //    var requiredSizeBytes = 0;
        //    var maxDepth = info.MaxDepth - 1;

        //    //Keeps track of nodes per depth level
        //    var nodesPerDepth = new uint[maxDepth];

        //    var toVisitNodesList = new LinkedList<(long impIndex, short depth)>();
        //    toVisitNodesList.AddFirst(((long)info.nodes - 1, 0));

        //    var exit = false;
        //    while (toVisitNodesList.Count > 0 && !exit)
        //    {
        //        var currentNode = toVisitNodesList.First.Value;
        //        toVisitNodesList.RemoveFirst();
        //        var depth = currentNode.depth;

        //        //Last depth only contains leafs which do not need to be stored or even loaded in memory
        //        if (depth < maxDepth - 1)
        //        {
        //            var impNode = importedNodes[currentNode.impIndex];
        //            for (uint i = 0; i < 8; i++)
        //            {
        //                if (!impNode.HasChild(i)) continue;

        //                long childIndex = (long)impNode.GetChildPos(i);
        //                toVisitNodesList.AddFirst((childIndex, (short)(currentNode.depth + 1)));
        //            }
        //        }

        //        nodesPerDepth[depth]++;
        //        requiredSizeBytes += nodeSize;
        //    }

        //    if (exit) throw new Exception("Exited");

        //    Debug.Log("RequiredMemory: " + requiredSizeBytes + " B");

        //    IntPtr intptr = Marshal.AllocHGlobal(requiredSizeBytes);
        //    UnsafeMemory.FillMemory(intptr, (uint)requiredSizeBytes, 0);
        //    SvoNodeLaine* ptr = (SvoNodeLaine*)intptr;

        //    ulong biggestRelativePtr = 0;
        //    ulong currentPtrOffset = 0;
        //    ulong depthStartPtrOffset = 0;

        //    toVisitNodesList.Clear();
        //    toVisitNodesList.AddFirst(((long)info.nodes - 1, 0));

        //    //all nodes of this depth
        //    uint currentDepthNodeCount = 0;
        //    //keeps track of the current node index in this depth
        //    uint currentDepthNodeCounter = 0;
        //    //keeps track of the number of children in this depth
        //    uint currentDepthChildrenCounter = 0;

        //    short currentDepth = -1;
        //    while (toVisitNodesList.Count > 0)
        //    {
        //        var currentNode = toVisitNodesList.First.Value;
        //        toVisitNodesList.RemoveFirst();
        //        var impNode = importedNodes[currentNode.impIndex];

        //        //Next depth reached assign
        //        if (currentDepth < currentNode.depth)
        //        {
        //            currentDepth = currentNode.depth;
        //            //Get nodes at this depth and check if we are in the last depth level
        //            currentDepthNodeCount = nodesPerDepth[currentDepth]; //nodesPerDepth.ContainsKey(currentDepth) ? nodesPerDepth[currentDepth] : 0;
        //            currentDepthChildrenCounter = 0;
        //            currentDepthNodeCounter = 0;

        //            depthStartPtrOffset = currentPtrOffset;
        //        }

        //        var node = new SvoNodeLaine();

        //        //(nodes in depth) + (already added children nodes) - (index of node in this depth)
        //        ulong offset = currentDepthNodeCount + currentDepthChildrenCounter - currentDepthNodeCounter;
        //        node.childrenRelativePointer = (uint)offset;
        //        if (offset > biggestRelativePtr) biggestRelativePtr = offset;

        //        byte childBitOffset = 1;
        //        for (uint i = 0; i < 8; i++)
        //        {
        //            if (impNode.HasChild(i))
        //            {
        //                node.validMask |= childBitOffset;
        //                if (currentDepth < maxDepth - 1)
        //                {
        //                    long childIndex = (long)impNode.GetChildPos(i);
        //                    toVisitNodesList.AddLast((childIndex, (short)(currentNode.depth + 1)));
        //                    currentDepthChildrenCounter++;
        //                }
        //                else
        //                {
        //                    node.leafMask |= childBitOffset;
        //                }
        //            }
        //            else
        //            {
        //                node.leafMask |= childBitOffset;
        //            }

        //            childBitOffset <<= 1;
        //        }

        //        currentDepthNodeCounter++;
        //        ptr[currentPtrOffset++] = node;
        //    }

        //    var unmanagedTree = new SvoUnmanaged<SvoNodeLaine>(ptr, (uint)requiredSizeBytes, sizeof(SvoNodeLaine), 1, 2, -1);
        //    Debug.Log("BiggestRelativePtr. " + biggestRelativePtr);

        //    return unmanagedTree;
        //}


        //public unsafe void ConvertFormatColorBreadth()
        //{
        //    if (reader.NodesCount > uint.MaxValue) throw new Exception("too many nodes!");

        //    maxDepth = reader.MaxDepth;

        //    uint requiredSizeBytes; nodesPerDepth = new uint[maxDepth]; var colors = new Dictionary<Color32, uint>();
        //    using (var sw = new SW("Collecting node count recursive per level took:"))
        //    {
        //        requiredSizeBytes = CalcSizeRecursive<SvoNodeColor>(reader.NodesCount - 1, 0);
        //    }
        //    Debug.Log("Required Size: " + requiredSizeBytes);

        //    var header = new SvoHeader
        //    {
        //        version = 3,
        //        maxDepth = maxDepth, //We subtract one level because the last level only contains color values and should not be interpreted as traceable voxel
        //        nodes = (uint)(requiredSizeBytes / sizeof(SvoNodeColor)),
        //        nodeType = NodeType.Color,
        //        order = HierachyOrder.Breadth,
        //        colors = 0,
        //        colorStrideSize = 4,
        //        depthNodesCount = nodesPerDepth
        //    };
        //    ReserveHeaderSpace(outWriter, header);


        //    ulong biggestRelativePtr = 0;
        //    uint[] nextDepthNodes = new uint[] { (uint)reader.NodesCount - 1 };
        //    for (short currentDepth = 0; currentDepth < maxDepth; currentDepth++)
        //    {
        //        uint[] currentDepthNodes = nextDepthNodes;
        //        if (currentDepth < maxDepth - 1) nextDepthNodes = new uint[nodesPerDepth[currentDepth + 1]];

        //        uint currentDepthChildrenCounter = 0;    //keeps track of the current node index in this depth
        //        uint currentDepthNodeCounter = 0;        //keeps track of the number of children in this depth
        //        Debug.Log($"Reformatting level {currentDepth} / {maxDepth - 1}\n Level size: {currentDepthNodes.Length}");

        //        for (int j = 0; j < currentDepthNodes.Length; j++)
        //        {
        //            var cNodeIdx = currentDepthNodes[j];
        //            var impNode = reader[cNodeIdx];
        //            var node = new SvoNodeColor();

        //            //(nodes in depth) + (already added children nodes) - (index of node in this depth)
        //            ulong offset = nodesPerDepth[currentDepth] + currentDepthChildrenCounter - currentDepthNodeCounter;
        //            node.childrenRelativeIndex = (uint)offset;
        //            if (offset > biggestRelativePtr) biggestRelativePtr = offset;


        //            if (impNode.IsLeaf())
        //            {
        //                var data = reader.GetData((long)impNode.data);
        //                var rgb = data.ConvertColor32(8);
        //                if (colors.ContainsKey(rgb))
        //                {
        //                    var colorIndex = colors[rgb];
        //                    node.ColorIndex = colorIndex;
        //                }
        //                else
        //                {
        //                    node.ColorIndex = (uint)colors.Count;
        //                    colors.Add(rgb, (uint)colors.Count);
        //                }
        //            }
        //            else
        //            {
        //                byte childBitOffset = 1;
        //                for (uint i = 0; i < 8; i++)
        //                {
        //                    if (impNode.HasChild(i))
        //                    {
        //                        node.ValidMask |= childBitOffset;

        //                        nextDepthNodes[currentDepthChildrenCounter] = (uint)impNode.GetChildPos(i);
        //                        currentDepthChildrenCounter++;
        //                    }

        //                    childBitOffset <<= 1;
        //                }
        //            }

        //            currentDepthNodeCounter++;
        //            outWriter.Write(*(long*)&node);
        //        }
        //    }

        //    var colorStrideSize = 3 + 1;      //3 bytes + 1 byte padding
        //    var colorsByteSize = colors.Count * colorStrideSize;
        //    if (colorsByteSize >= 1 << 24) Debug.LogWarning("More than 2^24 colors contained in octree. This will lead to overflows");

        //    foreach (var col in colors)
        //    {
        //        var cc = col.Key;
        //        var colInt = *(int*)&cc;

        //        outWriter.Write(colInt);
        //    }

        //    header.colors = colors.Count;
        //    WriteOctreeHeader(outWriter, header, true);
        //}


        private static unsafe uint CalcSize<T>(IVoxelConverterSVOReader reader, out uint[] nodesPerDepth, int maxDepth) where T : unmanaged, ISvoNode
        {
            //The size of DenseOctreeLeafNode
            var nodeSize = sizeof(T);
            uint requiredSizeBytes = 0;

            //Keeps track of nodes per depth level
            nodesPerDepth = new uint[maxDepth];

            var toVisitNodesList = new LinkedList<(long impIndex, short depth)>();
            toVisitNodesList.AddFirst((reader.NodesCount - 1, 0));

            var exit = false;
            while (toVisitNodesList.Count > 0 && !exit)
            {
                var currentNode = toVisitNodesList.First.Value;
                toVisitNodesList.RemoveFirst();
                var depth = currentNode.depth;

                //Leaf nodes need to be stored when color is used but dont iterate further bc we would go out of range
                if (depth < maxDepth - 1)
                {
                    var impNode = reader[currentNode.impIndex];
                    for (uint i = 0; i < 8; i++)
                    {
                        if (!impNode.HasChild(i)) continue;

                        long childIndex = (long)impNode.GetChildPos(i);
                        toVisitNodesList.AddFirst((childIndex, (short)(currentNode.depth + 1)));
                    }
                }

                nodesPerDepth[depth]++;
                requiredSizeBytes += (uint)nodeSize;
            }


            return requiredSizeBytes;
        }

        //public static unsafe uint CalcSizeRecursive<T>(SvoReader<T> reader, uint[] nodesPerDepth, int maxDepth, long impNodeIdx, int cDepth) where T : unmanaged, ISvoNode
        //{
        //    var impNode = reader[impNodeIdx];

        //    nodesPerDepth[cDepth]++;
        //    if (cDepth == maxDepth - 1) return (uint)sizeof(T);

        //    uint sizeReq = (uint)sizeof(T);
        //    for (int i = 0; i < 8; i++)
        //    {
        //        if (!impNode.HasChild(i)) continue;

        //        long childIndex = impNode.GetChildIndex(i, (uint)impNodeIdx);
        //        sizeReq += CalcSizeRecursive(reader, nodesPerDepth, maxDepth, childIndex, cDepth + 1);
        //    }

        //    return sizeReq;
        //}

        private unsafe uint CalcSizeRecursive<T>(long impNodeIdx, int cDepth) where T : unmanaged, ISvoNode
        {
            var impNode = reader[impNodeIdx];

            nodesPerDepth[cDepth]++;
            if (cDepth == maxDepth - 1) return (uint)sizeof(T);

            uint sizeReq = (uint)sizeof(T);
            for (uint i = 0; i < 8; i++)
            {
                if (!impNode.HasChild(i)) continue;

                long childIndex = (long)impNode.GetChildPos(i);
                sizeReq += CalcSizeRecursive<T>(childIndex, cDepth + 1);
            }

            return sizeReq;
        }
    }
}