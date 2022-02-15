using Assets.Scripts.Streaming.Entities;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Assets.Scripts.Streaming
{
    public class WorldController : IDisposable
    {
        public const int chunkDataSize = 4;
        public const int VOXEL_DATA_STRIDE = 4;

        private const int chunkSizeByte = 6 * 1024 * 1024;

        private int chunkLoadRange = 2;

        private ChunkLoader loader;
        private int2 curChunkPos;
        private Dictionary<int2, int> loadedChunksToIndices;
        private Queue<int> freeBuffers;

        public ComputeBuffer chunkDataBuffer;
        public ComputeBuffer chunkVoxelBuffer;
        public WorldInfo worldInfo;

        public Dictionary<int2, int> LoadedChunkSlots => loadedChunksToIndices;
        public int ChunkSizeInt => chunkSizeByte / 4;
        public int2 ChunkDims => worldInfo.ChunkDims;
        public int MaxActiveChunkCount => (chunkLoadRange * 2 + 1) * (chunkLoadRange * 2 + 1);

        public int ChunkLoadRange
        {
            get => chunkLoadRange;
            set { chunkLoadRange = value; ChangeActiveChunkCount(); LoadChunksAtPosition(Camera.main.transform.position); }
        }

        public WorldController(WorldInfo wi, int chunkLoadRange, int chunkUploadSizeLimitPerFrameMBytes, float chunkUploadTimeDelay)
        {
            worldInfo = wi;
            this.chunkLoadRange = chunkLoadRange;
            loader = new ChunkLoader(2 * (chunkLoadRange * 2 + 1), chunkSizeByte, chunkUploadSizeLimitPerFrameMBytes, chunkUploadTimeDelay);

            //represents square arround camera TODO: update the count to only contain chunks which can be in view (not 360° only 180° or fov or sth)
            if (((long)MaxActiveChunkCount * chunkSizeByte) > int.MaxValue)
                throw new Exception("Chunks can not exceed 2GB (2^31 Bytes) of memory");

            loadedChunksToIndices = new Dictionary<int2, int>();
            freeBuffers = new Queue<int>(MaxActiveChunkCount);
            //chunkVoxelBuffer = new ComputeBuffer(activeChunkCount * chunkSizeByte / 4, 4, ComputeBufferType.Default, ComputeBufferMode.SubUpdates);
            //chunkDataBuffer = new ComputeBuffer(ChunkDims.x * ChunkDims.y, chunkDataSize, ComputeBufferType.Default, ComputeBufferMode.SubUpdates);

            chunkVoxelBuffer = new ComputeBuffer(chunkSizeByte / VOXEL_DATA_STRIDE * MaxActiveChunkCount, VOXEL_DATA_STRIDE, ComputeBufferType.Default);
            chunkDataBuffer = new ComputeBuffer(ChunkDims.x * ChunkDims.y, chunkDataSize, ComputeBufferType.Default);

            //set all chunks as free
            for (int i = 0; i < MaxActiveChunkCount; i++)
            {
                freeBuffers.Enqueue(i);
            }

            //init chunk data buffer with our chunk not loaded representation (1 < 31)
            //var arr = chunkDataBuffer.BeginWrite<int>(0, ChunkDims.x * ChunkDims.y);
            //for (int i = 0; i < ChunkDims.x * ChunkDims.y; i++)
            //{
            //    arr[i] = 1 << 31;
            //}
            //chunkDataBuffer.EndWrite<int>(activeChunkCount);

            var arr = new NativeArray<int>(ChunkDims.x * ChunkDims.y, Allocator.Temp);
            for (int i = 0; i < ChunkDims.x * ChunkDims.y; i++)
            {
                arr[i] = 1 << 31;
            }
            chunkDataBuffer.SetData(arr);
            arr.Dispose();

            var cam = Camera.main;
            LoadChunksAtPosition(cam.transform.position);
        }

        public void Update()
        {
            loader.Update();

            UpdateWorldBuffers(Camera.main.transform);
        }


        public void ChangeActiveChunkCount()
        {
            var activeChunkCount = (chunkLoadRange * 2 + 1) * (chunkLoadRange * 2 + 1);
            if (activeChunkCount * chunkSizeByte > int.MaxValue) throw new Exception("Chunks can not exceed 2GB (2^31 Bytes) of memory");

            chunkVoxelBuffer = new ComputeBuffer(activeChunkCount * chunkSizeByte / 4, 4, ComputeBufferType.Default);
        }

        public void LoadChunksAtPosition(float3 worldPos)
        {
            var cam = Camera.main;
            int ymin = math.max((int)worldPos.z - chunkLoadRange, 0);
            int ymax = math.min((int)worldPos.z + chunkLoadRange, ChunkDims.x - 1);
            int xmin = math.max((int)worldPos.x - chunkLoadRange, 0);
            int xmax = math.min((int)worldPos.x + chunkLoadRange, ChunkDims.y - 1);

            curChunkPos = WorldToChunkPos(cam.transform.position);
            for (int y = ymin; y <= ymax; y++)
            {
                for (int x = xmin; x <= xmax; x++)
                {
                    LoadChunkIntoMemory(new int2(x, y));
                }
            }
        }

        public void UpdateWorldBuffers(Transform camTrans)
        {
            float3 pos = camTrans.position;
            var newChunkPos = WorldToChunkPos(pos);
            var chunkChange = newChunkPos - curChunkPos;

            if (chunkChange.x == 0 && chunkChange.y == 0) return;
            if (chunkChange.x > 0)
            {
                //unload left most chunks & load right + 1 chunks
                for (int y = 0; y < chunkLoadRange * 2 + 1; y++)
                {
                    UnloadChunk(new int2(curChunkPos.x - chunkLoadRange, curChunkPos.y - chunkLoadRange + y));
                    LoadChunkIntoMemoryAsync(new int2(curChunkPos.x + chunkLoadRange + 1, curChunkPos.y - chunkLoadRange + y));
                }
            }
            else if (chunkChange.x < 0)
            {
                //unload right most chunks & load left - 1 chunks
                for (int y = 0; y < chunkLoadRange * 2 + 1; y++)
                {
                    UnloadChunk(new int2(curChunkPos.x + chunkLoadRange, curChunkPos.y - chunkLoadRange + y));
                    LoadChunkIntoMemoryAsync(new int2(curChunkPos.x - chunkLoadRange - 1, curChunkPos.y - chunkLoadRange + y));
                }
            }

            if (chunkChange.y > 0)
            {
                //unload bottom most chunks & load top + 1 chunks
                for (int x = 0; x < chunkLoadRange * 2 + 1; x++)
                {
                    UnloadChunk(new int2(curChunkPos.x - chunkLoadRange + x, curChunkPos.y - chunkLoadRange));
                    LoadChunkIntoMemoryAsync(new int2(curChunkPos.x - chunkLoadRange + x, curChunkPos.y + chunkLoadRange + 1));

                }
            }
            else if (chunkChange.y < 0)
            {
                //unload top most chunks & load bottom - 1 chunks
                for (int x = 0; x < chunkLoadRange * 2 + 1; x++)
                {
                    UnloadChunk(new int2(curChunkPos.x - chunkLoadRange + x, curChunkPos.y + chunkLoadRange));
                    LoadChunkIntoMemoryAsync(new int2(curChunkPos.x - chunkLoadRange + x, curChunkPos.y - chunkLoadRange - 1));
                }
            }

            curChunkPos = newChunkPos;
        }


        public void LoadChunkIntoMemoryAsync(int2 pos)
        {
            if (math.any(pos < 0) || math.any(pos >= ChunkDims)) return;

            if (!loadedChunksToIndices.ContainsKey(pos))
            {
                var chunkBufferIndex = freeBuffers.Dequeue();
                loadedChunksToIndices.Add(pos, chunkBufferIndex);

                loader.QueueLoadAsyncRead(
                    pos,
                    worldInfo,
                    chunkVoxelBuffer, chunkBufferIndex, chunkSizeByte,
                    chunkDataBuffer
                );
            }
        }

        public void LoadChunkIntoMemory(int2 pos)
        {
            if (math.any(pos < 0) || math.any(pos >= ChunkDims)) return;

            if (!loadedChunksToIndices.ContainsKey(pos))
            {
                var chunkBufferIndex = freeBuffers.Dequeue();
                loadedChunksToIndices.Add(pos, chunkBufferIndex);

                loader.LoadNative(
                    pos,
                    worldInfo,
                    chunkVoxelBuffer, chunkBufferIndex, chunkSizeByte,
                    chunkDataBuffer
                );
            }
        }

        public void UnloadChunk(int2 pos)
        {
            if (loadedChunksToIndices.TryGetValue(pos, out var bufferIndex))
            {
                Debug.Log($"UNLOAD: chunk {pos}");

                loadedChunksToIndices.Remove(pos);
                freeBuffers.Enqueue(bufferIndex);
            }
            else
            {
                Debug.Log($"UNLOAD: Tried chunk {pos} but was already unloaded");
            }
        }


        public int2 WorldToChunkPos(float3 worldPos)
        {
            return new int2((int)worldPos.x, (int)worldPos.z);
        }

        public void Dispose()
        {
            chunkVoxelBuffer?.Dispose();
            chunkVoxelBuffer = null;

            chunkDataBuffer?.Dispose();
            chunkDataBuffer = null;

            loader.Dispose();
        }
    }
}