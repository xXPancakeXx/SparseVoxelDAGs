using Utils;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Voxelization.Entities
{
	public interface IVoxelizerOctNode
	{ 
		
	}


	public struct VoxelTreeLevels<T> : IDisposable where T : unmanaged, IVoxelizerOctNode
	{
		public UnsafeList<UnsafeLongList<T>> levels;

        public VoxelTreeLevels(int maxDepth)
        {
			this.levels = new UnsafeList<UnsafeLongList<T>>(maxDepth, Allocator.Persistent);
			for (int i = 0; i < maxDepth; i++)
			{
				levels.Add(new UnsafeLongList<T>(1, Allocator.Persistent));
			}
		}

		public void Dispose()
		{
			for (int i = 1; i < levels.Length; i++)
			{
				levels[i].Dispose();
			}
			levels.Dispose();
		}
	}

	public unsafe struct OctNode : IVoxelizerOctNode
	{
		public fixed int children[8];
		public byte validmask;

		//public OctNode(bool x)
		//{
		//	children = new int[8];
		//	validmask = 0;
		//}

		public bool HasChild(int childBit)
		{
			return (validmask & (1 << childBit)) != 0;
		}

		public void SetChildBit(int childBit)
		{
			validmask |= (byte)(1 << childBit);
		}

		public int ChildrenCount()
		{
			return math.countbits((int)validmask);
		}
	}

	public unsafe struct ColorOctNode : IVoxelizerOctNode
	{
		public fixed int children[8];
		public fixed int colors[8];
		public byte validmask;

		public bool HasChild(int childBit)
		{
			return (validmask & (1 << childBit)) != 0;
		}

		public void SetChildBit(int childBit)
		{
			validmask |= (byte)(1 << childBit);
		}

		public int ChildrenCount()
		{
			return math.countbits((int)validmask);
		}
	}

	public unsafe struct LeafOctNode : IVoxelizerOctNode
	{
		public fixed int colors[8];
		public fixed byte materialIndices[8];
		public byte validmask;

		public Color32 GetColor(int childBit)
        {
			return *(Color32*) colors[childBit];
        }

		public byte GetMaterialIndex(int childBit)
        {
			return materialIndices[childBit];
		}

		public bool HasChild(int childBit)
		{
			return (validmask & (1 << childBit)) != 0;
		}

		public void SetChildBit(int childBit)
		{
			validmask |= (byte)(1 << childBit);
		}

		public int ChildrenCount()
		{
			return math.countbits((int)validmask);
		}
	}

	public unsafe struct LeafLevelData 
	{
		public double3 center;
		public UnsafeList<int> triIds;
	}
}

namespace Assets.Scripts.Voxelization
{
	public partial class Voxelizer
	{
		private struct QueueItem
		{
			public int nodeIdx;
			public byte level;
			public double3 center;

			public QueueItem(int nodeIdx, byte level, double3 center)
			{
				this.nodeIdx = nodeIdx;
				this.level = level;
				this.center = center;
			}
		}
		private unsafe struct Data
		{
			public Queue<QueueItem> queue;
			public double3[] childrenCenters;
		}
	}

	public partial class BurstVoxelizer
	{
		private struct QueueItem
		{
			public int nodeIdx;
			public byte level;
			public double3 center;

			public QueueItem(int nodeIdx, byte level, double3 center)
			{
				this.nodeIdx = nodeIdx;
				this.level = level;
				this.center = center;
			}
		}
	}
}
