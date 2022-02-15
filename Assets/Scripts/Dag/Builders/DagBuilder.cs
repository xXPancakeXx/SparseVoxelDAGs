using Assets.Scripts.Dag.Builders;
using Assets.Scripts.Octree;
using Assets.Scripts.Octree.Builders;
using Assets.Scripts.Octree.Readers;
using Assets.Scripts.Voxelization.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using static Assets.Scripts.Octree.SvoInfo;

namespace Assets.Scripts.Dag
{
    public abstract unsafe class DagBuilder<DagNodeType, SvoNodeType> : IDagBuilder where DagNodeType : IDagNode where SvoNodeType : unmanaged, ISvoNode
    {
        protected uint svoNodesCount;
        public int svoDepth;

        public DagNodeType[] nodes;
        public List<List<uint>> nodeIdLevels;

        //Helpers for optimal performance
        public uint[] nodeIdLevelIndices;                    //Stores the index of the node in its level (same length as nodes)
        public uint[][] childrenCountLevelBeforeNode;        //Accumulated children count of this level before an index

        public int MaxDepth => nodeIdLevels.Count;

        public abstract void ConstructDagMemory(SvoInfo octInfo);
        


        public virtual void Init(int maxDepth, uint nodeCount)
        {
            svoDepth = maxDepth;
            nodes = new DagNodeType[nodeCount];
            nodeIdLevelIndices = new uint[nodeCount];
            nodeIdLevels = new List<List<uint>>();
            svoNodesCount = nodeCount;

            for (int i = 0; i < maxDepth; i++)
            {
                nodeIdLevels.Add(new List<uint>());
            }
        }

        public abstract DagNodeType CreateNode(uint index, int depth, IVoxelizerOctNode node);

        public virtual void ConstructSvo(SvoReaderSequential reader)
        {
            Debug.Log("Loading SVO into memory");

            uint i = 0;
            foreach (IVoxelizerOctNode node in reader)
            {
                var cDepth = reader.CurrentDepth;

                nodes[i] = CreateNode(i, cDepth, node);
                nodeIdLevels[cDepth].Add(i);

                i++;
            }

            Debug.Log("SVO in memory!");
        }

        protected void PopulateParents()
        {
            for (int level = 0; level < MaxDepth - 1; level++)
            {
                for (int i = 0; i < nodeIdLevels[level].Count; i++)
                {
                    var nodeIdx = nodeIdLevels[level][i];
                    var node = nodes[nodeIdx];
                    foreach (var childIdx in node.ChildrenIndexEnumerator())
                    {
                        nodes[childIdx].SvoParent = nodeIdx;
                    }
                }
            }
        }

        protected void ConstructLevels(uint rootIndex)
        {
            Debug.Log("Constructing depth levels ...");

            // Construct root level for DAG reduction
            nodeIdLevels.Add(new List<uint>() { rootIndex });
            nodeIdLevelIndices[rootIndex] = 0;        //this stores the index of the root node in its level => Used for faster access during save to file

            int l = nodeIdLevels.Count;
            List<uint> nextLevel = ConstructNextDAGLevel(nodeIdLevels[l - 1]);
            while (nextLevel.Count != 0)
            {
                nodeIdLevels.Add(nextLevel);
                l++;
                nextLevel = ConstructNextDAGLevel(nodeIdLevels[l - 1]);
            }

            Debug.Log("Depth levels constructed");
        }

        private List<uint> ConstructNextDAGLevel(List<uint> level)
        {
            List<uint> nextLevel = new List<uint>(); // is empty when we are at leaf level

            foreach (uint nId in level)
            {
                foreach (uint child in nodes[nId].ChildrenIndexEnumerator())
                {
                    nextLevel.Add(child);
                }
            }

            return nextLevel;
        }


        #region DAG Conversion
        protected void Reduce()
        {
            for (int i = nodeIdLevels.Count - 1; i > 0; i--)
            {
                Debug.Log($"Reducing level {i} ...");
                PrintStatistics();

                int nodesCount = nodeIdLevels[i].Count;
                SortLevel(i);
                GroupLevel(i);

                int newNodesCount = nodeIdLevels[i].Count;
            }
        }


        protected void SortLevel(int level)
        {
            List<(uint nodeId, byte validMask)> currentLevelNodes = new List<(uint, byte)>(nodeIdLevels[level].Count);
            foreach (var nodeInLevel in nodeIdLevels[level])
            {
                currentLevelNodes.Add((nodeInLevel, nodes[nodeInLevel].ValidMask));
            }

            currentLevelNodes.Sort((x, y) => x.validMask.CompareTo(y.validMask));
            // Writing the sorted vector
            for (int i = 0; i < currentLevelNodes.Count; i++)
            {
                nodeIdLevels[level][i] = currentLevelNodes[i].nodeId;
            }
        }
        protected void GroupLevel(int lvl)
        {
            // Id of the node in this new list for comparison with old list
            uint groupNodeId = nodeIdLevels[lvl][0];

            // Initialize new level
            var newLevel = new List<uint>() { groupNodeId };

            // Assert that the level is sorted
            for (int i = 1; i < nodeIdLevels[lvl].Count; i++)
            {
                // Old list
                uint nId = nodeIdLevels[lvl][i];
                if (nodes[nId].Equals(nodes[groupNodeId]))
                {
                    // Regrouping the nodes
                    ref var parentToUpdate = ref nodes[nodes[nId].SvoParent];

                    // Updating the children field of the parent node
                    for (int j = 0; j < parentToUpdate.ChildrenCount; j++)
                    {
                        if (parentToUpdate.GetChildIndex(j) == nId)
                            parentToUpdate.SetChildIndex(j, groupNodeId); // = (groupNodeId, parentToUpdate.children[j].attributeOffset);
                    }
                }
                else
                {
                    // New group of nodes
                    newLevel.Add(nId);
                    groupNodeId = nId;
                }
            }

            // Change the old level by the new level
            nodeIdLevels[lvl] = newLevel;
        }
        #endregion

