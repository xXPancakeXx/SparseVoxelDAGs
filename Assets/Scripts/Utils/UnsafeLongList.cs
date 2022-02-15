using System;
using System.Collections;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Utils
{
	/// <summary>
	/// A version of NativeList that supports large amounts of data (above 1 GB)
	/// </summary>
	/// 
	/// <typeparam name="T">
	/// Type of elements in the list. Must be blittable.
	/// </typeparam>
	///
	/// <author>
	/// Made by Artur Nasiadko, https://github.com/artnas
	/// Based on NativeCustomArray by Jackson Dunstan, http://JacksonDunstan.com/articles/4734
	/// Modified to be nestable
	/// </author>
	[NativeContainer]
	public unsafe struct UnsafeLongList<T> : IDisposable
		where T : unmanaged
	{
		// internal data
		private long count;
		private long capacity;
		private long bufferLength;
		[NativeDisableUnsafePtrRestriction] private unsafe T* buffer;

		private bool isCreated;
		private Allocator allocator;

		public UnsafeLongList(Allocator allocator) : this(64, allocator)
		{
			
		}

		public UnsafeLongList(long capacity, Allocator allocator)
		{
			isCreated = true;
			this.allocator = allocator;

			// allocate list data
			this.capacity = capacity;
			this.count = 0;

			var totalSize = UnsafeUtility.SizeOf<T>() * capacity;

			// allocate list buffer
			buffer = (T*)UnsafeUtility.Malloc(totalSize, UnsafeUtility.AlignOf<T>(), allocator);
			bufferLength = totalSize;
		}

		public long Capacity => capacity;

		public long Length => count;

		public bool IsCreated => isCreated;

		public void Clear()
		{
			count = 0;
		}

		public void Dispose()
		{
			if (!isCreated || buffer == null)
				return;

			isCreated = false;

			UnsafeUtility.Free(buffer, allocator);
		}

		public T this[long index]
		{
			get => buffer[index];
			set => buffer[index] = value;
		}

		public ref T ElementAt(long index)
        {
			return ref buffer[index];
        }

		public void Add(T value)
		{
			ResizeIfNecessary(1);

			// Write new value at the end
			buffer[count] = value;

			// Count the newly-added element
			count++;
		}

		public void AddRange(void* data, long elementsCount)
		{
			var bufferLength = elementsCount * UnsafeUtility.SizeOf<T>();

			ResizeIfNecessary(elementsCount);

			// add elements to end of array
			UnsafeUtility.MemCpy(&buffer[count], data, bufferLength);

			count += elementsCount;
		}

		private void ResizeIfNecessary(long requiredAdditionalCapacity)
		{
			if (count + requiredAdditionalCapacity < capacity)
				return;

			var sizeOfType = UnsafeUtility.SizeOf<T>();

			var newLength = bufferLength * 2;
			var newCapacity = newLength / sizeOfType;

			// Add some additional capacity
			var capacityDiff = requiredAdditionalCapacity - newCapacity;
			if (capacityDiff > 0)
			{
				newLength += capacityDiff * sizeOfType;
			}

			var newBuffer = (T*)UnsafeUtility.Malloc(newLength, UnsafeUtility.AlignOf<T>(), allocator);

			// copy existing memory
			UnsafeUtility.MemCpy(newBuffer, buffer, bufferLength);

			// free old buffer
			UnsafeUtility.Free(buffer, allocator);

			capacity = newLength / sizeOfType;
			bufferLength = newLength;
			buffer = newBuffer;
		}

		public void RemoveAt(int index)
		{
			long numElementsToShift = count - index - 1;

			if (numElementsToShift > 0)
			{
				int elementSize = UnsafeUtility.SizeOf<T>();
				byte* source = (byte*)buffer + elementSize * (index + 1);
				long shiftSize = numElementsToShift * elementSize;
				UnsafeUtility.MemMove(source - elementSize, source, shiftSize);
			}

			count--;
		}

		/// <summary>
		/// Trims unused excess memory with an option to retain some additional capacity
		/// </summary>
		/// <param name="leaveSomeAdditionalCapacity"></param>
		public void TrimExcess(bool leaveSomeAdditionalCapacity = false)
		{
			var sizeOfType = UnsafeUtility.SizeOf<T>();

			var newBufferLength = count * sizeOfType;

			// add 10% additional capacity
			if (leaveSomeAdditionalCapacity)
				newBufferLength += UnityEngine.Mathf.RoundToInt(newBufferLength * 0.1f);

			if (bufferLength > newBufferLength)
			{
				var existingBuffer = buffer;
				var newBuffer = (T*)UnsafeUtility.Malloc(newBufferLength, UnsafeUtility.AlignOf<T>(), allocator);

				// copy existing memory
				UnsafeUtility.MemCpy(newBuffer, buffer, count * sizeOfType);

				// free old buffer
				UnsafeUtility.Free(existingBuffer, allocator);

				capacity = newBufferLength / sizeOfType;
				bufferLength = newBufferLength;
				buffer = newBuffer;
			}
		}
	}

	internal struct NativeListData<T> where T : unmanaged
	{
		public long Count;
		public long Capacity;
		public long BufferLength;
		[NativeDisableUnsafePtrRestriction]
		public unsafe T* Buffer;
	}
}