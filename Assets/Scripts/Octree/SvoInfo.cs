using Assets.Scripts.Octree.Readers;
using Utils;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Assets.Scripts.Octree.Builders;
using UnityEditor;
using Assets.Scripts.Dag.Builders;
using Assets.Scripts.Dag;
using Assets.Scripts.Voxelization.Entities;
using static Assets.Scripts.Octree.SvoInfo;
using System.Collections;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;

namespace Assets.Scripts.Octree
{
    //Tri data file
    //A.tridata file is a very simple binary file which just contains a list of the x, y and z coordinates of the triangle vertices (v0, v1 and v2), followed by the optional payload per triangle(normal, texture information, ...).

    //In case of geometry-only(or, confusingly called binary, as in binary voxelization) .tridata files, the layout per triangle looks like this:

    //vertex 0 (3 x 32 bit float)
    //vertex 1 (3 x 32 bit float)
    //vertex 2 (3 x 32 bit float)

    //In case of a payload.tridata file, the layout per triangle is followed by
    //normal(3 x 32 bit float)
    //vertex 0 color(3 x 32 bit float)
    //vertex 1 color(3 x 32 bit float)
    //vertex 2 color(3 x 32 bit float)

    //*****************************************************************************

    //Octree node file
    //An .octreenodes file is a binary file which describes the big flat array of octree nodes. In the nodes, there are only child pointers, which are constructed from a 64 - bit base address combined with a child offset, since all nonempty children of a certain node are guaranteed by the algorithm to be stored next to eachother. The.octreenodes file contains an amount of n_nodes nodes.

    //children base address: (size_t, 64 bits) The base address of the children of this node.
    //child offsets: (8 * 8 bit char = 64 bits) Children offsets for each of the 8 children.Each is a number between - 1 and 7.
    //If the number is >= 0, add it to the children base address the get the actual child address.
    //A child offset of - 1 is interpreted as a NULL pointer : there's no child there.
    //data address: (size_t, 64 bits) Index of data payload in data array described in the.octreedata file(see further).
    //If the address is 0, this is a data NULL pointer : there's no data associated with this node

    //*****************************************************************************

    //Octree data file
    //An.octreedata file is a binary file representing the big flat array of data payloads.Nodes in the octree refer to their data payload by using a 64-bit pointer,
    //which corresponds to the index in this data array.
    //The first data payload in this array is always the one representing an empty payload.Nodes refer to this if they have no payload (internal nodes in the tree, ...).

    //The current payload contains a morton code, normal vector and color information.
    //This can be easily extended with more appearance data.I refer to the 'Appearance' section in our paper.
    //As described in the.octreeheader, the.octreedata file contains n_data nodes.

    //morton: (64 bit unsigned int) Morton code of this voxel payload
    //color: (3 * 32 bit float = 96 bits) RGB color, three float values between 0 and 1.
    //normal vector: (3 * 32 bit float = 96 bits) x, y and z components of normal vector of this voxel payload.


    public class SvoInfo : RenderInfo
    {
        public enum SvoFormat
        {
            Gray, 
            Color,
            GrayFull,
            ColorFull,
        }

        public enum HierachyOrder { Breadth, Depth }
        public enum NodeType { GrayLaine, Color }

        [SerializeField] private string path;

        public override string Path { get => path; set => path = value; }
        
        public ISvoBuilder GetBuilderFromFile(BinaryReader br)
        {
            var format = br.ReadInt32();
            var builder = GetBuilderForFormat(format);
            builder.ReadHeader(br);

            return builder;
        }


        public SvoFormat GetFormat()
        {
            using (var br = new BinaryReader(File.Open(path, FileMode.Open)))
            {
                return (SvoFormat) br.ReadInt32();
            }
        }

        public override int GetMaxDepth()
        {
            using (var br = new BinaryReader(File.Open(path, FileMode.Open)))
            {
                var format = br.ReadInt32();
                var builder = GetBuilderForFormat(format);
                builder.ReadHeader(br);
                
                return builder.MaxDepth;
            }
        }

        public SvoReaderSequential GetSeqFileReader()
        {
            return new SvoReaderSequential(path);
        }