        protected void AccumulateChildrenCountLevel()
        {
            childrenCountLevelBeforeNode = new uint[nodeIdLevels.Count][];
            for (int i = 0; i < nodeIdLevels.Count; i++)
            {
                List<uint> lvl = nodeIdLevels[i];
                childrenCountLevelBeforeNode[i] = new uint[lvl.Count];

                uint childrenCount = 0;
                for (int j = 0; j < lvl.Count; j++)
                {
                    uint nodeId = lvl[j];
                    ref var node = ref nodes[nodeId];

                    childrenCountLevelBeforeNode[i][j] = childrenCount;
                    childrenCount += (uint)node.ChildrenCount;
                }
            }
        }
        protected void GenerateNodeIdLevelList()
        {
            for (int i = 0; i < nodeIdLevels.Count; i++)
            {
                List<uint> lvl = nodeIdLevels[i];

                for (int j = 0; j < lvl.Count; j++)
                {
                    uint nodeId = lvl[j];
                    nodeIdLevelIndices[nodeId] = (uint)j;
                }
            }
        }

        #region IO

        public abstract void WriteHeader(BinaryWriter bw);
        public abstract void ReadHeader(BinaryReader br, ref DagHeader header);

        public abstract void WriteToFile(BinaryWriter bw);
        public abstract RenderData ReadFromFile(BinaryReader br);

        #endregion

        #region Utility

        public int GetChildrenCountLevel(int lvl)
        {
            return nodeIdLevels[lvl].Sum(x => nodes[x].ChildrenCount);
        }

        public uint CountDagNodeChildren()
        {
            return (uint)nodeIdLevels.SelectMany(x => x).Sum(x => nodes[x].ChildrenCount);
        }

        public uint CountBrickLevelChildren()
        {
            return (uint)nodeIdLevels.Last().Sum(x => math.countbits((int)nodes[x].ValidMask));
        }

        public uint CountDagNodes()
        {
            uint n = 0;
            for (int i = 0; i < nodeIdLevels.Count; i++)
            {
                n += (uint)nodeIdLevels[i].Count;
            }
            return n;
        }

        public int CalcDagSize()
        {
            return (int)(CountDagNodes() + CountDagNodeChildren());
        }


        public uint GetChildrenCountLevelBeforeNode(int lvl, uint nodeId)
        {
            var nodeIndexInLvl = GetNodeIndexLevel(nodeId);
            return childrenCountLevelBeforeNode[lvl][nodeIndexInLvl];
        }

        public uint GetNodeIndexLevel(uint nodeId)
        {
            var x = nodeIdLevelIndices[nodeId];
            return x;
        }

        public void PrintLevels()
        {
            Debug.Log("Checking DAGLevels...");
            Debug.Log($"There are {FormatLargeNum(CountDagNodes())} DAG nodes, against {FormatLargeNum(svoNodesCount)} in SVO.");
        }

        public void PrintLevelsP()
        {
            for (int i = 0; i < nodeIdLevels.Count; i++)
            {
                Debug.Log($"Level {i+1}: {nodeIdLevels[i].Count}");
            }
        }

        //Collect average parents length per node
        //Collect average children length per node
        public void PrintStatistics()
        {
            Debug.Log("Checking Statistics...");

            uint parentsCount = 0;
            uint childrenCount = 0;
            uint childrenCap = 0;

            int n = 0;
            for (int i = 0; i < nodeIdLevels.Count; i++)
            {
                n += nodeIdLevels[i].Count;

                for (int j = 0; j < nodeIdLevels[i].Count; j++)
                {
                    ref var node = ref nodes[nodeIdLevels[i][j]];

                    parentsCount += (uint)1;
                    childrenCount += (uint)node.ChildrenCount;

                    childrenCap += (uint)node.ChildrenCapacity;
                }
            }
            var str = $"Node count    : {FormatLargeNum(parentsCount)}\n"
                    + $"Node stats    : Parents: {FormatLargeNum(parentsCount)}, Children: {FormatLargeNum(childrenCount)}, ChildrenCapacity: {FormatLargeNum(childrenCap)}\n"
                    + $"Node avg stats: Parents: {parentsCount / (float)n:0.##}, Children: {childrenCount / (float)n:0.##}, ChildrenCapacity: {childrenCap / (float)n:0.##}\n";

            Debug.Log(str);
        }


        private string FormatLargeNum(uint n)
        {
            return $"{n:#,##0.}";
        }

        #endregion
    }
}