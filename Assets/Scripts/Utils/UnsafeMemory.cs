using Assets.Scripts.Octree;
using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Assets.Scripts
{
    public class UnsafeMemory
    {
        [DllImport("kernel32.dll", EntryPoint = "CopyMemory", SetLastError = false)]
        public static extern void CopyMemory(IntPtr destination, IntPtr source, long length);

        [DllImport("kernel32.dll", EntryPoint = "MoveMemory", SetLastError = false)]
        public static extern void MoveMemory(IntPtr destination, IntPtr source, long length);

        [DllImport("kernel32.dll", EntryPoint = "RtlFillMemory", SetLastError = false)]
        public static extern void FillMemory(IntPtr destination, uint length, byte fill);

        [DllImport("msvcrt.dll")]
        public static extern unsafe int memcmp(byte* b1, byte* b2, ulong count);
    }


    public unsafe struct Pointer<T> where T : unmanaged
    {
        public T* ptr;

        public T Data => *ptr;

        public static implicit operator ulong(Pointer<T> wrapper) => (ulong)wrapper.ptr;
        public static implicit operator Pointer<T>(ulong ptr) => new Pointer<T> { ptr = (T*)ptr };

        public static implicit operator T*(Pointer<T> wrapper) => wrapper.ptr;
        public static implicit operator Pointer<T>(T* ptr) => new Pointer<T> { ptr = ptr };
    }

}