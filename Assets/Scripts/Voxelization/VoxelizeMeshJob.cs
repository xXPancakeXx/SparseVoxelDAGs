using Assets.Scripts.Entities;
using Utils;
using Assets.Scripts.Voxelization.Entities;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Debug = UnityEngine.Debug;
using UnityEngine;

namespace Assets.Scripts.Voxelization
{
	[BurstCompile]
    public struct VoxelizeMeshJob : IJob
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

        public const int progressReportTriCount = 10000;

        public NativeLocalMesh mesh;
		public Bounds bounds;
        public int maxDepth;

		public UnsafeList<int> triIds;
		public UnsafeList<UnsafeLongList<OctNode>> levels;
		public NativeList<LeafLevelData> leafLevelData;

		public VoxelizeMeshJob(NativeLocalMesh mesh, UnsafeList<int> triIds, Bounds bounds, int maxDepth, VoxelTreeLevels<OctNode> voxelLevels, NativeList<LeafLevelData> leafLevelData)
        {
            this.mesh = mesh;
            this.bounds = bounds;
            this.triIds = triIds;
			this.maxDepth = maxDepth;
			this.levels = voxelLevels.levels;
			this.leafLevelData = leafLevelData;
		}

        public unsafe void Execute()
        {
            double3 size = (float3)bounds.max - (float3)bounds.min;
            double rootSize = math.max(math.max(size.x, size.y), size.z);

            int nNodes = 0;
            int nVoxels = 0;

			if (!levels.IsCreated) throw new System.Exception("Levels need to be allocated with maxDepth size");

			//Init root voxel
			levels.ElementAt(0).Add(new OctNode());

			NativeQueue<QueueItem> queue = new NativeQueue<QueueItem>(AllocatorManager.Temp);
            var childrenCenters = stackalloc double3[8];

            var triCount = mesh.triangles.Length / 3;
            for (int j = 0; j < triIds.Length; j++)
            {
                if (j % progressReportTriCount == 0)
                {
                    Debug.Log($"{(j / (float)triCount * 100)}%");
                }

                var tri = mesh.GetTriangle(triIds[j]);

                queue.Enqueue(new QueueItem(0, 0, (float3)bounds.center));
                while (queue.Count > 0)
                {
                    var qi = queue.Dequeue();

                    double k = rootSize / (2 << (qi.level + 1));
                    childrenCenters[7] = qi.center + new double3(+k, +k, +k);
                    childrenCenters[6] = qi.center + new double3(+k, +k, -k);
                    childrenCenters[5] = qi.center + new double3(+k, -k, +k);
                    childrenCenters[4] = qi.center + new double3(+k, -k, -k);
                    childrenCenters[3] = qi.center + new double3(-k, +k, +k);
                    childrenCenters[2] = qi.center + new double3(-k, +k, -k);
                    childrenCenters[1] = qi.center + new double3(-k, -k, +k);
                    childrenCenters[0] = qi.center + new double3(-k, -k, -k);

					ref var node = ref levels.ElementAt(qi.level).ElementAt(qi.nodeIdx);
                    for (int i = 7; i >= 0; --i)
                    {
                        if (IntersectTriBox(childrenCenters[i], k, tri))
                        {
                            //check if it has already a child at that index => add one if it hasnt
                            if (!node.HasChild(i))
                            {
                                //set this childvoxel to occupied
                                node.SetChildBit(i);

                                //only add a child voxel before last level
                                if (qi.level < maxDepth - 1)
                                {
                                    //create new voxel in next level and reference to parent
                                    node.children[i] = (int) levels[qi.level + 1].Length;           //reference in parent
									levels.ElementAt(qi.level + 1).Add(new OctNode());              //create node at last element in next level
								}
								else if (leafLevelData.IsCreated)
                                {
									leafLevelData.Add(new LeafLevelData());
								}

                                nNodes++;
                            }

							//Add child to queue for next octree level if we havent reached the limit
							if (qi.level < maxDepth - 1)
                            {
                                queue.Enqueue(new QueueItem(node.children[i], (byte)(qi.level + 1), childrenCenters[i]));
                            }
                            //if we are at the last level
                            else 
                            {
								if (leafLevelData.IsCreated)
                                {
									leafLevelData.ElementAt(node.children[i]).center = childrenCenters[i];
									leafLevelData.ElementAt(node.children[i]).triIds.Add(triIds[j]);
								}

								nVoxels++;
                            }
                        }
                    }
                }
            }

            Debug.Log($"Voxels: {nVoxels} Nodes: {nNodes}");

			queue.Dispose();
		}


