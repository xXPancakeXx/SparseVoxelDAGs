using Assets.Scripts.Dag;
using Assets.Scripts.Octree;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Utils;

namespace Assets.Scripts.Streaming.Entities
{
    public class ChunkLoader : IDisposable
    {
        private NativeArray<int>[] chunkTempLoadBuffers;
        private Queue<int> freeChunkLoadBuffers = new Queue<int>();

        private readonly List<ChunkLoadData> scheduledJobs = new List<ChunkLoadData>();

        private readonly int maxUploadBytesFrame;

        private readonly float chunkStreamDelay;
        private float elapsedTime;

        public ChunkLoader(int tempBuffersCount, int chunkSizeBytes, int chunkUploadSizeLimitPerFrameMBytes = 1, float chunkStreamDelay = .3f)
        {
            if (chunkSizeBytes % 4 != 0) throw new Exception("Chunk size needs to be a multiple of 4");
            //if (chunkUploadSizeLimitPerFrameMBytes % 2 != 0) throw new Exception("chunkUploadSizeLimitPerFrameMBytes needs to be a multiple of 2");

            chunkTempLoadBuffers = new NativeArray<int>[tempBuffersCount];
            for (int i = 0; i < tempBuffersCount; i++)
            {
                chunkTempLoadBuffers[i] = new NativeArray<int>(chunkSizeBytes / 4, Allocator.Persistent);
                freeChunkLoadBuffers.Enqueue(i);
            }

            this.chunkStreamDelay = chunkStreamDelay;
            this.maxUploadBytesFrame = chunkSizeBytes / chunkUploadSizeLimitPerFrameMBytes;
            this.maxUploadBytesFrame = chunkUploadSizeLimitPerFrameMBytes * 1024 * 1024;
        }

        public void Update()
        {
            elapsedTime += Time.deltaTime;
            if (elapsedTime <= chunkStreamDelay) return;

            elapsedTime = 0;
            for (int i = scheduledJobs.Count - 1; i >= 0; i--)
            {
                var jobi = scheduledJobs[i];
                if (scheduledJobs[i].JobHandle.IsCompleted)
                {
                    //when uploading is complete remove from list
                    if (jobi.task.Commit(maxUploadBytesFrame))
                    {
                        scheduledJobs.RemoveAt(i);
                        jobi.GCHandle.Free();

                        if (jobi.task is LoadChunkDiskTask task)
                        {
                            freeChunkLoadBuffers.Enqueue(task.loadBufferIdx);
                        }
                    }

                    //Only allow a single job to complete in a frame
                    return;
                }
            } 
        }

        public void QueueLoadBeginWrite(int2 pos, WorldInfo worldInfo, ComputeBuffer voxelData, int chunkBufferIndex, int chunkSize, ComputeBuffer chunkData)
        {
            Debug.Log($"LOAD QUEUED: chunk ({pos.x}, {pos.y})");

            var task = new LoadChunkTask(pos, chunkBufferIndex, chunkSize, worldInfo, voxelData, chunkData);
            task.Prepare();

            var data = new ChunkLoadData(task);

            if (scheduledJobs.Count > 0) data.Schedule(scheduledJobs[scheduledJobs.Count - 1].JobHandle);
            else data.Schedule(default);

            scheduledJobs.Add(data); // We remember this so we can free it later
        }

        public void QueueLoadAsyncRead(int2 pos, WorldInfo worldInfo, ComputeBuffer voxelData, int chunkBufferIndex, int chunkSize, ComputeBuffer chunkData)
        {
            if (freeChunkLoadBuffers.Count == 0) throw new Exception("ChunkLoader: Out of free load buffers. Please allow a greater buffer count or wait before queueing load requests");

            Debug.Log($"LOAD QUEUED: chunk ({pos.x}, {pos.y})");

            var loadBufferIdx = freeChunkLoadBuffers.Dequeue();
            var task = new LoadChunkDiskTask(pos, chunkBufferIndex, chunkSize, worldInfo, chunkTempLoadBuffers[loadBufferIdx], voxelData, chunkData, loadBufferIdx);

            var data = new ChunkLoadData(task);

            if (scheduledJobs.Count > 0) data.Schedule(scheduledJobs[scheduledJobs.Count - 1].JobHandle);
            else data.Schedule(default);

            scheduledJobs.Add(data); // We remember this so we can free it later
        }

        struct Stride128
        {
            long a64;
            long b64;
        }

