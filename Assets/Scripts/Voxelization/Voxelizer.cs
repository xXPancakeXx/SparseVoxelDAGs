using Assets.Scripts.Entities;
using Assets.Scripts.Voxelization.Entities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Utils;
using Debug = UnityEngine.Debug;
using Mesh = UnityEngine.Mesh;

namespace Assets.Scripts.Voxelization
{
    public partial class Voxelizer : IVoxelizer
    {
		const int progressReportTriCount = 50000;

		private readonly LocalMesh mesh;
		private int maxDepth;
		private bool hasMaterialColorData;

		private List<OctNode>[] levels;
		private List<LeafOctNode> leafLevel;

		public int GridDimension => 1 << maxDepth;
		public int MaxDepth => maxDepth;
		public int NodeCount => levels.Sum(x => x.Count) + (leafLevel != null ? leafLevel.Count : 0);
		public int VoxelCount => leafLevel != null 
			? leafLevel.Sum(x => math.countbits((int) x.validmask)) 
			: levels.Last().Sum(x => math.countbits((int) x.validmask));
		public uint[] NodesPerDepth
        {
            get
            {
				var npd = levels.Select(x => (uint)x.Count);
				if (leafLevel != null) npd = npd.Append((uint)leafLevel.Count);

				return npd.ToArray();
            }
        }
		public List<LeafOctNode> LeafNodes => leafLevel;

		public bool HasMaterialColorData => hasMaterialColorData;

		public Voxelizer(Mesh mesh, int maxDepth)
		{
			this.mesh = new LocalMesh
			{
				bounds = mesh.bounds,
				name = mesh.name,
				vertices = new Vector3[mesh.vertices.Length],
				triangles = new int[mesh.triangles.Length],
				vertexColors = new Color[mesh.colors.Length],
			};

			Array.Copy(mesh.triangles, this.mesh.triangles, this.mesh.triangles.Length);
			Array.Copy(mesh.vertices, this.mesh.vertices, this.mesh.vertices.Length);
			Array.Copy(mesh.colors, this.mesh.vertexColors, this.mesh.vertexColors.Length);

			this.maxDepth = maxDepth;
		}

		//public async void VoxelizeVisualize(MeshFilter filter, MeshRenderer meshRenderer, Material vertexMat)
		//{
		//    meshRenderer.material = vertexMat;
		//    var originalMesh = filter.mesh;

		//    var bounds = mesh.bounds;
		//    double3 size = (float3)mesh.bounds.max - (float3)mesh.bounds.min;
		//    double rootSize = math.max(math.max(size.x, size.y), size.z);

		//    int nNodes = 0;
		//    levels = new List<OctNode>[maxDepth];

		//    //Init voxel levels
		//    levels[0] = new List<OctNode>() { new OctNode() };
		//    for (int i = 1; i < maxDepth; i++)
		//    {
		//        levels[i] = new List<OctNode>();
		//    }

		//    Queue<QueueItem> queue = new Queue<QueueItem>();
		//    double3[] childrenCenters = new double3[8];

		//    var triCount = mesh.triangles.Length / 3;
		//    for (int triIdx = 0; triIdx < triCount; triIdx++)
		//    {
		//        if (triIdx % 1000 == 0)
		//        {
		//            Debug.Log((triIdx / triCount * 100) + "%");
		//        }

		//        var tri = mesh.GetTriangle(triIdx);

		//        var prevTriIdx = math.max((triIdx - 1) * 3, 0);


		//        mesh.vertexColors[mesh.triangles[prevTriIdx]] = new Color(1, 1, 1, 1);
		//        mesh.vertexColors[mesh.triangles[prevTriIdx + 1]] = new Color(1, 1, 1, 1);
		//        mesh.vertexColors[mesh.triangles[prevTriIdx + 2]] = new Color(1, 1, 1, 1);

		//        mesh.vertexColors[mesh.triangles[(triIdx * 3)]] = Color.red;
		//        mesh.vertexColors[mesh.triangles[(triIdx * 3) + 1]] = Color.red;
		//        mesh.vertexColors[mesh.triangles[(triIdx * 3) + 2]] = Color.red;

