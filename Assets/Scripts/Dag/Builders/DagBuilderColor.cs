using Assets.Scripts.Dag.Builders;
using Assets.Scripts.Octree;
using Assets.Scripts.Octree.Readers;
using Assets.Scripts.Voxelization.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using static Assets.Scripts.Octree.SvoInfo;

namespace Assets.Scripts.Dag
{
    public abstract unsafe class DagBuilderColor<T> : DagBuilder<T, SvoNodeColor>, IDagBuilder where T : IDagNode
    {
        public Color32[] colors;

        //public virtual void ConstructDag(SvoColorUnmanaged<SvoNodeColor> oct)
        //{
        //    if (oct == null) throw new Exception("Octree does not contain color or is empty!");
        //    if (oct.Count < 0) throw new Exception("Octree to big / Octree size negative");

        //    Init(oct);
        //}

        public override void Init(int maxDepth, uint nodeCount)
        {
            base.Init(maxDepth, nodeCount);
            
            colors = new Color32[nodeCount];
        }

        //public void Init(SvoColorUnmanaged<SvoNodeColor> oct)
        //{
        //    svoDepth = oct.maxDepth;
        //    nodes = new T[oct.Count];
        //    nodeIdLevelIndices = new uint[oct.Count];
        //    nodeIdLevels = new List<List<uint>>();
        //    svoNodesCount = (uint)oct.Count;
        //    colors = new Color32[svoNodesCount];
        //}


        //public void LoadSVODepthFirst(SvoNodeColorFullReaderRandom reader, Func<uint, uint, SvoNodeColor, T> onInsert)
        //{
        //    Debug.Log("Loading SVO in depth first order...");

        //    var nodesToVisit = new LinkedList<(uint impNodeIdx, uint parentIdx)>();
        //    nodesToVisit.AddFirst((0, 0));

        //    //Read depth first
        //    uint depthFirstIdx = 0;
        //    while (nodesToVisit.Count > 0)
        //    {
        //        (uint svoNodeIdx, uint parentIdx) = nodesToVisit.First.Value;
        //        var svoNode = reader.GetNode(svoNodeIdx);
        //        nodesToVisit.RemoveFirst();

        //        for (int i = 7; i >= 0; i--)
        //        {
        //            if (svoNode.HasChild(i))
        //            {
        //                uint childSvoIdx = svoNode.GetChildIndex(i, svoNodeIdx);

        //                nodesToVisit.AddFirst((childSvoIdx, depthFirstIdx));
        //            }
        //        }


        //        var dagNode = onInsert(parentIdx, depthFirstIdx, svoNode);

        //        nodes[depthFirstIdx] = dagNode;
        //        colors[depthFirstIdx] = reader.GetColor(svoNode.ColorIndex);
        //        depthFirstIdx++;
        //    }


        //    Debug.Log("SVO is in memory!");
        //}

        protected void LoadSVODepthFirst(SvoNodeColorFullReaderRandom reader, Func<uint, uint, SvoNodeColor, T> onInsert)
        {
            Debug.Log("Loading SVO in depth first order...");

            var nodesToVisit = new List<(uint impNodeIdx, uint parentIdx, byte depth)>();
            nodesToVisit.Add((0, 0, 0));

            //Read depth first
            uint depthFirstIdx = 0;
            while (nodesToVisit.Count > 0)
            {
                (uint svoNodeIdx, uint parentIdx, byte depth) = nodesToVisit.First();
                var svoNode = reader.GetNode((int)svoNodeIdx);
                nodesToVisit.RemoveAt(0);
                
                for (int i = 7; i >= 0; i--)
                {
                    if (svoNode.HasChild(i))
                    {
                        var childSvoIdx = svoNode.GetChildIndex(i, svoNodeIdx);

                        nodesToVisit.Insert(0, (childSvoIdx, depthFirstIdx, (byte) (depth + 1)));
                    }
                }

                var dagNode = onInsert(parentIdx, depthFirstIdx, svoNode);
                nodes[depthFirstIdx] = dagNode;

                //assign node to depth
                if (depth < MaxDepth)
                    nodeIdLevels[depth].Add(depthFirstIdx);

                var col = reader.GetColor(svoNode.ColorIndex);
                colors[depthFirstIdx] = *(Color32*)&col;
                depthFirstIdx++;
            }


            Debug.Log("SVO is in memory!");
        }

        #region IO
        public override void WriteToFile(BinaryWriter bw)
        {
            WriteDataToFile(bw);
            WriteColorsToFile(bw);
        }

        public abstract void WriteDataToFile(BinaryWriter bw);

        public void WriteColorsToFile(BinaryWriter bw)
        {
            //Write colors into file
            byte[] colorByteArr = new byte[colors.Length * sizeof(Color32)];
            fixed (byte* colResPtr = colorByteArr)
            fixed (Color32* colPtr = colors)
            {
                UnsafeMemory.CopyMemory(new IntPtr(colResPtr), new IntPtr(colPtr), (uint)colorByteArr.Length);
            }
            bw.Write(colorByteArr);
        }

        #endregion
    }
}