        public void LoadNative(int2 chunkPos, WorldInfo worldInfo, ComputeBuffer voxelData, int chunkBufferIndex, int chunkSizeBytes, ComputeBuffer chunkData)
        {
            Debug.Log($"LOAD SYNC: chunk ({chunkPos.x}, {chunkPos.y})");

            var chunk = Chunk.GetChunkInfo(worldInfo, 16 - chunkPos.y, chunkPos.x);
            int maxDepth; var voxelBuffer = new NativeArray<int>(chunkSizeBytes / WorldController.VOXEL_DATA_STRIDE, Allocator.Temp);
            using (var br = new BinaryReader(File.Open(chunk.path, FileMode.Open)))
            {
                DagInfo.Read(br, voxelBuffer, out maxDepth);
            }
            voxelData.SetData(voxelBuffer, 0, chunkBufferIndex * (chunkSizeBytes / WorldController.VOXEL_DATA_STRIDE), chunkSizeBytes / WorldController.VOXEL_DATA_STRIDE);
            voxelBuffer.Dispose();

            var chunkDataIndex = Chunk.GetChunkDataIndex(worldInfo, chunkPos.x, chunkPos.y);
            var chunkInfo = (chunkBufferIndex << 4) | (maxDepth & 0xF);
            chunkData.SetData(new int[] { chunkInfo }, 0, chunkDataIndex, 1);
        }

        public void LoadManaged(int2 chunkPos, WorldInfo worldInfo, ComputeBuffer voxelData, int chunkBufferIndex, int chunkSizeBytes, ComputeBuffer chunkData)
        {
            Debug.Log($"LOAD SYNC: chunk ({chunkPos.x}, {chunkPos.y})");

            var chunk = Chunk.GetChunkInfo(worldInfo, 16 - chunkPos.y, chunkPos.x);
            int maxDepth; var voxelBuffer = new int[chunkSizeBytes / 4];
            using (var br = new BinaryReader(File.Open(chunk.path, FileMode.Open)))
            {
                DagInfo.Read(br, voxelBuffer, out maxDepth);
            }
            voxelData.SetData(voxelBuffer, 0, chunkBufferIndex * (chunkSizeBytes / 4), chunkSizeBytes / 4);

            var chunkDataIndex = Chunk.GetChunkDataIndex(worldInfo, chunkPos.x, chunkPos.y);
            var chunkInfo = (chunkBufferIndex << 4) | (maxDepth & 0xF);
            chunkData.SetData(new int[] { chunkInfo }, 0, chunkDataIndex, 1);
        }

        public void Dispose()
        {
            for (int i = 0; i < chunkTempLoadBuffers.Length; i++)
            {
                chunkTempLoadBuffers[i].Dispose();
            }
        }

        private class ChunkLoadData : IDisposable
        {
            public IChunkLoadTask task;

            public JobHandle JobHandle { get; private set; }
            public GCHandle GCHandle { get; }


            public ChunkLoadData(IChunkLoadTask task)
            {
                this.task = task;
                this.GCHandle = GCHandle.Alloc(task);
            }

            public void Schedule(JobHandle prevScheduledJob)
            {
                this.JobHandle = new Job { handle = GCHandle }.Schedule(prevScheduledJob);
            }

            public void Dispose()
            {
                GCHandle.Free();
            }
        }

        private interface IChunkLoadTask 
        {
            public void Execute();

            /// <summary>
            /// Signals the task that data can be uploaded to the GPU
            /// </summary>
            /// <param name="maxUploadBytes">The maximal bytes to upload every frame</param>
            /// <returns>true if all data has been uploaded</returns>
            public bool Commit(int maxUploadBytes);
        }


        private struct Job : IJob
        {
            public GCHandle handle;

            public void Execute()
            {
                IChunkLoadTask task = (IChunkLoadTask)handle.Target;
                task.Execute();
            }
        }

        class LoadChunkTask : IChunkLoadTask
        {
            public int2 chunkPos;

            public ComputeBuffer voxelBuffer;
            public int chunkVoxelIndex;
            public int chunkSizeBytes;
            private NativeArray<int> voxelArr;

            public ComputeBuffer chunkBuffer;
            private NativeArray<int> chunkDataArr;

            public WorldInfo worldInfo;

            public LoadChunkTask(int2 chunkPos, int chunkVoxelIndex, int chunkSizeBytes, WorldInfo worldInfo, ComputeBuffer voxelBuffer, ComputeBuffer chunkBuffer)
            {
                this.chunkPos = chunkPos;
                this.chunkVoxelIndex = chunkVoxelIndex;
                this.chunkSizeBytes = chunkSizeBytes;
                this.worldInfo = worldInfo;
                this.voxelBuffer = voxelBuffer;
                this.chunkBuffer = chunkBuffer;
            }