		//        originalMesh.colors = mesh.vertexColors;
		//        originalMesh.UploadMeshData(false);


		//        queue.Enqueue(new QueueItem(0, 0, (float3)bounds.center));
		//        while (queue.Count > 0)
		//        {
		//            var qi = queue.Dequeue();

		//            double k = rootSize / (2 << qi.level);
		//            childrenCenters[7] = qi.center + new double3(+k, +k, +k);
		//            childrenCenters[6] = qi.center + new double3(+k, +k, -k);
		//            childrenCenters[5] = qi.center + new double3(+k, -k, +k);
		//            childrenCenters[4] = qi.center + new double3(+k, -k, -k);
		//            childrenCenters[3] = qi.center + new double3(-k, +k, +k);
		//            childrenCenters[2] = qi.center + new double3(-k, +k, -k);
		//            childrenCenters[1] = qi.center + new double3(-k, -k, +k);
		//            childrenCenters[0] = qi.center + new double3(-k, -k, -k);

		//            DebugStack.PushBox(AABB.CreateExtents((float3)qi.center, (float3)k * 2));

		//            OctNode node = levels[qi.level][qi.nodeIdx];


		//            for (int i = 7; i >= 0; --i)
		//            {
		//                if (qi.level == 5) DebugStack.WaitForUserInput(true);
		//                if (IntersectTriBox(childrenCenters[i], k, tri)) Debug.Log("Voxel!");

		//                DebugStack.PushBox(AABB.CreateExtents((float3)childrenCenters[i], (float3)k));
		//                await DebugStack.WaitForStep();
		//                DebugStack.PopBox();

		//                if (IntersectTriBox(childrenCenters[i], k, tri))
		//                {
		//                    //Debug.Log("Voxel!");

		//                    //check if it has already a child at that index => add one if it hasnt
		//                    if (!node.HasChild(i))
		//                    {
		//                        //set this childvoxel to occupied
		//                        node.SetChildBit(i);

		//                        //only add a child voxel before last level

		//                        if (qi.level < maxDepth - 1)
		//                        {
		//                            unsafe
		//                            {
		//                                //create new voxel in next level and reference to parent
		//                                node.children[i] = levels[qi.level + 1].Count;           //reference in parent
		//                                levels[qi.level + 1].Add(new OctNode());                       //create node at last element in next level
		//                            }

		//                        }

		//                        nNodes++;
		//                        levels[qi.level][qi.nodeIdx] = node;
		//                    }
		//                    //Add child to queue for next octree level if we havent reached the limit
		//                    if (qi.level < maxDepth - 1)
		//                    {
		//                        unsafe
		//                        {
		//                            queue.Enqueue(new QueueItem(node.children[i], (byte)(qi.level + 1), childrenCenters[i]));
		//                        }
		//                    }
		//                    //if we are at the last level
		//                    else
		//                    {
		//                        //if (putMaterialIdInLeaves) node.children[i] = (id_t)triMatId;

		//                        //#if 0 // outputs a debug obj of voxels as points with their colours
		//                        //						sl::color3f c;
		//                        //						_scene->getTriangleColor(iTri, c);
		//                        //						printf("v %f %f %f %f %f %f\n", childrenCenters[i][0], childrenCenters[i][1], childrenCenters[i][2], c[0], c[1], c[2]);
		//                        //#endif
		//                    }
		//                }
		//            }

		//            DebugStack.PopBox();
		//        }
		//    }
		//}

