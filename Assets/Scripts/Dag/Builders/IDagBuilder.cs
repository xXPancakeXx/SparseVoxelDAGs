using Assets.Scripts.Octree;
using Assets.Scripts.Octree.Builders;
using System;
using System.IO;
using static Assets.Scripts.Octree.SvoInfo;

namespace Assets.Scripts.Dag.Builders
{
    public interface IDagBuilder
    {
        public int MaxDepth {  get; }

        public void ConstructDagMemory(SvoInfo octInfo);


        public void WriteHeader(BinaryWriter bw);
        public void ReadHeader(BinaryReader br, ref DagHeader header);


        public void WriteToFile(BinaryWriter bw);
        public RenderData ReadFromFile(BinaryReader br);


        public uint CountDagNodes();
        public uint CountDagNodeChildren();
        public int GetChildrenCountLevel(int level);
        public void PrintLevels();
    }
}