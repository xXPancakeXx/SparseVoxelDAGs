using Assets.Scripts.Dag;
using System;
using System.Runtime.InteropServices;
using static Assets.Scripts.Octree.SvoInfo;

namespace Assets.Scripts
{

    public abstract unsafe class RenderData : IDisposable
    {
        public int maxDepth;

        public void* dataPtr;
        public uint dataByteSize;

        public void* colorPtr;
        public uint colorByteSize;

        public RenderData(void* dataPtr, uint dataByteSize, int maxDepth)
        {
            this.dataPtr = dataPtr;
            this.dataByteSize = dataByteSize;
            this.maxDepth = maxDepth;
        }

        public RenderData(void* dataPtr, uint dataByteSize, int maxDepth, void* colorPtr, uint colorByteSize)
        {
            this.dataPtr = dataPtr;
            this.dataByteSize = dataByteSize;
            this.colorPtr = colorPtr;
            this.colorByteSize = colorByteSize;
            this.maxDepth = maxDepth;
        }

        public void Dispose()
        {
            unsafe
            {
                Marshal.FreeHGlobal(new IntPtr(dataPtr));
                dataPtr = null;

                Marshal.FreeHGlobal(new IntPtr(colorPtr));
                colorPtr = null;
            }
        }
    }

    public unsafe class SvoRenderData : RenderData
    {
        public SvoFormat format;

        public SvoRenderData(SvoFormat svoFormat, void* dataPtr, uint dataByteSize, int maxDepth) : base(dataPtr, dataByteSize, maxDepth)
        {
            this.format = svoFormat;
        }

        public SvoRenderData(SvoFormat svoFormat, void* dataPtr, uint dataByteSize, int maxDepth, void* colorPtr, uint colorByteSize) : base(dataPtr, dataByteSize, maxDepth, colorPtr, colorByteSize)
        {
            this.format = svoFormat;
        }
    }

    public unsafe class DagRenderData : RenderData
    {
        public DagFormat format;

        public DagRenderData(DagFormat dagFormat, void* dataPtr, uint dataByteSize, int maxDepth) : base(dataPtr, dataByteSize, maxDepth)
        {
            this.format = dagFormat;
        }

        public DagRenderData(DagFormat dagFormat, void* dataPtr, uint dataByteSize, int maxDepth, void* colorPtr, uint colorByteSize) : base(dataPtr, dataByteSize, maxDepth, colorPtr, colorByteSize)
        {
            this.format = dagFormat;
        }
    }
}