		public async void VoxelizeVisualize(MeshFilter filter, MeshRenderer meshRenderer, Material vertexMat)
		{
			meshRenderer.material = vertexMat;
			var originalMesh = filter.mesh;

			var bounds = mesh.bounds;
			double3 size = (float3)mesh.bounds.max - (float3)mesh.bounds.min;
			double rootSize = math.max(math.max(size.x, size.y), size.z);

			int nNodes = 0;

			levels = new List<OctNode>[maxDepth];

			//Init voxel levels
			levels[0] = new List<OctNode>() { new OctNode() };
			for (int i = 1; i < maxDepth; i++)
			{
				levels[i] = new List<OctNode>();
			}

			Queue<QueueItem> queue = new Queue<QueueItem>();
			var childrenCenters = new double3[8];


			var triCount = mesh.triangles.Length / 3;
			for (int triIdx = 0; triIdx < triCount; triIdx++)
			{
				if (triIdx % progressReportTriCount == 0)
				{
					Debug.Log((triIdx / (float)triCount * 100) + "%");
				}


				var tri = mesh.GetTriangle(triIdx);
				var prevTriIdx = math.max((triIdx - 1) * 3, 0);


				mesh.vertexColors[mesh.triangles[prevTriIdx]] = new Color(1, 1, 1, 1);
				mesh.vertexColors[mesh.triangles[prevTriIdx + 1]] = new Color(1, 1, 1, 1);
				mesh.vertexColors[mesh.triangles[prevTriIdx + 2]] = new Color(1, 1, 1, 1);

				mesh.vertexColors[mesh.triangles[(triIdx * 3)]] = Color.red;
				mesh.vertexColors[mesh.triangles[(triIdx * 3) + 1]] = Color.red;
				mesh.vertexColors[mesh.triangles[(triIdx * 3) + 2]] = Color.red;

				originalMesh.colors = mesh.vertexColors;
				originalMesh.UploadMeshData(false);

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

					DebugStack.PushBox(AABB.CreateExtents((float3)qi.center, (float3)k * 2));

					OctNode node = levels[qi.level][qi.nodeIdx];
					for (int i = 7; i >= 0; --i)
					{
						if (IntersectTriBox(childrenCenters[i], k, tri)) Debug.Log("Voxel found");

						DebugStack.PushBox(AABB.CreateExtents((float3)childrenCenters[i], (float3)k));
						await DebugStack.WaitForStep();
						DebugStack.PopBox();

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
									unsafe
                                    {
										//create new voxel in next level and reference to parent
										node.children[i] = levels[qi.level + 1].Count;           //reference in parent
										levels[qi.level + 1].Add(new OctNode());                       //create node at last element in next level
									}
								}

								nNodes++;
								levels[qi.level][qi.nodeIdx] = node;
							}

							//Add child to queue for next octree level if we havent reached the limit
							if (qi.level < maxDepth - 1)
							{
								unsafe
								{
									queue.Enqueue(new QueueItem(node.children[i], (byte)(qi.level + 1), childrenCenters[i]));
								}
							}
							//if we are at the last level
							else
							{
								//DebugStack.PushBox(AABB.CreateExtents((float3)childrenCenters[i], (float3)k));
							}
						}
					}

