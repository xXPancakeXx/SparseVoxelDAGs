using Assets.Scripts.Entities;
using Assets.Scripts.MeshPartitioning;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Ray = UnityEngine.Ray;

namespace Assets.Scripts
{
    public struct Partition
    {
        public Bounds bounds;
        public UnsafeList<Tri> triangles;

        public Partition(Bounds bounds, UnsafeList<Tri> triangles)
        {
            this.bounds = bounds;
            this.triangles = triangles;
        }
    }

    public class MeshPartitioner
    {
        private readonly LocalMesh mesh;
        private readonly int3 gridDimensions;

        public MeshPartitioner(Mesh mesh, int3 gridDimensions)
        {
            this.mesh = new LocalMesh(mesh);

            this.gridDimensions = gridDimensions;
        }

        public static void PartitionMemory(NativeLocalMesh mesh, int3 gridDimensions, out NativeArray<Partition> partitions)
        {
            var partitionCount = gridDimensions.x * gridDimensions.y * gridDimensions.z;

            partitions = new NativeArray<Partition>(partitionCount, Allocator.Persistent);

            float3 size = mesh.bounds.max - mesh.bounds.min;
            float3 unitLengths = size / gridDimensions;

            //Create partitions
            float3 partitionMin = mesh.bounds.min;
            int index = 0;
            for (int z = 0; z < gridDimensions.z; z++)
            {
                partitionMin.y = mesh.bounds.min.y;
                for (int y = 0; y < gridDimensions.y; y++)
                {
                    partitionMin.x = mesh.bounds.min.x;
                    for (int x = 0; x < gridDimensions.x; x++)
                    {
                        var bound = new Bounds()
                        {
                            min = partitionMin,
                            max = partitionMin + unitLengths
                        };
                        partitions[index] = new Partition(bound, new UnsafeList<Tri>(1, Allocator.Persistent));

                        partitionMin.x += unitLengths.x;
                        index++;
                    }
                    partitionMin.y += unitLengths.y;
                }
                partitionMin.z += unitLengths.z;
            }

            var triCount = mesh.triangles.Length / 3;

            Debug.Log("Partitioning meshes...");
            for (int i = 0; i < triCount; i++)
            {
                var tri = mesh.GetTriangle(i);

                var min = Vector3.Min(Vector3.Min(tri.a, tri.b), tri.c);
                var max = Vector3.Max(Vector3.Max(tri.a, tri.b), tri.c);
                Bounds triBounds = new Bounds { min = min, max = max };

                for (int j = 0; j < partitions.Length; j++)
                {
                    if (partitions[j].bounds.Intersects(triBounds))
                    {
                        var p = partitions[j];
                        p.triangles.Add(tri);
                        partitions[j] = p;
                    }
                }
            }
        }

        [BurstCompile]
        public struct MeshPartitionJob : IJob
        {
            public NativeLocalMesh mesh;
            public int3 gridDimensions;
            public NativeArray<Partition> partitions;

            public MeshPartitionJob(NativeLocalMesh mesh, int3 gridDimensions)
            {
                this.mesh = mesh;
                this.gridDimensions = gridDimensions;
                this.partitions = default;
            }

            public NativeArray<Partition> CreatePartitions()
            {
                var partitionCount = gridDimensions.x * gridDimensions.y * gridDimensions.z;
                partitions = new NativeArray<Partition>(partitionCount, Allocator.Persistent);
                
                float3 size = mesh.bounds.max - mesh.bounds.min;
                float3 unitLengths = size / gridDimensions;

                //Create partitions
                float3 partitionMin = mesh.bounds.min;
                int index = 0;
                for (int z = 0; z < gridDimensions.z; z++)
                {
                    partitionMin.y = mesh.bounds.min.y;
                    for (int y = 0; y < gridDimensions.y; y++)
                    {
                        partitionMin.x = mesh.bounds.min.x;
                        for (int x = 0; x < gridDimensions.x; x++)
                        {
                            var bound = new Bounds()
                            {
                                min = partitionMin,
                                max = partitionMin + unitLengths
                            };
                            partitions[index] = new Partition(bound, new UnsafeList<Tri>(1, Allocator.Persistent));

                            partitionMin.x += unitLengths.x;
                            index++;
                        }
                        partitionMin.y += unitLengths.y;
                    }
                    partitionMin.z += unitLengths.z;
                }

                return partitions;
            }

