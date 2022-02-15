using Assets.Scripts.Octree;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using Utils;

namespace Assets.Scripts.Entities
{

    public struct DepthData
    {
        public int offset;
        public float maxT;
    };

    public struct TraceResult
    {
        public float t;
        public float3 voxelPos;
        public float3 hitPos;
    };

    public struct TraceResultColor
    {
        public float t;
        public float minT;
        public float maxT;
        public float3 bT;
        public float3 dT;
        public float3 voxelPos;
        public float3 hitPos;
        public int colorIndex;
    };

    public struct Ray
    {
        public float3 origin;
        public float3 direction;

        public Ray(float3 origin, float3 direction)
        {
            this.origin = origin;
            this.direction = direction;
        }
    }

    public struct RayHit
    {
        public float3 hitPos;
        public float distance;
        public float3 normal;

        public RayHit(float3 position, float distance, float3 normal)
        {
            this.hitPos = position;
            this.distance = distance;
            this.normal = normal;
        }

        public static RayHit NoHit => new RayHit(float3.zero, math.INFINITY, float3.zero);
    }


    public unsafe struct AABB
    {
        public float3 min;
        public float3 max;

        public unsafe AABB(float3 minBounds, float3 maxBounds)
        {
            this.min = minBounds;
            this.max = maxBounds;
        }

        public static AABB CreateExtents(float3 center, float3 halfExtents)
        {
            return new AABB(center - halfExtents, center + halfExtents);
        }


        public unsafe float3 this[int i]
        {
            get
            {
                return i == 0 ? this.min : this.max;

            }
            set
            {
                if (i == 0) this.min = value;
                else if (i == 1) this.max = value;
            }
        }

        public bool Intersects(Bounds bounds)
        {
            return min.x <= bounds.max.x && max.x >= bounds.min.x && min.y <= bounds.max.y && max.y >= bounds.min.y && min.z <= bounds.max.z && max.z >= bounds.min.z;
        }

        public bool Intersects(Ray ray, out float t)
        {
            t = math.INFINITY;

            var invDir = 1.0f / ray.direction;
            var sign = new int3(invDir < 0.0f);

            var boundsMin = new float3(this[sign.x].x, this[sign.y].y, this[sign.z].z);
            var boundsMax = new float3(this[1 - sign.x].x, this[1 - sign.y].y, this[1 - sign.z].z);

            var tmin = (boundsMin - ray.origin) * invDir;
            var tmax = (boundsMax - ray.origin) * invDir;

            if ((tmin.x > tmax.y) || (tmin.y > tmax.x))
                return false;
            if (tmin.y > tmin.x)
                tmin.x = tmin.y;
            if (tmax.y < tmax.x)
                tmax.x = tmax.y;

            if ((tmin.x > tmax.z) || (tmin.z > tmax.x))
                return false;
            if (tmin.z > tmin.x)
                tmin.x = tmin.z;
            if (tmax.z < tmax.x)
                tmax.x = tmax.z;

            t = tmin.x;
            return true;
        }
    }

    public struct Plane
    {
        public float3 normal;
        public float position;

        public Plane(float3 normal, float position)
        {
            this.normal = normal;
            this.position = position;
        }
    }


    public unsafe struct DepthDataSVO
    {
        public SvoNodeLaine copiedNode;
        public SvoNodeLaine* ptr;
        public AABB bounds;
        public float3 distances;
        public int octantId;
        public Ray ray;

        public DepthDataSVO(SvoNodeLaine* ptr, AABB bounds, float3 distances, int octantId, Ray ray)
        {
            this.ptr = ptr;
            this.copiedNode = *ptr;
            this.bounds = bounds;
            this.distances = distances;
            this.octantId = octantId;
            this.ray = ray;
        }
    }


    public unsafe struct DepthDataDAG
    {
        public int copiedNode;
        public int ptr;
        public AABB bounds;
        public float3 distances;
        public int octantId;
        public Ray ray;

