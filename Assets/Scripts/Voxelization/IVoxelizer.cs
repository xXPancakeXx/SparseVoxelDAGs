using Assets.Scripts.Voxelization.Entities;
using System.Collections.Generic;
using Unity.Jobs;
using Unity.Mathematics;
using Utils;

namespace Assets.Scripts.Voxelization
{
    public interface IVoxelizer
    {
        int GridDimension { get; }
        int MaxDepth { get; }
        uint[] NodesPerDepth { get; }
        int NodeCount { get; }
        int VoxelCount { get; }

        IVoxelizerOctNode GetNode(int level, int nodeIdx);
        JobHandle Voxelize();
    }
}