using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.MeshPartitioning
{
    public class ModelWriterPly : IDisposable
    {
        private BinaryWriter tempVerts;
        private BinaryWriter tempFaces;

        private readonly string filePath;

        private int vertexCount;
        private int faceCount;

        public ModelWriterPly(string filePath)
        {
            tempVerts = new BinaryWriter(new FileStream(filePath + "temp.verts", FileMode.Create));
            tempFaces = new BinaryWriter(new FileStream(filePath + "temp.faces", FileMode.Create));
            this.filePath = filePath;
        }

        private void WriteHeader(BinaryWriter outFile)
        {
            outFile.Write(ToByteArray("ply\n"));
            outFile.Write(ToByteArray("format binary_little_endian 1.0\n"));
            outFile.Write(ToByteArray($"element vertex {vertexCount}\n"));
            outFile.Write(ToByteArray("property float x\n"));
            outFile.Write(ToByteArray("property float y\n"));
            outFile.Write(ToByteArray("property float z\n"));

            if (faceCount > 0)
            {
                outFile.Write(ToByteArray($"element face {faceCount}\n"));
                outFile.Write(ToByteArray("property list uchar int vertex_index\n"));
            }
            

            outFile.Write(ToByteArray("end_header\n"));
        }

        public void WriteVertex(float3 vertex)
        {
            tempVerts.Write(vertex.x);
            tempVerts.Write(vertex.y);
            tempVerts.Write(vertex.z);

            vertexCount++;
        }

        public void WriteFace(int a, int b, int c)
        {
            tempFaces.Write((byte) 3);
            tempFaces.Write(a);
            tempFaces.Write(b);
            tempFaces.Write(c);

            faceCount++;
        }

        /// <summary>
        /// Connects the last vCount written vertices with a face
        /// </summary>
        /// <param name="vCount">the amount of last written vertices to connect</param>
        public void WriteFace(byte vCount)
        {
            tempFaces.Write((byte)vCount);
            for (int i = 0; i < vCount; i++)
            {
                tempFaces.Write(vertexCount - (i+1));
            }

            faceCount++;
        }

        public void WriteTriangle(float3 a, float3 b, float3 c)
        {
            WriteVertex(a);
            WriteVertex(b);
            WriteVertex(c);
            WriteFace(vertexCount-1, vertexCount-2, vertexCount-3);
        }

        public void WritePolygon(List<float3> arr)
        {
            for (int i = 0; i < arr.Count; i++)
            {
                WriteVertex(arr[i]);
            }

            WriteFace((byte) arr.Count);
        }

        public void Dispose()
        {
            var outFile = new BinaryWriter(new FileStream(filePath + ".ply", FileMode.Create));
            WriteHeader(outFile);

            tempVerts.Seek(0, SeekOrigin.Begin);
            tempVerts.BaseStream.CopyTo(outFile.BaseStream);

            tempFaces.Seek(0, SeekOrigin.Begin);
            tempFaces.BaseStream.CopyTo(outFile.BaseStream);

            tempFaces.Close();
            tempVerts.Close();

            File.Delete(filePath + "temp.verts");
            File.Delete(filePath + "temp.faces");

            outFile.Close();
        }
        

        private byte[] ToByteArray(string theString)
        {
            return Encoding.ASCII.GetBytes(theString);
        }

        
    }
}