        public DepthDataDAG(int ptr, int copiedNode, AABB bounds, float3 distances, int octantId, Ray ray)
        {
            this.ptr = ptr;
            this.copiedNode = copiedNode;
            this.bounds = bounds;
            this.distances = distances;
            this.octantId = octantId;
            this.ray = ray;
        }
    }

    public unsafe struct DepthDataDAGColor
    {
        internal uint offset;
        internal float maxT;
        internal uint attributeIndex;
    }

    public unsafe struct DepthDataDAGOpt
    {
        public int copiedNode;
        public int ptr;
        public float tmin;
        public float tmax;
        public float t;

        public float3 distances;
        public int octantId;
        public Ray ray;
    }


    public unsafe struct CastStack
    {
        public const uint MAX_DEPTH = 16;

        public byte index;
        public DepthDataSVO[] ptrs;

        public bool IsInRootDepth => index == 0;
        public bool IsInMaxDepth => index == MAX_DEPTH - 1;


        public CastStack(bool i)
        {
            index = 0;
            ptrs = new DepthDataSVO[MAX_DEPTH];
        }

        public void Push(DepthDataSVO depthData)
        {
            ptrs[index++] = depthData;
        }

        public DepthDataSVO Pop()
        {
            return ptrs[--index];
        }
    }

    public struct Tri
    {
        public float3 a;
        public float3 b;
        public float3 c;

        public float3 Normal => math.cross(a - c, b - c);

        public unsafe float3 this[int i]
        {
            get
            {
                fixed (float3* ptr = &a)
                {
                    return ptr[i];
                }
            }
            set
            {
                fixed (float3* ptr = &a)
                {
                    ptr[i] = value;
                }
            }
        }
    }

    public struct Color3
    {
        public Color32 a;
        public Color32 b;
        public Color32 c;

        public unsafe Color32 this[int i]
        {
            get
            {
                fixed (Color32* ptr = &a)
                {
                    return ptr[i];
                }
            }
            set
            {
                fixed (Color32* ptr = &a)
                {
                    ptr[i] = value;
                }
            }
        }
    }

    public struct LocalMesh
    {
        public struct SubMesh
        {
            public int vertexStartIndex;
            public int vertexCount;
        }

        public Vector3[] vertices;
        public int[] triangles;
        public Color[] vertexColors;
        //public SubMesh[] submeshes;

        public string name;
        public Bounds bounds;

        public LocalMesh(UnityEngine.Mesh mesh)
        {
            bounds = mesh.bounds;
            name = mesh.name;
            vertices = new Vector3[mesh.vertices.Length];
            triangles = new int[mesh.triangles.Length];
            vertexColors = new Color[mesh.colors.Length];

            Array.Copy(mesh.triangles, triangles, triangles.Length);
            Array.Copy(mesh.vertices, vertices, vertices.Length);
            Array.Copy(mesh.colors, vertexColors, vertexColors.Length);
        }

        public Tri GetTriangle(int triIdx)
        {
            var baseIdx = triIdx * 3;

            return new Tri
            {
                a = vertices[triangles[baseIdx]],
                b = vertices[triangles[baseIdx + 1]],
                c = vertices[triangles[baseIdx + 2]]
            };
        }

        public void GetVertexColors(int triIdx, out Color a, out Color b, out Color c)
        {
            var baseIdx = triIdx * 3;

            a = vertexColors[triangles[baseIdx]];
            b = vertexColors[triangles[baseIdx + 1]];
            c = vertexColors[triangles[baseIdx + 2]];
        }
    }

    public struct NativeLocalTexture : IDisposable
    {
        UnsafeList<Color32> pixels;
        int width, height;
        public bool IsCreated => pixels.IsCreated;

        public NativeLocalTexture(Texture2D tex)
        {
            this.width = tex.width;
            this.height = tex.height;

            var px = tex.GetPixels();
            pixels = new UnsafeList<Color32>(width * height, Allocator.Persistent);
            for (int i = 0; i < px.Length; i++)
            {
                pixels.Add(new Color(px[i].r, px[i].g, px[i].b, 1.0f));
            }
        }

