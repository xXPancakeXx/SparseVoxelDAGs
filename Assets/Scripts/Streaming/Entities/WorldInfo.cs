using Assets.Scripts.Streaming.Entities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Streaming
{
    [CreateAssetMenu()]
    public class WorldInfo : ScriptableObject
    {
        public int2 chunkDims;
        [SerializeField] public WorldChunk[] worldChunks;

        public int2 ChunkDims => chunkDims;
        public int ChunkCount => chunkDims.x * chunkDims.y;

#if UNITY_EDITOR
        [ContextMenu("Init")]
        public void InitWorld()
        {
            var path = AssetDatabase.GetAssetPath(this);

            int x = 0, y = 0;
            var fs = Directory.GetFiles(Path.GetDirectoryName(path), "*.dag");
            if (fs.Length == 0) throw new Exception("No other files in folder");
            for (int i = 0; i < fs.Length; i++)
            {
                var split = fs[i].Split('#');

                x = math.max(x, Convert.ToInt32(split[1]));
                y = math.max(y, Convert.ToInt32(split[2]));
            }
            if (x == 0 && y == 0) throw new Exception("Format doesnt match! need dims in hastags (#x#y#)");

            chunkDims = new int2(++x, ++y);
            worldChunks = new WorldChunk[ChunkCount];
            for (int i = 0; i < fs.Length; i++)
            {
                var split = fs[i].Split('#');

                x = Convert.ToInt32(split[1]);
                y = Convert.ToInt32(split[2]);

                worldChunks[Chunk.Flatten(ChunkDims, x, y)] = new WorldChunk { path = Path.GetFullPath(fs[i]) };
            }
        }

        [ContextMenu("Init2")]
        public void InitChunkDims()
        {
            var path = AssetDatabase.GetAssetPath(this);
            var sampleFile = Directory.GetFiles(Path.GetDirectoryName(path), "*.dag")[0];

            for (int y = 0; y < ChunkDims.y; y++)
            {
                for (int x = 0; x < ChunkDims.x; x++)
                {
                    if (File.Exists(Path.Combine(Path.GetDirectoryName(path), $"city#{x}#{y}#.dag"))) continue;

                    File.Copy(sampleFile, Path.Combine(Path.GetDirectoryName(path), $"city#{x}#{y}#.dag"));
                }
            }
        }
#endif
    }

    [Serializable]
    public class WorldChunk
    {
        public string path;


    }
}