            public void Prepare()
            {
                voxelArr = voxelBuffer.BeginWrite<int>(chunkVoxelIndex * (chunkSizeBytes / 4), chunkSizeBytes / 4);

                var chunkDataIndex = Chunk.GetChunkDataIndex(worldInfo, chunkPos.x, chunkPos.y);
                chunkDataArr = chunkBuffer.BeginWrite<int>(chunkDataIndex, 1);
            }

            public void Execute()
            {
                Debug.Log($"Loading ({16 - chunkPos.y}, {chunkPos.x}) from disk");

                var chunk = Chunk.GetChunkInfo(worldInfo, 16 - chunkPos.y, chunkPos.x);
                if (chunk == null) return;

                int maxDepth;
                using (var br = new BinaryReader(File.Open(chunk.path, FileMode.Open)))
                {
                    DagInfo.Read(br, voxelArr, out maxDepth);
                }

                chunkDataArr[0] = (chunkVoxelIndex << 4) | (maxDepth & 0xF);
                //chunkDataArr[0] = 1;

                var chunkDataIndex = Chunk.GetChunkDataIndex(worldInfo, chunkPos.x, chunkPos.y);
                Debug.Log($"Loaded {chunkPos} into buffer at chunk slot: {chunkVoxelIndex} and added reference at {chunkDataIndex}");

            }

            public bool Commit(int maxUploadSize)
            {
                voxelBuffer.EndWrite<int>(chunkSizeBytes / 4);
                chunkBuffer.EndWrite<int>(1);

                return true;
            }
        }


        class LoadChunkDiskTask : IChunkLoadTask
        {
            public int2 chunkPos;
            public WorldInfo worldInfo;

            public int chunkVoxelIndex;
            public int chunkSizeBytes;

            private readonly ComputeBuffer voxelCb;
            private readonly ComputeBuffer chunkCb;

            public int loadBufferIdx;

            //This field will be written to
            public NativeArray<int> voxelLoadBuffer;
            public int managementData;

            private int uploadedBytes;

            public LoadChunkDiskTask(int2 chunkPos, int chunkVoxelIndex, int chunkSizeBytes, WorldInfo worldInfo, NativeArray<int> voxelLoadBuffer, ComputeBuffer voxelCb, ComputeBuffer chunkCb, int loadBufferIdx)
            {
                this.chunkPos = chunkPos;
                this.chunkVoxelIndex = chunkVoxelIndex;
                this.chunkSizeBytes = chunkSizeBytes;
                this.voxelLoadBuffer = voxelLoadBuffer;
                this.voxelCb = voxelCb;
                this.chunkCb = chunkCb;
                this.worldInfo = worldInfo;
                this.loadBufferIdx = loadBufferIdx;
            }

            public void Execute()
            {
                //Debug.Log($"LOAD TASK CHUNK: chunk ({16 - chunkPos.y}, {chunkPos.x}) from disk");

                var chunk = Chunk.GetChunkInfo(worldInfo, 16 - chunkPos.y, chunkPos.x);
                if (chunk == null) return;

                int maxDepth;
                using (var br = new BinaryReader(File.Open(chunk.path, FileMode.Open)))
                {
                    DagInfo.Read(br, voxelLoadBuffer, out maxDepth);
                }

                managementData = (chunkVoxelIndex << 4) | (maxDepth & 0xF);

                //Debug.Log($"LOAD TASK CHUNK: chunk {chunkPos} into buffer at chunk slot: {chunkVoxelIndex} and added reference at {chunkDataIndex}");
            }

            public bool Commit(int maxUploadBytes)
            {
                int bufferStartIdx = uploadedBytes / 4;
                int gpuBufferStartIdx = chunkVoxelIndex * (chunkSizeBytes / 4) + uploadedBytes / 4;

                int bytesToUpload = math.min(maxUploadBytes, voxelLoadBuffer.Length * 4 - uploadedBytes);

                //using (var sw = new SW($"GPU Upload (Size={maxUploadBytes}B):", true))
                //{
                    voxelCb.SetData(voxelLoadBuffer, bufferStartIdx, gpuBufferStartIdx, bytesToUpload / 4);
                //}

                uploadedBytes += bytesToUpload;

                //If everything has been uploaded to the GPU set the chunk buffer, so this chunk counts as valid
                if (uploadedBytes == chunkSizeBytes)
                {
                    var chunkDataIndex = Chunk.GetChunkDataIndex(worldInfo, chunkPos.x, chunkPos.y);
                    chunkCb.SetData(new int[] { managementData }, 0, chunkDataIndex, 1);
                    return true;
                }

                return false;
            }
        }
    }
}