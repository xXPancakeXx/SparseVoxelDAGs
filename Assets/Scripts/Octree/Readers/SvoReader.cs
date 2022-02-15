using Assets.Scripts.Octree.Builders;
using Assets.Scripts.Voxelization.Entities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using static Assets.Scripts.Octree.SvoInfo;

namespace Assets.Scripts.Octree.Readers
{
    public class SvoReaderSequential : IDisposable, IEnumerator<IVoxelizerOctNode>
    {
        private BinaryReader br;
        private ISvoBuilder builder;

        private int currentDepth;

        public int MaxDepth => builder.MaxDepth;
        public uint NodeCount => builder.NodeCount;
        public int CurrentDepth => currentDepth;

        public object Current { get; private set; }
        IVoxelizerOctNode IEnumerator<IVoxelizerOctNode>.Current => (IVoxelizerOctNode)Current;


        public SvoReaderSequential(string path)
        {
            this.br = new BinaryReader(File.Open(path, FileMode.Open));

            var format = br.ReadInt32();
            this.builder = SvoInfo.GetBuilderForFormat(format);
            this.builder.ReadHeader(br);
        }

        public IEnumerator GetEnumerator()
        {
            return this;
        }

        public bool MoveNext()
        {
            var hasNode = builder.ReadBreadth(br, out var node, out currentDepth);
            Current = node;
            return hasNode;
        }

        public void Reset()
        {
            br.BaseStream.Seek(builder.HeaderByteSize, SeekOrigin.Begin);
        }

        public void Dispose()
        {
            br.Dispose();
        }
    }

    public class SvoNodeColorFullReaderRandom : IDisposable
    {
        private MemoryMappedFile mmf;
        private MemoryMappedViewAccessor nodeAccessor;
        private MemoryMappedViewAccessor colorAccessor;

        private ISvoBuilder builder;

        public int MaxDepth => builder.MaxDepth;
        public uint NodeCount => builder.NodeCount;

        public SvoNodeColorFullReaderRandom(string path)
        {
            using (var br = new BinaryReader(File.Open(path, FileMode.Open)))
            {
                var svoFormat = br.ReadInt32();

                if ((SvoFormat)svoFormat != SvoFormat.ColorFull) throw new Exception($"Only able to read format: ColorFull, but found {svoFormat}");
                this.builder = GetBuilderForFormat(svoFormat);
                this.builder.ReadHeader(br);
            }

            this.mmf = MemoryMappedFile.CreateFromFile(path, FileMode.Open);
            this.nodeAccessor = mmf.CreateViewAccessor(builder.HeaderByteSize, builder.NodeByteSize);
            this.colorAccessor = mmf.CreateViewAccessor(builder.HeaderByteSize + builder.NodeByteSize, builder.ColorByteSize);

        }
        public unsafe SvoNodeColor GetNode(long index)
        {
            var byteIndex = index * 8;

            SvoNodeColor node;
            nodeAccessor.Read(byteIndex, out node);
            return node;
        }

        public int GetColor(long index)
        {
            var byteIndex = index * 4;

            return colorAccessor.ReadInt32(byteIndex);
        }

        public void Dispose()
        {
            nodeAccessor.Dispose();
            colorAccessor.Dispose();
            mmf.Dispose();
        }
    }


}