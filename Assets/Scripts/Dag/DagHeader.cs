using Assets.Scripts.Dag.Builders;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Dag
{
    public enum DagFormat { Gray, ColorPerPointer, ColorPerNode }

    public class DagHeader
    {
        public int version;
        public DagFormat format;
        public int maxDepth;
        public int brickLvlChildrenCount;
        
        public int NodeCount { get; private set; }
        public int PointerCount { get; private set; }
        public int GeometryByteSize { get; private set; }
        public uint[] NodesPerLevel{ get; set; }

        public int ColorCount { get; private set; }
        public int ColorByteSize { get; private set; }

        public void SetGeometryData(int nodeCount, int pointerCount, int byteSize, uint[] nodesPerLevel)
        {
            this.NodeCount = nodeCount;
            this.PointerCount = pointerCount;
            this.GeometryByteSize = byteSize;
            this.NodesPerLevel = nodesPerLevel;
        }

        public void SetColorData(int colorCount, int byteSíze)
        {
            this.ColorCount = colorCount;
            this.ColorByteSize = byteSíze;
        }
    }
}