		#region Intersection Box <-> Triangle
		private unsafe bool IntersectTriBox(double3 boxCenter, double boxHalfSide, Tri tri)
		{
			/*    Use separating axis theorem to test overlap between triangle and box */
			/*    need to test for overlap in these directions: */
			/*    1) the {x,y,z}-directions (actually, since we use the AABB of the triangle */
			/*       we do not even need to test these) */
			/*    2) normal of the triangle */
			/*    3) crossproduct(edge from tri, {x,y,z}-directin) */
			/*       this gives 3x3=9 more tests */

			double3 v0, v1, v2;
			double min, max, p0, p1, p2, rad, fex, fey, fez;
			double3 normal, e0, e1, e2;

			/* This is the fastest branch on Sun */
			/* move everything so that the boxcenter is in (0,0,0) */
			v0 = tri[0] - boxCenter;
			v1 = tri[1] - boxCenter;
			v2 = tri[2] - boxCenter;

			/* compute triangle edges */
			e0 = v1 - v0;      /* tri edge 0 */
			e1 = v2 - v1;      /* tri edge 1 */
			e2 = v0 - v2;      /* tri edge 2 */

			/* Bullet 3:  */
			/*  test the 9 tests first (this was faster) */

			fex = math.abs(e0.x);
			fey = math.abs(e0.y);
			fez = math.abs(e0.z);
			//AXISTEST_X01(e0[Z], e0[Y], fez, fey);
			p0 = e0.z * v0.y - e0.y * v0.z;
			p2 = e0.z * v2.y - e0.y * v2.z;
			if (p0 < p2) { min = p0; max = p2; } else { min = p2; max = p0; }
			rad = (fez + fey) * boxHalfSide;
			if (min > rad || max < -rad) return false;

			//AXISTEST_Y02(e0.z, e0.x, fez, fex);
			p0 = -e0.z * v0.x + e0.x * v0.z;
			p2 = -e0.z * v2.x + e0.x * v2.z;
			if (p0 < p2) { min = p0; max = p2; } else { min = p2; max = p0; }
			rad = (fez + fex) * boxHalfSide;
			if (min > rad || max < -rad) return false;

			//AXISTEST_Z12(e0.y, e0.x, fey, fex);
			p1 = e0.y * v1.x - e0.x * v1.y;
			p2 = e0.y * v2.x - e0.x * v2.y;
			if (p2 < p1) { min = p2; max = p1; } else { min = p1; max = p2; }
			rad = (fey + fex) * boxHalfSide;
			if (min > rad || max < -rad) return false;


			fex = math.abs(e1.x);
			fey = math.abs(e1.y);
			fez = math.abs(e1.z);
			//AXISTEST_X01(e1.z, e1.y, fez, fey);
			p0 = e1.z * v0.y - e1.y * v0.z;
			p2 = e1.z * v2.y - e1.y * v2.z;
			if (p0 < p2) { min = p0; max = p2; } else { min = p2; max = p0; }
			rad = (fez + fey) * boxHalfSide;
			if (min > rad || max < -rad) return false;

			//AXISTEST_Y02(e1.z, e1.x, fez, fex);
			p0 = -e1.z * v0.x + e1.x * v0.z;
			p2 = -e1.z * v2.x + e1.x * v2.z;
			if (p0 < p2) { min = p0; max = p2; } else { min = p2; max = p0; }
			rad = (fez + fex) * boxHalfSide;
			if (min > rad || max < -rad) return false;

			//AXISTEST_Z0(e1.y, e1.x, fey, fex);
			p0 = e1.y * v0.x - e1.x * v0.y;
			p1 = e1.y * v1.x - e1.x * v1.y;
			if (p0 < p1) { min = p0; max = p1; } else { min = p1; max = p0; }
			rad = (fey + fex) * boxHalfSide;
			if (min > rad || max < -rad) return false;


			fex = math.abs(e2.x);
			fey = math.abs(e2.y);
			fez = math.abs(e2.z);
			//AXISTEST_X2(e2.z, e2.y, fez, fey);
			p0 = e2.z * v0.y - e2.y * v0.z;
			p1 = e2.z * v1.y - e2.y * v1.z;
			if (p0 < p1) { min = p0; max = p1; } else { min = p1; max = p0; }
			rad = (fez + fey) * boxHalfSide;
			if (min > rad || max < -rad) return false;

			//AXISTEST_Y1(e2.z, e2.x, fez, fex);
			p0 = -e2.z * v0.x + e2.x * v0.z;
			p1 = -e2.z * v1.x + e2.x * v1.z;
			if (p0 < p1) { min = p0; max = p1; } else { min = p1; max = p0; }
			rad = (fez + fex) * boxHalfSide;
			if (min > rad || max < -rad) return false;

			//AXISTEST_Z12(e2.y, e2.x, fey, fex);
			p1 = e2.y * v1.x - e2.x * v1.y;
			p2 = e2.y * v2.x - e2.x * v2.y;
			if (p2 < p1) { min = p2; max = p1; } else { min = p1; max = p2; }
			rad = (fey + fex) * boxHalfSide;
			if (min > rad || max < -rad) return false;

			/* Bullet 1: */
			/*  first test overlap in the {x,y,z}-directions */
			/*  find min, max of the triangle each direction, and test for overlap in */
			/*  that direction -- this is equivalent to testing a minimal AABB around */
			/*  the triangle against the AABB */

			/* test in X-direction */
			FindMinMax(v0.x, v1.x, v2.x, out min, out max);
			if (min > boxHalfSide || max < -boxHalfSide) return false;

			/* test in Y-direction */
			FindMinMax(v0.y, v1.y, v2.y, out min, out max);
			if (min > boxHalfSide || max < -boxHalfSide) return false;

			/* test in Z-direction */
			FindMinMax(v0.z, v1.z, v2.z, out min, out max);
			if (min > boxHalfSide || max < -boxHalfSide) return false;

			/* Bullet 2: */
			/*  test if the box intersects the plane of the triangle */
			/*  compute plane equation of triangle: normal*x+d=0 */
			normal = math.cross(e0, e1);
			//normal = e0.cross(e1);
			if (!PlaneBoxOverlap(normal, v0, boxHalfSide)) return false;

			return true;   /* box and triangle overlaps */
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void FindMinMax(double a, double b, double c, out double min, out double max)
		{
			min = math.min(math.min(a, b), c);
			max = math.max(math.max(a, b), c);
		}
		public bool PlaneBoxOverlap(double3 normal, double3 vert, double maxbox)
		{
			int q;
			double3 vmin = 0, vmax = 0;
			double v;
			for (q = 0; q <= 2; q++)
			{
				v = vert[q];
				if (normal[q] > 0.0f)
				{
					vmin[q] = -maxbox - v;
					vmax[q] = maxbox - v;
				}
				else
				{
					vmin[q] = maxbox - v;
					vmax[q] = -maxbox - v;
				}
			}
			if (math.dot(normal, vmin) > 0) return false;
			if (math.dot(normal, vmax) >= 0) return true;
			return false;
		}

		#endregion

	}
}