        public Color32 GetPixelBilinear(float u, float v)
        {
            int xMin, xMax, yMin, yMax;
            float xfloat, yfloat;
            Color c, h1, h2;

            xfloat = (width - 1) * math.frac(u);
            yfloat = (height - 1) * math.frac(v);

            xMin = (int)math.floor(xfloat);
            xMax = (int)math.ceil(xfloat);

            yMin = (int)math.floor(yfloat);
            yMax = (int)math.ceil(yfloat);

            h1 = Color.Lerp(GetPixel(xMin, yMin), GetPixel(xMax, yMin), math.frac(xfloat));
            h2 = Color.Lerp(GetPixel(xMin, yMax), GetPixel(xMax, yMax), math.frac(xfloat));
            c = Color.Lerp(h1, h2, math.frac(yfloat));
            return c;
        }

        public Color32 GetPixel(int x, int y)
        {
            x = math.clamp(x, 0, width - 1);
            y = math.clamp(y, 0, height - 1);
            return pixels[x + y * width];
        }

        public void Dispose()
        {
            pixels.Dispose();
        }
    }

    public struct NativeLocalMaterial : IDisposable
    {
        public NativeLocalTexture mainTex;
        public Color32 albedo;

        public NativeLocalMaterial(Material mat)
        {
            var uTex = mat.mainTexture as Texture2D;
            if (uTex != null) mainTex = new NativeLocalTexture(uTex);
            else mainTex = default;

            albedo = mat.color;
        }

        public Color32 Sample(float2 uv)
        {
            if (mainTex.IsCreated) return mainTex.GetPixelBilinear(uv.x, uv.y);
            return albedo;
        }

        public void Dispose()
        {
            mainTex.Dispose();
        }
    }

    public struct NativeLocalMesh : IDisposable
    {
        [ReadOnly, NativeDisableParallelForRestriction] public NativeArray<Vector3> vertices;
        [ReadOnly, NativeDisableParallelForRestriction] public NativeArray<int> triangles;
        [ReadOnly, NativeDisableParallelForRestriction] public NativeArray<Color> vertexColors;
        [ReadOnly, NativeDisableParallelForRestriction] public NativeArray<Vector2> uvs;
        [ReadOnly, NativeDisableParallelForRestriction] public NativeArray<NativeLocalMaterial> materials;
        [ReadOnly, NativeDisableParallelForRestriction] public NativeArray<short> vertexMaterialMap;

        public Bounds bounds;
        public bool UsesTextures => vertexColors.Length == 1;
        public bool UsesTextureCoordinates => uvs.Length > 1;

        public NativeLocalMesh(UnityEngine.Mesh mesh)
        {
            bounds = mesh.bounds;

            vertices = new NativeArray<Vector3>(mesh.vertices.Length, Allocator.Persistent);
            triangles = new NativeArray<int>(mesh.triangles.Length, Allocator.Persistent);
            uvs = new NativeArray<Vector2>(1, Allocator.Persistent);
            materials = new NativeArray<NativeLocalMaterial>(1, Allocator.Persistent);
            vertexMaterialMap = new NativeArray<short>(1, Allocator.Persistent);

            vertices.CopyFrom(mesh.vertices);
            triangles.CopyFrom(mesh.triangles);

            vertexColors = new NativeArray<Color>(mesh.colors.Length, Allocator.Persistent);
            vertexColors.CopyFrom(mesh.colors);
        }

