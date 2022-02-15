using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using Unity.Mathematics;
using UnityEngine;

namespace Utils
{
    public static class Mesh
    {
        public static UnityEngine.Mesh CreateQuad2D(float width, float height)
        {
            var mesh = new UnityEngine.Mesh();

            var vertices = new List<Vector3>();
            vertices.Add(new Vector3(0, 0, 0));
            vertices.Add(new Vector3(width, 0, 0));
            vertices.Add(new Vector3(0, 0, height));
            vertices.Add(new Vector3(width, 0, height));

            var uvs = new List<Vector2>();
            uvs.Add(new Vector2(0, 0));
            uvs.Add(new Vector2(1, 0));
            uvs.Add(new Vector3(0, 1));
            uvs.Add(new Vector3(1, 1));

            var tris = new List<int>();
            tris.Add(0);
            tris.Add(2);
            tris.Add(1);
            tris.Add(1);
            tris.Add(2);
            tris.Add(3);

            mesh.vertices = vertices.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.uv = uvs.ToArray();

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        public static UnityEngine.Mesh CreateCenteredQuad2D(float width, float height)
        {
            var mesh = new UnityEngine.Mesh();

            var vertices = new List<Vector3>();
            vertices.Add(new Vector3(-width * .5f, 0, -height * .5f)); //bottom-left
            vertices.Add(new Vector3(width * .5f, 0, -height * .5f));  //bottom-right
            vertices.Add(new Vector3(-width * .5f, 0, height * .5f));  //top-left
            vertices.Add(new Vector3(width * .5f, 0, height * .5f));   //top-right

            var uvs = new List<Vector2>();
            uvs.Add(new Vector2(0, 0));
            uvs.Add(new Vector2(1, 0));
            uvs.Add(new Vector3(0, 1));
            uvs.Add(new Vector3(1, 1));

            var tris = new List<int>();
            tris.Add(0);
            tris.Add(2);
            tris.Add(1);
            tris.Add(1);
            tris.Add(2);
            tris.Add(3);

            mesh.vertices = vertices.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.uv = uvs.ToArray();

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        public static UnityEngine.Mesh CreateBox(float size)
        {
            var mesh = new UnityEngine.Mesh();

            var vertices = new List<Vector3>();

            vertices.Add(new Vector3(0, 0, 0));
            vertices.Add(new Vector3(size, 0, 0));
            vertices.Add(new Vector3(0, 0, size));
            vertices.Add(new Vector3(size, 0, size));

            vertices.Add(new Vector3(0, size, 0));
            vertices.Add(new Vector3(size, size, 0));
            vertices.Add(new Vector3(0, size, size));
            vertices.Add(new Vector3(size, size, size));


            var uvs = new List<Vector2>();
            uvs.Add(new Vector2(0, 0));
            uvs.Add(new Vector2(1, 0));
            uvs.Add(new Vector3(0, 1));
            uvs.Add(new Vector3(1, 1));

            uvs.Add(new Vector2(1, 0));
            uvs.Add(new Vector2(0, 0));
            uvs.Add(new Vector3(1, 1));
            uvs.Add(new Vector3(0, 1));

            var tris = new List<int>();
            //bottom
            tris.Add(0);
            tris.Add(2);
            tris.Add(1);
            tris.Add(1);
            tris.Add(2);
            tris.Add(3);

            //front
            tris.Add(0);
            tris.Add(4);
            tris.Add(1);
            tris.Add(4);
            tris.Add(5);
            tris.Add(1);

            //left
            tris.Add(0);
            tris.Add(4);
            tris.Add(2);
            tris.Add(4);
            tris.Add(6);
            tris.Add(2);

            //back
            tris.Add(2);
            tris.Add(3);
            tris.Add(6);
            tris.Add(3);
            tris.Add(6);
            tris.Add(7);

            //right
            tris.Add(1);
            tris.Add(5);
            tris.Add(3);
            tris.Add(5);
            tris.Add(7);
            tris.Add(3);

            //top
            tris.Add(4);
            tris.Add(5);
            tris.Add(6);
            tris.Add(5);
            tris.Add(6);
            tris.Add(7);


            mesh.vertices = vertices.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.uv = uvs.ToArray();

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        public static UnityEngine.Mesh CreateSphere(float radius)
        {
            UnityEngine.Mesh mesh = new UnityEngine.Mesh();
            List<Vector3> vertList = new List<Vector3>();
            Dictionary<long, int> middlePointIndexCache = new Dictionary<long, int>();
            
            int recursionLevel = 3;

            // create 12 vertices of a icosahedron
            float t = (1f + Mathf.Sqrt(5f)) / 2f;

            vertList.Add(new Vector3(-1f, t, 0f).normalized * radius);
            vertList.Add(new Vector3(1f, t, 0f).normalized * radius);
            vertList.Add(new Vector3(-1f, -t, 0f).normalized * radius);
            vertList.Add(new Vector3(1f, -t, 0f).normalized * radius);

            vertList.Add(new Vector3(0f, -1f, t).normalized * radius);
            vertList.Add(new Vector3(0f, 1f, t).normalized * radius);
            vertList.Add(new Vector3(0f, -1f, -t).normalized * radius);
            vertList.Add(new Vector3(0f, 1f, -t).normalized * radius);

            vertList.Add(new Vector3(t, 0f, -1f).normalized * radius);
            vertList.Add(new Vector3(t, 0f, 1f).normalized * radius);
            vertList.Add(new Vector3(-t, 0f, -1f).normalized * radius);
            vertList.Add(new Vector3(-t, 0f, 1f).normalized * radius);


            // create 20 triangles of the icosahedron
            List<TriangleIndices> faces = new List<TriangleIndices>();

            // 5 faces around point 0
            faces.Add(new TriangleIndices(0, 11, 5));
            faces.Add(new TriangleIndices(0, 5, 1));
            faces.Add(new TriangleIndices(0, 1, 7));
            faces.Add(new TriangleIndices(0, 7, 10));
            faces.Add(new TriangleIndices(0, 10, 11));

            // 5 adjacent faces 
            faces.Add(new TriangleIndices(1, 5, 9));
            faces.Add(new TriangleIndices(5, 11, 4));
            faces.Add(new TriangleIndices(11, 10, 2));
            faces.Add(new TriangleIndices(10, 7, 6));
            faces.Add(new TriangleIndices(7, 1, 8));

            // 5 faces around point 3
            faces.Add(new TriangleIndices(3, 9, 4));
            faces.Add(new TriangleIndices(3, 4, 2));
            faces.Add(new TriangleIndices(3, 2, 6));
            faces.Add(new TriangleIndices(3, 6, 8));
            faces.Add(new TriangleIndices(3, 8, 9));

            // 5 adjacent faces 
            faces.Add(new TriangleIndices(4, 9, 5));
            faces.Add(new TriangleIndices(2, 4, 11));
            faces.Add(new TriangleIndices(6, 2, 10));
            faces.Add(new TriangleIndices(8, 6, 7));
            faces.Add(new TriangleIndices(9, 8, 1));


            // refine triangles
            for (int i = 0; i < recursionLevel; i++)
            {
                List<TriangleIndices> faces2 = new List<TriangleIndices>();
                foreach (var tri in faces)
                {
                    // replace triangle by 4 triangles
                    int a = getMiddlePoint(tri.v1, tri.v2, ref vertList, ref middlePointIndexCache, radius);
                    int b = getMiddlePoint(tri.v2, tri.v3, ref vertList, ref middlePointIndexCache, radius);
                    int c = getMiddlePoint(tri.v3, tri.v1, ref vertList, ref middlePointIndexCache, radius);

                    faces2.Add(new TriangleIndices(tri.v1, a, c));
                    faces2.Add(new TriangleIndices(tri.v2, b, a));
                    faces2.Add(new TriangleIndices(tri.v3, c, b));
                    faces2.Add(new TriangleIndices(a, b, c));
                }
                faces = faces2;
            }

            mesh.vertices = vertList.ToArray();

            List<int> triList = new List<int>();
            for (int i = 0; i < faces.Count; i++)
            {
                triList.Add(faces[i].v1);
                triList.Add(faces[i].v2);
                triList.Add(faces[i].v3);
            }

            mesh.triangles = triList.ToArray();
            mesh.uv = new Vector2[mesh.vertices.Length];

            Vector3[] normales = new Vector3[vertList.Count];
            for (int i = 0; i < normales.Length; i++)
                normales[i] = vertList[i].normalized;

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        private static int getMiddlePoint(int p1, int p2, ref List<Vector3> vertices, ref Dictionary<long, int> cache, float radius)
        {
            // first check if we have it already
            bool firstIsSmaller = p1 < p2;
            long smallerIndex = firstIsSmaller ? p1 : p2;
            long greaterIndex = firstIsSmaller ? p2 : p1;
            long key = (smallerIndex << 32) + greaterIndex;

            int ret;
            if (cache.TryGetValue(key, out ret))
            {
                return ret;
            }

            // not in cache, calculate it
            Vector3 point1 = vertices[p1];
            Vector3 point2 = vertices[p2];
            Vector3 middle = new Vector3
            (
                (point1.x + point2.x) / 2f,
                (point1.y + point2.y) / 2f,
                (point1.z + point2.z) / 2f
            );

            // add vertex makes sure point is on unit sphere
            int i = vertices.Count;
            vertices.Add(middle.normalized * radius);

            // store it, return index
            cache.Add(key, i);

            return i;
        }

        private struct TriangleIndices
        {
            public int v1;
            public int v2;
            public int v3;

            public TriangleIndices(int v1, int v2, int v3)
            {
                this.v1 = v1;
                this.v2 = v2;
                this.v3 = v3;
            }
        }
    }
}
