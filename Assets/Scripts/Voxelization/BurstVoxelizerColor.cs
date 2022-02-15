using Assets.Scripts.Entities;
using Assets.Scripts.Voxelization.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Utils;
using Debug = UnityEngine.Debug;
using Mesh = UnityEngine.Mesh;

namespace Assets.Scripts.Voxelization
{
    public partial class BurstVoxelizerColor : IVoxelizer
    {
        public NativeLocalMesh mesh;
        private int maxDepth;

        private VoxelTreeLevels<ColorOctNode> levelContainer;

        #region Properties
        public int GridDimension => 1 << maxDepth;
        public int MaxDepth => maxDepth;
        public int NodeCount
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < Levels.Length; i++)
                {
                    sum += (int) Levels[i].Length;
                }

                return sum;
            }
        }
        public int VoxelCount
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < Levels[maxDepth - 1].Length; i++)
                {
                    sum += math.countbits((int)Levels[maxDepth - 1][i].validmask);

                }
                return sum;
            }

        }
        public uint[] NodesPerDepth
        {
            get
            {
                var x = new uint[Levels.Length];
                for (int i = 0; i < Levels.Length; i++)
                {
                    x[i] = (uint)Levels[i].Length;
                }
                return x;
            }
        }

        public UnsafeList<UnsafeLongList<ColorOctNode>> Levels => levelContainer.levels;
        public UnsafeLongList<ColorOctNode> LeafNodes => levelContainer.levels[maxDepth - 1];
        #endregion

        public BurstVoxelizerColor(Mesh mesh, int maxDepth, Material[] materials = null)
        {
            this.mesh = materials != null ? new NativeLocalMesh(mesh, materials) : new NativeLocalMesh(mesh);
            this.maxDepth = maxDepth;
        }

        ~BurstVoxelizerColor()
        {
            this.mesh.Dispose();
            this.levelContainer.Dispose();
        }

        public JobHandle Voxelize()
        {
            levelContainer = new VoxelTreeLevels<ColorOctNode>(MaxDepth);
            var job = new VoxelizeColorJob(mesh, maxDepth, levelContainer);

            Debug.Log($"Scheduling: Triangles: {mesh.triangles.Length}");

            var handle = job.Schedule();
            return handle;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe IVoxelizerOctNode GetNode(int level, int nodeIdx)
        {
            if (level < maxDepth - 1)
            {
                return Levels[level][nodeIdx];
            }

            var node = Levels[level][nodeIdx];
            var leafNode = new LeafOctNode();

            UnsafeUtility.MemCpy(leafNode.colors, node.colors, 4*8);
            leafNode.validmask = node.validmask;

            return leafNode;
        }
    }
}