        public SvoNodeColorFullReaderRandom GetRandomFileReader()
        {
            return new SvoNodeColorFullReaderRandom(path);
        }

        public override RenderData ReadFromFileNew()
        {
            using (var br = new BinaryReader(File.Open(path, FileMode.Open)))
            {
                var format = br.ReadInt32();
                var builder = GetBuilderForFormat(format);

                var data = builder.ReadFromFile(br);
                return data;
            }
        }
       
        public static VoxelConverterSVOInfo ParseOctreeHeaderForceFlow(string octreeHeaderFilePath)
        {
            //A typical .octree header file is text - based and looks like this.All keyword - values lines are separated by a newline:

            //#octreeheader 1
            //gridlength 1024
            //n_nodes 1474721
            //n_data 484322
            //end

            var lines = File.ReadAllLines(octreeHeaderFilePath);
            if (lines[0] != "#octreeheader 1") throw new Exception($"Header file {octreeHeaderFilePath} not in the right format!");

            var gridLength = Convert.ToInt32(lines[1].Split(' ')[1]);
            var nodes = Convert.ToUInt64(lines[2].Split(' ')[1]);
            var data = Convert.ToUInt64(lines[3].Split(' ')[1]);


            return new VoxelConverterSVOInfo(gridLength, nodes, data);
        }
        public unsafe VoxelConverterSVONode* ReadOctreeFileForceFlow(out VoxelConverterSVOInfo header)
        {
            return ReadOctreeFileForceFlow(Path, out header);
        }
        public static unsafe VoxelConverterSVONode* ReadOctreeFileForceFlow(string path, out VoxelConverterSVOInfo header)
        {
            header = ParseOctreeHeaderForceFlow(path + ".octree");
            var byteSize = header.nodes * 8 * 3;
            if (byteSize >= Int32.MaxValue) return null;

            VoxelConverterSVONode* nodesPtr;
            using (var file = new FileStream(path + ".octreenodes", FileMode.Open))
            using (var sr = new StreamReader(file))
            using (var br = new BinaryReader(file))
            {
                byte* ptr = (byte*)Marshal.AllocHGlobal(new IntPtr((long)byteSize)).ToPointer();

                var octNodes = br.ReadBytes((int)byteSize);
                fixed (byte* octNodesPtr = octNodes)
                {
                    UnsafeMemory.CopyMemory(new IntPtr(ptr), new IntPtr(octNodesPtr), (uint)byteSize);
                }

                nodesPtr = (VoxelConverterSVONode*)ptr;
            }

            return nodesPtr;
        }

        public static void ConvertFormatForceFlow2Own(string sourcePath, string outPath, bool useColors)
        {
            Debug.Log("Converting generated SVO to own streamable format...");

            var sw1 = Stopwatch.StartNew();

            using var reader = new VoxelConverterSVOReaderRandom(sourcePath);
            using var bw = new BinaryWriter(File.Open(outPath, FileMode.Create));
            var builder = new SvoConverter(reader, bw);
            if (useColors) throw new NotImplementedException();
            else builder.ConvertFormatBreadth();


            Debug.Log($"Conversion stats - Resolution: {reader.GridDimension}, Time: {sw1.ElapsedMilliseconds} ms");
        }

        
        public static ISvoBuilder GetBuilderForFormat(int format)
        {
            var formatEnum = (SvoFormat) format;

            return formatEnum switch
            {
                SvoFormat.Gray => new SvoBuilderGray(),
                SvoFormat.ColorFull => new SvoBuilderColorFull(),
                SvoFormat.GrayFull => new SvoBuilderGrayFull(),
                _ => throw new Exception($"Format {(SvoFormat) format} not found!"),
            };
        }

        public static SvoFormat GetFormatForBuilder(ISvoBuilder builder)
        {
            if (builder is SvoBuilderGray) return SvoFormat.Gray;
            if (builder is SvoBuilderColor) return SvoFormat.ColorFull;
            if (builder is SvoBuilderGrayFull) return SvoFormat.GrayFull;

            throw new Exception($"Format for {builder} not found!");
        }
    }
}