            public void Execute()
            {
                if (!this.partitions.IsCreated) throw new Exception("Partitions have to be created via CreatePartitions");

                var triCount = mesh.triangles.Length / 3;

                Debug.Log("Partitioning meshes...");
                for (int i = 0; i < triCount; i++)
                {
                    var tri = mesh.GetTriangle(i);

                    var min = Vector3.Min(Vector3.Min(tri.a, tri.b), tri.c);
                    var max = Vector3.Max(Vector3.Max(tri.a, tri.b), tri.c);
                    Bounds triBounds = new Bounds { min = min, max = max };

                    for (int j = 0; j < partitions.Length; j++)
                    {
                        if (partitions[j].bounds.Intersects(triBounds))
                        {
                            var p = partitions[j];
                            p.triangles.Add(tri);
                            partitions[j] = p;
                        }
                    }
                }
            }

            public void Execute2()
            {
                if (!this.partitions.IsCreated) throw new Exception("Partitions have to be created via CreatePartitions");

                var triCount = mesh.triangles.Length / 3;

                for (int j = 0; j < partitions.Length; j++)
                {
                    for (int i = 0; i < triCount; i++)
                    {
                        var tri = mesh.GetTriangle(i);

                        var min = Vector3.Min(Vector3.Min(tri.a, tri.b), tri.c);
                        var max = Vector3.Max(Vector3.Max(tri.a, tri.b), tri.c);
                        Bounds triBounds = new Bounds { min = min, max = max };

                        if (partitions[j].bounds.Intersects(triBounds))
                        {
                            var p = partitions[j];
                            p.triangles.Add(tri);
                            partitions[j] = p;
                        }
                    }
                   
                }
            }
        }

        [BurstCompile]
        public struct MeshPartitionJobParallelFor : IJobParallelFor
        {
            [NativeDisableParallelForRestriction, ReadOnly]
            public NativeArray<Partition> partitions;
            public NativeLocalMesh mesh;

            //[ReadOnly, NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
            //[ReadOnly, NativeDisableParallelForRestriction] public NativeArray<int> triangles;
            //[ReadOnly, NativeDisableParallelForRestriction] public NativeArray<Color> vertexColors;
            public Bounds meshBounds;

            public int3 gridDimensions;

            public MeshPartitionJobParallelFor(NativeLocalMesh mesh, int3 gridDimensions)
            {
                //this.vertices = mesh.vertices;
                //this.triangles = mesh.triangles;
                //this.vertexColors = mesh.vertexColors;
                //this.meshBounds = mesh.bounds;
                this.mesh = mesh;
                this.meshBounds = mesh.bounds;

                this.gridDimensions = gridDimensions;
                this.partitions = default;
            }

            public NativeArray<Partition> CreatePartitions()
            {
                var partitionCount = gridDimensions.x * gridDimensions.y * gridDimensions.z;
                partitions = new NativeArray<Partition>(partitionCount, Allocator.Persistent);

                float3 size = meshBounds.max - meshBounds.min;
                float3 unitLengths = size / gridDimensions;

                //Create partitions
                float3 partitionMin = meshBounds.min;
                int index = 0;
                for (int z = 0; z < gridDimensions.z; z++)
                {
                    partitionMin.y = meshBounds.min.y;
                    for (int y = 0; y < gridDimensions.y; y++)
                    {
                        partitionMin.x = meshBounds.min.x;
                        for (int x = 0; x < gridDimensions.x; x++)
                        {
                            var bound = new Bounds()
                            {
                                min = partitionMin,
                                max = partitionMin + unitLengths
                            };
                            partitions[index] = new Partition(bound, new UnsafeList<Tri>(1, Allocator.Persistent));

                            partitionMin.x += unitLengths.x;
                            index++;
                        }
                        partitionMin.y += unitLengths.y;
                    }
                    partitionMin.z += unitLengths.z;
                }

                return partitions;
            }

