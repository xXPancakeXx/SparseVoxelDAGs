using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Streaming.Entities
{
    public static class Chunk
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static WorldChunk GetChunkInfo(WorldInfo info, int x, int y)
        {
            return info.worldChunks[Flatten(info.ChunkDims, x, y)];
        }

        public static int GetChunkDataIndex(WorldInfo info, int x, int y)
        {
            return Flatten(info.ChunkDims, x, y);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Flatten(int2 chunkDims, int x, int y)
        {
            return y * chunkDims.x + x;
        }

        public static int WorldToChunkPos(int2 chunkDims, float3 worldPos)
        {
            return chunkDims.x * (int)worldPos.z + (int)worldPos.x;
        }
    }
}