using Assets.Scripts.Voxelization;
using Assets.Scripts.Voxelization.Entities;
using System.Collections.Generic;
using System.IO;

namespace Assets.Scripts.Octree.Builders
{
    public interface ISvoBuilder
    {
        public int MaxDepth { get; }
        public uint NodeCount { get; }
        public long HeaderByteSize { get; }
        public long NodeByteSize { get; }
        public long ColorByteSize { get; }

        public void WriteHeader(BinaryWriter bw, IVoxelizer voxelizer);
        public void ReadHeader(BinaryReader br);

        public void WriteToFile(BinaryWriter bw, IVoxelizer voxelizer);
        public SvoRenderData ReadFromFile(BinaryReader br);

        public bool ReadBreadth(BinaryReader br, out IVoxelizerOctNode node, out int depth);

#if UNITY_EDITOR
        public void InitEditorGUI(BinaryReader br, bool enableDeepAnalysis);
        public void OnEditorGUI();
#endif

    }
}