            //public Tri GetTriangle(int triIdx)
            //{
            //    var baseIdx = triIdx * 3;

            //    return new Tri
            //    {
            //        a = vertices[triangles[baseIdx]],
            //        b = vertices[triangles[baseIdx + 1]],
            //        c = vertices[triangles[baseIdx + 2]]
            //    };
            //}

            public void Execute(int j)
            {
                var triCount = mesh.triangles.Length / 3;

                for (int i = 0; i < triCount; i++)
                {
                    var tri = mesh.GetTriangle(i);

                    var min = Vector3.Min(Vector3.Min(tri.a, tri.b), tri.c);
                    var max = Vector3.Max(Vector3.Max(tri.a, tri.b), tri.c);
                    Bounds triBounds = new Bounds { min = min, max = max };

                    if (partitions[j].bounds.Intersects(triBounds))
                    {
                        var p = partitions[j];
                        p.triangles.Add(tri);
                        partitions[j] = p;
                    }
                }
            }
        }

        public void PartitionDisk(string outPath)
        {
            Bounds[] partitions = new Bounds[gridDimensions.x * gridDimensions.y * gridDimensions.z];
            ModelWriterPly[] wr = new ModelWriterPly[gridDimensions.x * gridDimensions.y * gridDimensions.z];
            float3 size = mesh.bounds.max - mesh.bounds.min;
            float3 unitLengths = size / gridDimensions;

            float3 partitionMin = mesh.bounds.min;
            int index = 0;
            for (int z = 0; z < gridDimensions.z; z++)
            {
                partitionMin.y = mesh.bounds.min.y;
                for (int y = 0; y < gridDimensions.y; y++)
                {
                    partitionMin.x = mesh.bounds.min.x;
                    for (int x = 0; x < gridDimensions.x; x++)
                    {
                        partitions[index] = new Bounds()
                        {
                            min = partitionMin,
                            max = partitionMin + unitLengths
                        };
                        wr[index] = new ModelWriterPly(Path.Combine(outPath, mesh.name + $"#{x}#{y}#{z}#"));

                        partitionMin.x += unitLengths.x;
                        index++;
                    }
                    partitionMin.y += unitLengths.y;
                }
                partitionMin.z += unitLengths.z;
            }

            var triCount = mesh.triangles.Length / 3;

            Debug.Log("Partitioning meshes...");
            Color defaultColor = new Color(1, 1, 1, 1);
            for (int i = 0; i < triCount; i++)
            {
                Debug.Log($"Tri: {i+1}/{triCount}");

                var tri = mesh.GetTriangle(i);
                mesh.GetVertexColors(i, out var aCol, out var bCol, out var cCol);

                if (aCol != defaultColor &&  bCol != defaultColor && cCol != defaultColor)
                {
                    Debug.Log("Vetex colored tri found: " + i);
                }

                var min = Vector3.Min(Vector3.Min(tri.a, tri.b), tri.c);
                var max = Vector3.Max(Vector3.Max(tri.a, tri.b), tri.c);
                Bounds triBounds = new Bounds { min = min, max = max };

                for (int j = 0; j < partitions.Length; j++)
                {
                    if (partitions[j].Intersects(triBounds))
                    {
                        //tri.c = math.clamp(tri.a, partitions[j].min, partitions[j].max);
                        //tri.b = math.clamp(tri.b, partitions[j].min, partitions[j].max);
                        //tri.c = math.clamp(tri.c, partitions[j].min, partitions[j].max);

                        if (IsInBounds(tri, partitions[j]))
                        {
                            wr[j].WriteTriangle(tri.a, tri.b, tri.c);
                        }
                        else
                        {
                            wr[j].WritePolygon(CalcBoundVertices(tri, partitions[j]));
                        }
                    }
                }
            }

            Debug.Log("Finalizing meshes...");
            for (int j = 0; j < partitions.Length; j++)
            {
                wr[j].Dispose();
            }
        }