					DebugStack.PopBox();
				}
			}

			Debug.Log("Nodes in Octree:" + nNodes);
		}

		public unsafe JobHandle Voxelize()
		{
			var sw1 = Stopwatch.StartNew();

			var bounds = mesh.bounds;
			double3 size = (float3)mesh.bounds.max - (float3)mesh.bounds.min;
			double rootSize = math.max(math.max(size.x, size.y), size.z);

			int nNodes = 0;
			int nVoxels = 0;

			levels = new List<OctNode>[maxDepth];

			//Init voxel levels
			levels[0] = new List<OctNode>() { new OctNode() };
			for (int i = 1; i < maxDepth; i++)
			{
				levels[i] = new List<OctNode>();
			}

			Queue<QueueItem> queue = new Queue<QueueItem>();
			var childrenCenters = stackalloc double3[8];


			var triCount = mesh.triangles.Length / 3;
			for (int triIdx = 0; triIdx < triCount; triIdx++)
			{
                if (triIdx % progressReportTriCount == 0)
                {
                    Debug.Log((triIdx / (float)triCount * 100) + "%");
                }


                var tri = mesh.GetTriangle(triIdx);

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

					OctNode node = levels[qi.level][qi.nodeIdx];
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
									node.children[i] = levels[qi.level + 1].Count;           //reference in parent
									levels[qi.level + 1].Add(new OctNode());                       //create node at last element in next level
								}

								nNodes++;
								levels[qi.level][qi.nodeIdx] = node;
							}
							
							//Add child to queue for next octree level if we havent reached the limit
							if (qi.level < maxDepth - 1)
							{
								queue.Enqueue(new QueueItem(node.children[i], (byte)(qi.level + 1), childrenCenters[i]));
							}
							//if we are at the last level
							else
							{
								nVoxels++;
								//DebugStack.PushBox(AABB.CreateExtents((float3)childrenCenters[i], (float3)k));
							}
						}
					}
				}
			}

			Debug.Log($"Voxels: {nVoxels} Nodes: {nNodes}");
			Debug.Log($"Time: {sw1.ElapsedMilliseconds} ms");

			return default;
		}

		public unsafe JobHandle VoxelizeColoredVertex()
        {
			hasMaterialColorData = true;

			var bounds = mesh.bounds;
			double3 size = (float3) mesh.bounds.max - (float3) mesh.bounds.min;
			double rootSize = math.max(math.max(size.x, size.y), size.z);

			int nNodes = 0;
			
			//subtract one level because we want the last level seperate, bc of colors
			levels = new List<OctNode>[maxDepth - 1];
			leafLevel = new List<LeafOctNode>();

			//Init voxel levels
			levels[0] = new List<OctNode>() { new OctNode() };
            for (int i = 1; i < maxDepth - 1; i++)
            {
				levels[i] = new List<OctNode>();
			}

			Queue<QueueItem> queue = new Queue<QueueItem>();
			var childrenCenters = stackalloc double3[8];

			var triCount = mesh.triangles.Length / 3;
			for (int triIdx = 0; triIdx < triCount; triIdx++)
			{
				if (triIdx % progressReportTriCount == 0)
				{
					Debug.Log((triIdx / (float)triCount * 100) + "%");
				}


				var tri = mesh.GetTriangle(triIdx);
				//var matId = mesh.GetSubmeshIndex(triIdx * 3);

				queue.Enqueue(new QueueItem(0, 0, (float3)bounds.center));
				while (queue.Count > 0)
				{
					var qi = queue.Dequeue();

					double k = rootSize / (2 << qi.level);
					childrenCenters[7] = qi.center + new double3(+k, +k, +k);
					childrenCenters[6] = qi.center + new double3(+k, +k, -k);
					childrenCenters[5] = qi.center + new double3(+k, -k, +k);
					childrenCenters[4] = qi.center + new double3(+k, -k, -k);
					childrenCenters[3] = qi.center + new double3(-k, +k, +k);
					childrenCenters[2] = qi.center + new double3(-k, +k, -k);
					childrenCenters[1] = qi.center + new double3(-k, -k, +k);
					childrenCenters[0] = qi.center + new double3(-k, -k, -k);

					for (int i = 7; i >= 0; --i)
					{
						if (IntersectTriBox(childrenCenters[i], k, tri))
						{
							var node = GetNodeColor(qi.level, qi.nodeIdx);
							if (node is OctNode octNode)
                            {
								if (!octNode.HasChild(i))
								{
									//set this childvoxel to occupied and reference it from parent node
									AddNodeAndReference(qi.level + 1, ref octNode, i);
									
									//write it back into array (bc its a struct)
									levels[qi.level][qi.nodeIdx] = octNode;
									nNodes++;
								}

								queue.Enqueue(new QueueItem(octNode.children[i], (byte)(qi.level + 1), childrenCenters[i]));
							}
							else if (node is LeafOctNode leafNode)
                            {
								if (leafNode.HasChild(i)) continue;

								leafNode.SetChildBit(i);

								mesh.GetVertexColors(triIdx, out Color a, out Color b, out Color c);
								var voxColor = GetVoxelColor(tri, a, b, c, (float3)childrenCenters[i]);

								leafNode.materialIndices[i] = 0;
								leafNode.colors[i] = *(int*) &voxColor;

								leafLevel[qi.nodeIdx] = leafNode;
								nNodes++;
							}
						}
					}
				}
			}

			Debug.Log("Nodes in Octree:" + nNodes);

			return default;
		}

		public JobHandle VoxelizeParallel()
		{
			throw new NotImplementedException();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public IVoxelizerOctNode GetNode(int level, int nodeIdx)
		{
			if (level < maxDepth) 
				return levels[level][nodeIdx];
			return leafLevel[nodeIdx];
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public IVoxelizerOctNode GetNodeColor(int level, int nodeIdx)
		{
			if (level < maxDepth - 1)
				return levels[level][nodeIdx];
			return leafLevel[nodeIdx];
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private unsafe void AddNodeAndReference(int level, ref OctNode parent, int childBit)
		{
			parent.SetChildBit(childBit);

			if (level < maxDepth - 1)
            {
				parent.children[childBit] = levels[level].Count;
				levels[level].Add(new OctNode());
            }
            else
            {
				parent.children[childBit] = leafLevel.Count;
				leafLevel.Add(new LeafOctNode());
            }
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
			if (min > boxHalfSide || max< -boxHalfSide) return false;

			/* test in Y-direction */
			FindMinMax(v0.y, v1.y, v2.y, out min, out max);
			if (min > boxHalfSide || max< -boxHalfSide) return false;

			/* test in Z-direction */
			FindMinMax(v0.z, v1.z, v2.z, out min, out max);
			if (min > boxHalfSide || max< -boxHalfSide) return false;

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
			for (q = 0; q <= 2; q++) {
				v = vert[q];
				if (normal[q] > 0.0f) {
					vmin[q] = -maxbox - v;
					vmax[q] = maxbox - v;
				}
				else {
					vmin[q] = maxbox - v;
					vmax[q] = -maxbox - v;
				}
			}
			if (math.dot(normal, vmin) > 0) return false;
			if (math.dot(normal, vmax) >= 0) return true;
			return false;
		}

		#endregion


		private Color32 GetVoxelColor(Tri tri, Color a, Color b, Color c, float3 voxelPos)
        {
			var barCoords = ComputeBarycentricCoords(tri, tri.Normal, voxelPos);

			var voxelColor = InterpolateBarycentric(barCoords, a.ToFloat().xyz, b.ToFloat().xyz, c.ToFloat().xyz);
			return new Color(voxelColor.x, voxelColor.y, voxelColor.z);
        }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private float3 ComputeBarycentricCoords(Tri tri, float3 normal, float3 voxelPos)
		{
			// Triangle plane coefficients A,B,C are equal to triangleNormal.
			// We must compute D coefficient first.
			// Ax + By + Cz + D = 0, where (x,y,z) is one of triangle points.
			// D = -( n.x * v0.x + n.y * v0.y + n.z * v0.z ), where n is triangleNormal. 
			float coeffD = -math.dot(tri.a, normal);

			// Our goal is to find point p such that p + k*n = (voxelX, voxelY, voxelZ).
			float k = (math.dot(voxelPos, normal) + coeffD) / (normal.x * normal.x + normal.y * normal.y + normal.z * normal.z);
			float3 point = voxelPos - k * normal;

			return ComputeBarycentricCoords(tri, point);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private float3 ComputeBarycentricCoords(Tri triangle, float3 point)
		{
			// Barycentric coordinates fulfill equation: point = [ v1; v2; v3 ] * barycentricCoords
			float3x3 vertexMat = new float3x3(triangle.a, triangle.b, triangle.c); // Initialize columns with triangle verticies.
			float3x3 inverseVertexMat = math.inverse(vertexMat);

			return math.mul(inverseVertexMat, point);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private float3 InterpolateBarycentric(float3 barycentric, float3 vert1Val, float3 vert2Val, float3 vert3Val)
		{
			return barycentric.x * vert1Val + barycentric.y * vert2Val + barycentric.z * vert3Val;
		}

    }
}