        public NativeLocalMesh(UnityEngine.Mesh mesh, Material[] materials)
        {
            if (materials.Length > short.MaxValue) throw new Exception("Not more than 2^17 materials allowed");
            if (materials == null || materials.Length != mesh.subMeshCount) throw new Exception("Mesh must contain materials and as many as there are submeshes");

            bounds = mesh.bounds;

            vertices = new NativeArray<Vector3>(mesh.vertices.Length, Allocator.Persistent);
            triangles = new NativeArray<int>(mesh.triangles.Length, Allocator.Persistent);
            uvs = new NativeArray<Vector2>(mesh.uv.Length, Allocator.Persistent);
            this.materials = new NativeArray<NativeLocalMaterial>(materials.Length, Allocator.Persistent);
            vertexColors = new NativeArray<Color>(1, Allocator.Persistent);

            vertices.CopyFrom(mesh.vertices);
            triangles.CopyFrom(mesh.triangles);
            uvs.CopyFrom(mesh.uv);

            float2 max = new float2(float.MinValue, float.MinValue);
            for (int i = 0; i < uvs.Length; i++)
            {
                max = math.max(max, uvs[i]);
            }
            
            
            
            float2 min = new float2(float.MaxValue, float.MaxValue);
            for (int i = 0; i < uvs.Length; i++)
            {
                min = math.min(max, uvs[i]);
            }
            Debug.Log($"min: {min} - max: {max}");
            
            vertexMaterialMap = new NativeArray<short>(triangles.Length, Allocator.Persistent);
            
            for (short i = 0; i < materials.Length; i++)
            {
                var subMesh = mesh.GetSubMesh(i);
                this.materials[i] = new NativeLocalMaterial(materials[i]);

                for (int j = subMesh.firstVertex; j < subMesh.firstVertex + subMesh.vertexCount; j++)
                {
                    vertexMaterialMap[j] = i;
                }
            }
        }

        public Tri GetTriangle(int triIdx)
        {
            var baseIdx = triIdx * 3;

            return new Tri
            {
                a = vertices[triangles[baseIdx]],
                b = vertices[triangles[baseIdx + 1]],
                c = vertices[triangles[baseIdx + 2]]
            };
        }

        public NativeLocalMaterial GetMaterial(int triIdx)
        {
            return materials[vertexMaterialMap[triangles[triIdx * 3]]];
        }

        public Color32 GetColor(int triIdx, Tri tri, float3 pos)
        {
            if (UsesTextures)
            {
                var mat = GetMaterial(triIdx);
                if (UsesTextureCoordinates)
                {
                    GetTriangleUVs(triIdx, out var a, out var b, out var c);

                    return GetVoxelTextureColor(tri, mat, a, b, c, pos);
                }

                return mat.albedo;
            }
            else
            {
                GetVertexColors(triIdx, out var a, out var b, out var c);
                return GetVoxelColor(tri, a, b, c, pos);
            }
        }

        public void GetVertexColors(int triIdx, out Color a, out Color b, out Color c)
        {
            var baseIdx = triIdx * 3;

            a = vertexColors[triangles[baseIdx]];
            b = vertexColors[triangles[baseIdx + 1]];
            c = vertexColors[triangles[baseIdx + 2]];
        }

        public void GetTriangleUVs(int triIdx, out float2 a, out float2 b, out float2 c)
        {
            var baseIdx = triIdx * 3;

            a = uvs[triangles[baseIdx]];
            b = uvs[triangles[baseIdx + 1]];
            c = uvs[triangles[baseIdx + 2]];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Color32 GetVoxelColor(Tri tri, Color a, Color b, Color c, float3 voxelPos)
        {
            var barCoords = ComputeBarycentricCoords(tri, tri.Normal, voxelPos);

            var voxelColor = InterpolateBarycentric(barCoords, a.ToFloat().xyz, b.ToFloat().xyz, c.ToFloat().xyz);
            return new Color(voxelColor.x, voxelColor.y, voxelColor.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Color32 GetVoxelTextureColor(Tri tri, NativeLocalMaterial mat, float2 a, float2 b, float2 c, float3 voxelPos)
        {
            var barCoords = ComputeBarycentricCoords(tri, tri.Normal, voxelPos);

            var uv = InterpolateBarycentric(barCoords, a, b, c);
            return mat.Sample(uv);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float2 InterpolateBarycentric(float3 barycentric, float2 vert1Val, float2 vert2Val, float2 vert3Val)
        {
            return barycentric.x * vert1Val + barycentric.y * vert2Val + barycentric.z * vert3Val;
        }


        public void Dispose()
        {
            vertexColors.Dispose();
            vertices.Dispose();
            triangles.Dispose();
            uvs.Dispose();
            vertexMaterialMap.Dispose();

            for (int i = 0; i < materials.Length; i++)
            {
                materials[i].Dispose();
            }            
            materials.Dispose();
        }
    }
}