using System;
using System.Collections;
using System.IO;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Utils
{
    public static class Extensions
    {
        public static unsafe void ReadBytesLong(this BinaryReader br, byte* dest, long bytesToRead, int bufferSize)
        {
            var buffer = new byte[bufferSize];

            long sumBytesRead = 0;
            while (bytesToRead > 0)
            {
                long x = bufferSize;
                int y = (int)System.Math.Min(x, bytesToRead);
                int bytesRead = br.Read(buffer, 0, y);

                fixed(byte* src = buffer)
                {
                    UnsafeUtility.MemCpy(dest + sumBytesRead, src, bytesRead);
                }

                if (bytesRead != y) return;

                sumBytesRead += bytesRead;
                bytesToRead -= bytesRead;
            }
        }

        public static float4 ToFloat(this Color col)
        {
            return new float4(col.r, col.g, col.b, col.a);
        }

    }
}