        private Bounds GetSubBounds(Bounds srcBounds, int3 gridIndex)
        {
            float3 size = srcBounds.max - srcBounds.min;
            float3 subBoundsSize = size / gridDimensions;

            return new Bounds 
            { 
                min = (float3) srcBounds.min + subBoundsSize * gridIndex, 
                max = (float3) srcBounds.min + subBoundsSize * (gridIndex+1) 
            };
        }

        private List<float3> CalcBoundVertices(Tri tri, Bounds gridBounds)
        {
            var newVerts = new List<float3>();
            Ray r; float t;
            if (!gridBounds.Contains(tri.a))
            {
                r = new Ray(tri.a, tri.c - tri.a);
                if (gridBounds.IntersectRay(r, out t))
                {
                    newVerts.Add(r.GetPoint(t));
                }

                r = new Ray(tri.a, tri.b - tri.a);
                if (gridBounds.IntersectRay(r, out t))
                {
                    newVerts.Add(r.GetPoint(t));
                }
            }
            else
            {
                newVerts.Add(tri.a);
            }

            if (!gridBounds.Contains(tri.b))
            {
                r = new Ray(tri.b, tri.a - tri.b);
                if (gridBounds.IntersectRay(r, out t))
                {
                    newVerts.Add(r.GetPoint(t));
                }

                r = new Ray(tri.b, tri.c - tri.b);
                if (gridBounds.IntersectRay(r, out t))
                {
                    newVerts.Add(r.GetPoint(t));
                }
            }
            else
            {
                newVerts.Add(tri.b);
            }

            if (!gridBounds.Contains(tri.c))
            {
                r = new Ray(tri.c, tri.b - tri.c);
                if (gridBounds.IntersectRay(r, out t))
                {
                    newVerts.Add(r.GetPoint(t));
                }

                r = new Ray(tri.c, tri.a - tri.c);
                if (gridBounds.IntersectRay(r, out t))
                {
                    newVerts.Add(r.GetPoint(t));
                }
            }
            else
            {
                newVerts.Add(tri.c);
            }

            return newVerts;
        }

        private List<float3> CalcBoundVertices2(Tri tri, Bounds gridBounds)
        {
            var newVertices = new List<float3>();


            var outsideVertices = new List<int>();
            
            if (!gridBounds.Contains(tri.a))
                outsideVertices.Add(0);
            if (!gridBounds.Contains(tri.b))
                outsideVertices.Add(1);
            if (!gridBounds.Contains(tri.c))
                outsideVertices.Add(2);

            if (outsideVertices.Count == 1)
            {
                var oustideVertex = outsideVertices[0];

                var ray = new Ray(tri[oustideVertex - 1], tri[oustideVertex] - tri[oustideVertex - 1]);
                gridBounds.IntersectRay(ray, out var t1);

                newVertices.Add(tri[oustideVertex - 1]);
                newVertices.Add(ray.GetPoint(t1));

                //modulo bc we want to start from front when out of range
                ray = new Ray(tri[oustideVertex], tri[(oustideVertex + 1) % 3] - tri[oustideVertex]);
                gridBounds.IntersectRay(ray, out t1);
                newVertices.Add(ray.GetPoint(t1));


            }

            return newVertices;
        }

        private bool IsInBounds(Tri tri, Bounds b)
        {
            return b.Contains(tri.c) && b.Contains(tri.b) && b.Contains(tri.a);
        }
    }
}