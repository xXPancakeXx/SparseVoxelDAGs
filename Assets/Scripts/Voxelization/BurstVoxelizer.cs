using Assets.Scripts.Entities;
using Assets.Scripts.Voxelization.Entities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Mesh = UnityEngine.Mesh;

namespace Assets.Scripts.Voxelization
{
    public partial class BurstVoxelizer : IVoxelizer
    {
        public readonly NativeLocalMesh mesh;
        private int maxDepth;
        private bool hasMaterialColorData;

        private VoxelTreeLevels<OctNode> levelContainer;
        public UnsafeList<Utils.UnsafeLongList<OctNode>> levels;

        #region Properties
        public int GridDimension => 1 << maxDepth;
        public int MaxDepth => maxDepth;
        public int NodeCount
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < levels.Length; i++)
                {
                    sum += (int) levels[i].Length;
                }
                //if (leafLevel != null) sum += leafLevel.Count;

                return sum;
            }
        }
        public int VoxelCount
        {
            get
            {
                //if (leafLevel != null) return leafLevel.Sum(x => math.countbits((int)x.validmask));

                int sum = 0;
                for (int i = 0; i < levels[maxDepth - 1].Length; i++)
                {
                    sum += math.countbits((int)levels[maxDepth - 1][i].validmask);

                }
                return sum;
            }

        }
        public uint[] NodesPerDepth
        {
            get
            {
                var x = new uint[levels.Length/*leafLevel == null ? levels.Length : levels.Length+1*/];
                for (int i = 0; i < levels.Length; i++)
                {
                    x[i] = (uint)levels[i].Length;
                }
                //if (leafLevel != null) x[levels.Length] = (uint)leafLevel.Count;

                return x;
            }
        }
        #endregion

        public BurstVoxelizer(Mesh mesh, int maxDepth)
        {
            this.mesh = new NativeLocalMesh(mesh);
            this.maxDepth = maxDepth;
        }

        ~BurstVoxelizer()
        {
            Debug.Log("Finalizing");
            this.mesh.Dispose();
            this.levelContainer.Dispose();
        }

        public JobHandle Voxelize()
        {
            levelContainer = new VoxelTreeLevels<OctNode>(MaxDepth);
            levels = levelContainer.levels;

            var job = new VoxelizeJob(mesh, maxDepth, levelContainer);

            Debug.Log($"Scheduling: Triangles: {mesh.triangles.Length}");

            var handle = job.Schedule();
            return handle;
        }

        public async Task VoxelizeSub(int subtreeDepth)
        {
            var sw1 = Stopwatch.StartNew();

            var levelContainer = new VoxelTreeLevels<OctNode>(MaxDepth);
            var leafLevelData = new NativeList<LeafLevelData>((int) Math.Pow(4, subtreeDepth), Allocator.Persistent);

            VoxelizeMeshJob voxelizeJob;
            unsafe
            {
                voxelizeJob = new VoxelizeMeshJob(mesh, new UnsafeList<int>((int*)mesh.triangles.GetUnsafePtr(), mesh.triangles.Length), mesh.bounds, subtreeDepth, levelContainer, leafLevelData);
            }

            Debug.Log($"Voxelizing levels 0-{subtreeDepth}: Triangles: {mesh.triangles.Length}");
            var x = voxelizeJob.Schedule();
            while (!x.IsCompleted)
            {
                await Task.Yield();
            }
            x.Complete();

            double3 size = (float3)mesh.bounds.max - (float3)mesh.bounds.min;
            double rootSize = math.max(math.max(size.x, size.y), size.z);
            var voxelSize = rootSize / (2 << (subtreeDepth + 1));
            for (int i = 0; i < leafLevelData.Length; i++)
            {
                var bounds = new Bounds((float3) leafLevelData[i].center, (float3) voxelSize);
                voxelizeJob = new VoxelizeMeshJob(mesh, leafLevelData[i].triIds, bounds, MaxDepth - subtreeDepth, levelContainer, default);

                Debug.Log($"Voxelizing subtree {subtreeDepth}-{MaxDepth}: Triangles: { leafLevelData[i].triIds.Length}");
                // await voxelizeJob.Schedule();
            }


            Debug.Log("Joining subtrees... ");
            //for (int i = 0; i < _data[stepLevels - 1].size(); ++i)
            //{
            //    for (int j = 7; j >= 0; --j)
            //    {
            //        if (_data[stepLevels - 1][i].existsChild(j))
            //        {
            //            GeomOctree* oct = leavesOctrees[i * 8 + j];
            //            _data[stepLevels - 1][i].children[j] = (id_t)_data[stepLevels].size();
            //            for (unsigned int k = stepLevels; k < levels; ++k)
            //            {
            //                if (k < (levels - 1))
            //                { // update pointers
            //                    id_t offset = (id_t)_data[k + 1].size();
            //                    for (unsigned int m = 0; m < oct->_data[k - stepLevels].size(); ++m)
            //                    {
            //                        for (unsigned int n = 0; n < 8; ++n)
            //                        {
            //                            if (oct->_data[k - stepLevels][m].existsChildPointer(n))
            //                                oct->_data[k - stepLevels][m].children[n] += offset;
            //                        }
            //                    }
            //                }
            //                _data[k].insert(_data[k].end(), oct->_data[k - stepLevels].begin(), oct->_data[k - stepLevels].end());
            //            }
            //            delete oct;
            //        }
            //    }
            //}

            Debug.Log($"Time: {sw1.ElapsedMilliseconds} ms");
        }

        public async Task VoxelizeParallel2()
        {
            //NativeArray<Partition> partitions;
            //MeshPartitioner.PartitionMemory(mesh, new int3(2, 2, 2), out partitions);

            var sw1 = Stopwatch.StartNew();

            var partitionJob = new MeshPartitioner.MeshPartitionJob(mesh, new int3(2, 2, 2));
            var partitions = partitionJob.CreatePartitions();

            var x = partitionJob.Schedule();
            while (!x.IsCompleted)
            {
                await Task.Yield();
            }
            x.Complete();

            JobHandle handle = default;
            for (int i = 0; i < 8; i++)
            {
                Debug.Log($"Scheduling Partition {i}: Triangles: {partitions[i].triangles.Length}");
                var levelContainer = new VoxelTreeLevels<OctNode>(maxDepth - 1);
                levels = levelContainer.levels;
                var job = new VoxelizeTriJob(partitions[i].triangles, partitions[i].bounds, maxDepth-1, levelContainer);

                handle = JobHandle.CombineDependencies(handle, job.Schedule());
            }

            while (!handle.IsCompleted)
            {
                await Task.Yield();
            }

            Debug.Log($"Time: {sw1.ElapsedMilliseconds} ms");
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IVoxelizerOctNode GetNode(int level, int nodeIdx)
        {
            return levels[level][nodeIdx];
        }
    }
}