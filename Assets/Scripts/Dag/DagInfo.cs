using Assets.Scripts.Dag.Builders;
using Assets.Scripts.Octree;
using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Assets.Scripts.Dag
{
    /// <summary>
    /// Format of a DAG in file
    ///     Depth 0 > Depth 1 > Depth x
    ///     Save every validmask + child pointers of a node of in a depth => repeat with next depth
    /// Format of a NODE
    ///     32 bit => lower 8 bits: validMask which indicates if a child pointer exists
    ///     32 bits for every child pointer
    /// IF CRS4 (Symmetry-aware Sparse Voxel DAGs (SSVDAGs)for compression-domain tracing of high-resolution geometric scenes)
    ///     Last level doesnt contain any pointers just childmasks
    /// </summary>
    [CreateAssetMenu]
    public class DagInfo : RenderInfo
    {
        public DagFormat format;
        [SerializeField] private string path;

        public override string Path { get => path; set => path = value; }

        public static void WriteToFile(string path, IDagBuilder dag, bool overrideFiles)
        {
            var filename = path + ".dag";
            unsafe
            {
                using (var file = new FileStream(filename, overrideFiles ? FileMode.Create : FileMode.CreateNew))
                using (var bw = new BinaryWriter(file))
                {
                    int version = 1;
                    int dagFormat = GetFormatForBuilderInt(dag);

                    bw.Write(version);
                    bw.Write(dagFormat);

                    dag.WriteHeader(bw);
                    dag.WriteToFile(bw);
                }
            }
        }



        public override RenderData ReadFromFileNew()
        {
            unsafe
            {
                using (var file = new FileStream(path, FileMode.Open))
                using (var br = new BinaryReader(file))
                {
                    DagHeader h = new DagHeader();

                    h.version = br.ReadInt32();
                    h.format = (DagFormat)br.ReadInt32();

                    var builder = GetBuilderForFormat((int)h.format);

                    return builder.ReadFromFile(br);
                }
            }
        }

        public static RenderData Read(BinaryReader br)
        {
            DagHeader h = new DagHeader();

            h.version = br.ReadInt32();
            h.format = (DagFormat)br.ReadInt32();

            var builder = GetBuilderForFormat((int)h.format);

            return builder.ReadFromFile(br);
        }

        public static void Read<T>(BinaryReader br, NativeArray<T> cbArray, out int maxDepth) where T : unmanaged
        {
            DagHeader h = new DagHeader();

            h.version = br.ReadInt32();
            h.format = (DagFormat)br.ReadInt32();
            DagBuilderGray builder = (DagBuilderGray)GetBuilderForFormat((int)h.format);
            builder.ReadFromFile(br, cbArray, out maxDepth);
        }

        public static void Read(BinaryReader br, int[] cbArray, out int maxDepth)
        {
            DagHeader h = new DagHeader();

            h.version = br.ReadInt32();
            h.format = (DagFormat)br.ReadInt32();
            DagBuilderGray builder = (DagBuilderGray)GetBuilderForFormat((int)h.format);
            builder.ReadFromFile(br, cbArray, out maxDepth);
        }

        //public static void Write(BinaryWriter bw, IDagBuilder data)
        //{
        //    int version = 1;
        //    int dagFormat = GetFormatForBuilderInt(data);

        //    bw.Write(version);
        //    bw.Write(dagFormat);

        //    data.WriteHeader(bw);
        //}


        public static IDagBuilder GetBuilderForFormat(int format)
        {
            var formatEnum = (DagFormat)format;

            return formatEnum switch
            {
                DagFormat.Gray => new DagBuilderGray(),
                DagFormat.ColorPerPointer => new DagBuilderColorPerPointer(),
                DagFormat.ColorPerNode => new DagBuilderColorPerNode(),
                _ => throw new Exception($"Format {format} not found!"),
            };
        }

        public static DagFormat GetFormatForBuilder(IDagBuilder builder)
        {
            if (builder is DagBuilderGray) return DagFormat.Gray;
            if (builder is DagBuilderColorPerPointer) return DagFormat.ColorPerPointer;
            if (builder is DagBuilderColorPerNode) return DagFormat.ColorPerNode;

            throw new Exception($"Format for {builder} not found!");
        }

        public static int GetFormatForBuilderInt(IDagBuilder builder)
        {
            return (int)GetFormatForBuilder(builder);
        }

        public override int GetMaxDepth()
        {
            unsafe
            {
                using (var file = new FileStream(Path, FileMode.Open))
                using (var br = new BinaryReader(file))
                {
                    DagHeader h = new DagHeader();

                    h.version = br.ReadInt32();
                    h.format = (DagFormat)br.ReadInt32();

                    var builder = GetBuilderForFormat((int)h.format);
                    DagHeader header = new DagHeader();
                    builder.ReadHeader(br, ref header);

                    return header.maxDepth;
                }
            }
        }
    }
}