//#define MY_DEBUG

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Octree
{
    public static class DenseOctree
    {
        public static float3[] childOffsets = { new float3(-1, -1, -1), new float3(-1, -1, 1), new float3(-1, 1, -1), new float3(-1, 1, 1), new float3(1, -1, -1), new float3(1, -1, 1), new float3(1, 1, -1), new float3(1, 1, 1) };
        public static uint3[] childIndexOffsets = { new uint3(0, 0, 0), new uint3(0, 0, 1), new uint3(0, 1, 0), new uint3(0, 1, 1), new uint3(1, 0, 0), new uint3(1, 0, 1), new uint3(1, 1, 0), new uint3(1, 1, 1) };

        public static void SparseManaged(DenseOctreeNode octree)
        {
            //Get penultimate depth nodes
            //Check every children of them
            //  If all children contain no voxels
            //    => delete children
            //    => set this node to a leaf node which contains NO voxel
            //  If all children contain voxel
            //    => delete children
            //    => set this node to a leaf node which contains A voxel
            //Repeat this till we reach depth 0

            var nodesByDepth = GetDepthNodes(octree);
            var nextDepth = nodesByDepth[(short)(nodesByDepth.Count - 1)]; 
            var currentDepth = new HashSet<DenseOctreeNode>();

            do
            {
                var temp = currentDepth;
                currentDepth = nextDepth;
                nextDepth = temp;
                
                currentDepthLoop: while (currentDepth.Count > 0)
                {
                    var node = currentDepth.First();
                    currentDepth.Remove(node);

                    var anyChildrenFilled = false;
                    var allChildrenFilled = true;
                    for (int j = 0; j < node.Children.Length; j++)
                    {
                        if (!node.Children[j].IsLeaf) goto currentDepthLoop;

                        allChildrenFilled &= node.Children[j].ContainsVoxel;
                        anyChildrenFilled |= node.Children[j].ContainsVoxel;
                    }

                    //All children are empty
                    if (!anyChildrenFilled)
                    {
                        node.Children = null;
                        node.ContainsVoxel = false;

                        nextDepth.Add(node.Parent);
                    }

                    if (allChildrenFilled)
                    {
                        node.Children = null;
                        node.ContainsVoxel = true;

                        nextDepth.Add(node.Parent);
                    }

                } 

                
            } while (nextDepth.Count > 0);
        }

        //public static unsafe SvoUnmanaged<SvoNodeLaine> ConvertToUnmanaged(DenseOctreeNode octree)
        //{
        //    //The size of DenseOctreeLeafNode
        //    ulong nodeSize = (ulong) sizeof(SvoNodeLaine);
        //    ulong requiredSizeBytes = nodeSize;

        //    //Keeps track of nodes per depth level
        //    var nodesPerDepth = new Dictionary<short, uint>();

        //    var toVisitNodes = new Queue<(DenseOctreeNode octree, short depth)>();
        //    toVisitNodes.Enqueue((octree, 0));

        //    short currentDepth = 0;
        //    uint currentDepthNodeCount = 0;

        //    while (toVisitNodes.Count > 0)
        //    {
        //        (DenseOctreeNode octree, short depth) currentNode = toVisitNodes.Dequeue();
        //        var children = currentNode.octree.Children;

        //        //Next depth reached => store & reset currentDepthNode counter
        //        if (currentDepth < currentNode.depth)
        //        {
        //            nodesPerDepth.Add(currentDepth, currentDepthNodeCount);
        //            currentDepthNodeCount = 0;
        //            currentDepth = currentNode.depth;
        //        }

        //        for (int i = 0; i < children.Length; i++)
        //        {
        //            //if the child is a leaf dont add any more bytes because the data is stored in its parent
        //            if (children[i].IsLeaf) continue;

        //            requiredSizeBytes += nodeSize;
        //            toVisitNodes.Enqueue((children[i], (short)(currentNode.depth + 1)));
        //        }

        //        currentDepthNodeCount++;
        //    }

        //    nodesPerDepth.Add(currentDepth, currentDepthNodeCount);
        //    currentDepthNodeCount = 0;

        //    Debug.Log("RequiredMemory: " + requiredSizeBytes + " B");

        //    IntPtr intptr = Marshal.AllocHGlobal(new IntPtr((long) requiredSizeBytes));
        //    UnsafeMemory.FillMemory(intptr, (uint) requiredSizeBytes, 0);
        //    SvoNodeLaine* ptr = (SvoNodeLaine*) intptr;

        //    ulong biggestRelativePtr = 0;
        //    ulong currentPtrOffset = 0;
        //    ulong depthStartPtrOffset = 0;

        //    toVisitNodes.Clear();
        //    toVisitNodes.Enqueue((octree, 0));

        //    //all nodes of this depth
        //    currentDepthNodeCount = 0;
        //    //keeps track of the current node index in this depth
        //    uint currentDepthNodeCounter = 0;
        //    //keeps track of the number of children in this depth
        //    uint currentDepthChildrenCounter = 0;

        //    currentDepth = -1;
        //    while (toVisitNodes.Count > 0)
        //    {
        //        var currentNode = toVisitNodes.Dequeue();
        //        var children = currentNode.octree.Children;

        //        //Next depth reached assign
        //        if (currentDepth < currentNode.depth)
        //        {
        //            currentDepth = currentNode.depth;
        //            //Get nodes at this depth and check if we are in the last depth level
        //            currentDepthNodeCount = nodesPerDepth.ContainsKey(currentDepth) ? nodesPerDepth[currentDepth] : 0;
        //            currentDepthChildrenCounter = 0;
        //            currentDepthNodeCounter = 0;

        //            depthStartPtrOffset = currentPtrOffset;
        //        }

        //        var node = new SvoNodeLaine();
        //        byte childBitOffset = 1;

        //        //(nodes in depth) + (already added children nodes) - (index of node in this depth)
        //        ulong offset = (currentDepthNodeCount + currentDepthChildrenCounter - currentDepthNodeCounter);
        //        node.childrenRelativePointer = (uint) offset;
        //        if (offset > biggestRelativePtr) biggestRelativePtr = offset;

        //        for (int i = 0; i < children.Length; i++)
        //        {
        //            if (children[i] != null)
        //            {
        //                if (children[i].IsLeaf)
        //                {
        //                    node.leafMask |= childBitOffset;
        //                    if (children[i].ContainsVoxel) node.validMask |= childBitOffset;
        //                }
        //                else
        //                {
        //                    node.validMask |= childBitOffset;

        //                    toVisitNodes.Enqueue((children[i], (short)(currentNode.depth + 1)));
        //                    currentDepthChildrenCounter++;
        //                }
        //            }
                        
        //            childBitOffset <<= 1;
        //        }

        //        currentDepthNodeCounter++;
        //        ptr[currentPtrOffset++] = node;
        //    }

        //    if (requiredSizeBytes > Int32.MaxValue) throw new Exception("More bytes required than Int32 size");
        //    var unmanagedTree = new SvoUnmanaged<SvoNodeLaine>(ptr, (uint)requiredSizeBytes, sizeof(SvoNodeLaine), octree.MinBounds, octree.MaxBounds, -1);

        //    Debug.Log("BiggestRelativePtr. " + biggestRelativePtr);

        //    return unmanagedTree;
        //}

        private static Dictionary<short, HashSet<DenseOctreeNode>> GetDepthNodes(DenseOctreeNode octree)
        {
            var depthNodes = new Dictionary<short, HashSet<DenseOctreeNode>>();

            var toVisitNodes = new Queue<(DenseOctreeNode octree, short depth)>();
            toVisitNodes.Enqueue((octree, 0));
            while (toVisitNodes.Count > 0)
            {
                var currentNode = toVisitNodes.Dequeue();
                var children = currentNode.octree.Children;
                short currentDepth = currentNode.depth;

                for (int i = 0; i < children.Length; i++)
                {
                    if (!children[i].IsLeaf)
                        toVisitNodes.Enqueue((children[i], (short)(currentDepth + 1)));
                }

                if (!depthNodes.ContainsKey(currentDepth))
                    depthNodes.Add(currentDepth, new HashSet<DenseOctreeNode>());

                depthNodes[currentDepth].Add(currentNode.octree);
            }

            return depthNodes;
        }

        private static uint FindTreeDepth(uint voxelCount)
        {
            return (uint)math.log2(math.pow(voxelCount, 1 / 3f));
        }

        private static uint3 FindNextPower2(uint3 x)
        {
            return (uint3) math.pow(2, math.ceil(math.log2(x)));
        }

        private static uint FindNextPower2(uint x)
        {
            return (uint) math.pow(2, math.ceil(math.log2(x)));
        }
    }


    public class DenseOctreeNode
    {
        public const byte CHILD_NODES = 8;

        public float3 center;
        public float halfExtents;

        public float3 MinBounds => center - halfExtents;
        public float3 MaxBounds => center + halfExtents;
        public DenseOctreeNode[] Children { get; set; }
        public DenseOctreeNode Parent { get; set; }
        public bool ContainsVoxel { get; set; }
        public bool IsLeaf => Children == null;


        public DenseOctreeNode(float halfExtents, bool leafNode = false)
        {
            this.halfExtents = halfExtents;
            this.Children = leafNode ? null : new DenseOctreeNode[CHILD_NODES];
        }

        public DenseOctreeNode(float3 center, float halfExtents, bool leafNode = false)
        {
            this.center = center;
            this.halfExtents = halfExtents;
            this.Children = leafNode ? null : new DenseOctreeNode[CHILD_NODES];
        }

      
        private bool DoBoxesIntersect(float3 aCenter, float aHalfExtents, float3 bCenter, float bHalfExtents)
        {
            var sumSize = aHalfExtents + bHalfExtents;

            return (math.abs(aCenter.x - bCenter.x) < sumSize) &&
                   (math.abs(aCenter.y - bCenter.y) < sumSize) &&
                   (math.abs(aCenter.z - bCenter.z) < sumSize);
        }

        public override string ToString()
        {
            if (IsLeaf)
                return $"Leaf: {ContainsVoxel}";
            return $"Node";
        }
    }
}