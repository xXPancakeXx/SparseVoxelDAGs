using Assets.Scripts.Raytracer;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Assets.Scripts.Entities;
using Plane = Assets.Scripts.Entities.Plane;
using Ray = Assets.Scripts.Entities.Ray;
using Assets.Scripts.Voxelization;
using Assets.Scripts.Voxelization.Entities;
using Unity.Jobs;

namespace Assets.Scripts
{
    public class VoxelBoy : MonoBehaviour
    {
        [Header("Mesh Voxelization Visualization")]
        public MeshFilter mf;
        public MeshRenderer mr;
        public Material vertexMat;
        public UnityEngine.Mesh mesh;
        public int3 gridDims;
        public int subDim;
        public Texture2D tex;
        public GameObject go;
        
        private CancellationTokenSource tokenSource;

        private bool useBigCube;
        private Stopwatch sw1;
        private JobHandle handle;


#if UNITY_EDITOR

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.C)) GC.Collect();
            if (Input.GetKeyDown(KeyCode.F)) DebugStack.Step();
            if (Input.GetKeyDown(KeyCode.G)) ToggleCubeSize();

            //Debug.Log("Support RInt: " + SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RInt));

            if (sw1 != null && handle.IsCompleted)
            {
                Debug.Log($"Time: {sw1.ElapsedMilliseconds} ms");
                sw1 = null;
            }
        }

        [ContextMenu("TextureSampling test")]
        private async void TextureSampling()
        {
            var myTex = new NativeLocalTexture(tex);

            var outTex = new Texture2D(tex.width, tex.height);

            
            for (int y = 0; y < tex.height; y++)
            {
                for (int x = 0; x < tex.width; x++)
                {
                    var sampledPixel = myTex.GetPixel(x, y);
                    outTex.SetPixel(x, y, sampledPixel);
                }
            }
            
            var png = outTex.EncodeToPNG();
            File.WriteAllBytes( @"C:\Users\phste\Desktop\tex.png", png);
            
            var outRes = new int2(tex.width, tex.height);
            outTex = new Texture2D(outRes.x, outRes.y);
            
            for (int y = 0; y < outRes.y; y++)
            {
                for (int x = 0; x < outRes.x; x++)
                {
                    var uv = new float2(x / (float) outRes.x, y / (float) outRes.y) + new float2(0.5f, .5f);
                 
                    var sampledPixel = myTex.GetPixelBilinear(uv.x, uv.y);
                    outTex.SetPixel(x, y, sampledPixel);
                }
            }
            
            png = outTex.EncodeToPNG();
            File.WriteAllBytes( @"C:\Users\phste\Desktop\tex2.png", png);
           
            
            myTex.Dispose();
        }
        
        [ContextMenu("Mesh texture test")]
        private void MeshTextureTest()
        {
            var materials = go.GetComponent<MeshRenderer>()?.sharedMaterials;
            var mesh = go.GetComponent<MeshFilter>()?.sharedMesh;

            var nm = new NativeLocalMesh(mesh, materials);
            
            Debug.Log("Hey");
            
            nm.Dispose();
        }

        private void ToggleCubeSize()
        {
            DebugStack.Reset();

            if (useBigCube) DrawCubeBig();
            else DrawCube();

            useBigCube = !useBigCube;
        }

        [ContextMenu("Cancel Task")]
        private void CancelTask()
        {
            tokenSource.Cancel();
        }

        [ContextMenu("Voxelize Experimental")]
        private async void VoxelizeExp()
        {
            var vox = new BurstVoxelizer(mesh, gridDims.x);

            sw1 = Stopwatch.StartNew();
            await vox.VoxelizeSub(subDim);
        }

        [ContextMenu("Voxelize Experimental Parallel")]
        private void VoxelizeExpPar()
        {
            var vox = new BurstVoxelizer(mesh, gridDims.x);

            // UniTask.Void(() => vox.VoxelizeParallel2());

            //JobHelper.RunAsync(() => );
        }


        [ContextMenu("MeshPart")]
        private void MeshPart()
        {
            Task.Run(async () =>
            {
                var nm = new NativeLocalMesh(mesh);

                var sw1 = Stopwatch.StartNew();

                var partitionJob = new MeshPartitioner.MeshPartitionJob(nm, new int3(2, 2, 2));
                var partitions = partitionJob.CreatePartitions();

                var x = partitionJob.Schedule();

                x.Complete();
                nm.Dispose();

                Debug.Log($"Time: {sw1.ElapsedMilliseconds} ms");
            });
        }

        [ContextMenu("MeshPartParallel")]
        private void MeshPartParallel()
        {
            Task.Run(async () =>
            {
                var nm = new NativeLocalMesh(mesh);

                var sw1 = Stopwatch.StartNew();

                var partitionJob = new MeshPartitioner.MeshPartitionJobParallelFor(nm, new int3(2, 2, 2));
                var partitions = partitionJob.CreatePartitions();

                var x = partitionJob.Schedule(2*2*2, 1);

                x.Complete();
                nm.Dispose();

                Debug.Log($"Time: {sw1.ElapsedMilliseconds} ms");
            });
        }

        [ContextMenu("Voxelize Experimental Color")]
        private async void VoxelizeExpParColor()
        {
            var vox = new BurstVoxelizerColor(mesh, gridDims.x);

            sw1 = Stopwatch.StartNew();
            this.handle = vox.Voxelize();

        }

        [ContextMenu("Voxexlize")]
        public async void Voxelize()
        {
            var vox = new Voxelizer(mesh, gridDims.x);

            await Task.Run(() => vox.Voxelize());
        }

        [ContextMenu("Voxelizer Output Compare")]
        public unsafe void VoxelCompare()
        {
            var vox = new Voxelizer(mesh, gridDims.x);
            var voxParallel = new BurstVoxelizer(mesh, gridDims.x);

            for (int i = 0; i < mesh.vertices.Length; i++)
            {
                var ov = mesh.vertices[i];
                var cv = voxParallel.mesh.vertices[i];

                if (ov != cv)
                {
                    Debug.Log($"Vertex {i} not equal");
                }
            }


            vox.Voxelize();
            voxParallel.Voxelize();

            for (int depth = 0; depth < vox.MaxDepth; depth++)
            {
                for (int i = 0; i < vox.NodesPerDepth[0]; i++)
                {
                    OctNode origNode = (OctNode) vox.GetNode(depth, i);
                    OctNode paraNode = (OctNode) voxParallel.GetNode(depth, i);

                    if (!(origNode.validmask == paraNode.validmask))
                    {
                        Debug.Log($"Not equal: Depth: {depth}, idx: {i}");
                        return;
                    }

                    for (int x = 0; x < 8; x++)
                    {
                        var c = origNode.children[x];
                        var o = paraNode.children[x];

                        if (c != o)
                        {
                            Debug.Log($"Not equal: Depth: {depth}, idx: {i} child: {x}");
                            return;
                        }
                    }
                }
            }
        }

        [ContextMenu("Partition")]
        private async void Partition()
        {
            var p = new MeshPartitioner(mesh, gridDims);
            tokenSource = new CancellationTokenSource();
            await Task.Run(() => p.PartitionDisk(@"D:\Bsc\VoxelData\models\terrain\houdini\p\"), tokenSource.Token);
        }



        [ContextMenu("DrawCube 1.25-1.75")]
        private void DrawCube()
        {
            DebugStack.PushBox(new AABB(1.25f, 1.75f));
        }

        [ContextMenu("DrawCube 1-2")]
        private void DrawCubeBig()
        {
            DebugStack.PushBox(new AABB(1f, 2f));
        }


        [ContextMenu("Find Mesh bounds")]
        private async void FindMeshBounds()
        {
            float3 min = mesh.vertices[0];
            float3 max = mesh.vertices[0];

            var localMesh = new LocalMesh(mesh);
            await Task.Run(() =>
            {
                for (int i = 0; i < localMesh.vertices.Length; i++)
                {
                    if (i % 10000 == 0) Debug.Log(i / (float)localMesh.vertices.Length * 100 + "%");
                    min = math.min(localMesh.vertices[i], min);
                    max = math.max(localMesh.vertices[i], max);
                }
            });
            

            Debug.Log($"Bounds mesh: {(float3)mesh.bounds.min} {(float3)mesh.bounds.max}");
            Debug.Log($"Bounds calced: {min}, {max}");
            Debug.Log($"Lengths: {max - min}");
        }

        [ContextMenu("Test")]
        public void Test()
        {
            int count = 1000000;
            var x = new Plane(new float3(1, 0, 0), 1);
            var y = new Plane(new float3(0, 1, 0), 1);
            var z = new Plane(new float3(0, 0, 1), 1);
            var ray = new Ray(new float3(0, 0, 0), new float3(1, 1, 1));


            var sw1 = Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
            {
                var xRes = IntersectPlane(ray, x);
                var yRes = IntersectPlane(ray, y);
                var zRes = IntersectPlane(ray, z);
            }
            Debug.Log($"Dot Intersect: {sw1.ElapsedMilliseconds}ms");

            var positions = new float3(1, 1, 1);

            sw1.Restart();
            for (int i = 0; i < count; i++)
            {
                var res = IntersectPlanes(ray, positions);
            }
            Debug.Log($"Linear Intersect: {sw1.ElapsedMilliseconds}ms");

            var invRay = new OptRay { invDirection = 1.0f / ray.direction, origin = ray.origin };
            sw1.Restart();
            for (int i = 0; i < count; i++)
            {
                var res = IntersectPlanes(invRay, positions);
            }
            Debug.Log($"Linear Intersect: {sw1.ElapsedMilliseconds}ms");

        }

     

        [ContextMenu("Test Color DAG")]
        public unsafe void TestColorDag()
        {
            //SvoColorUnmanaged<SvoNodeColor> svo = octreeToConvert.ReadFromFile() as SvoColorUnmanaged<SvoNodeColor>;
            //var dag = new DagBuilderColorPerPointer();
            //dag.ConstructDag(svo);

            //var toVisitList = new LinkedList<(uint index, uint offset)>();
            //toVisitList.AddFirst((0, 0));
           

            ////Dag stuff
            ////var dagNode = dag.nodes[0];
            //var expectedColorIndex = 0;

            //while (toVisitList.Count > 0)
            //{
            //    (var cIndex, var cColorIndex) = toVisitList.First.Value;
            //    toVisitList.RemoveFirst();
            //    var dagNode = dag.nodes[(int)cIndex];

            //    for (int i = dagNode.children.Count - 1; i >= 0; i--)
            //    {
            //        var childIndex = dagNode.children[i];
            //        toVisitList.AddFirst((childIndex.index, childIndex.attributeOffset + cColorIndex));
            //    }

            //    Assert.AreEqual(expectedColorIndex, cColorIndex);
            //    expectedColorIndex++;
            //}
        }


        [ContextMenu("CPU Raytrace DAG")]
        public async void TestSVOConversion()
        {
            var settings = FindObjectOfType<DataController>().CurrentlyLoadedData;
            var dagData = settings.renderData as DagRenderData;
            if (dagData == null) throw new Exception("Must be a DAG");

            var light = FindObjectOfType<Light>();

            int2 centerScreePos = new int2(Camera.main.pixelWidth / 2, Camera.main.pixelHeight / 2);

            await CPURaytracer.SampleScreenPixel(centerScreePos, dagData, (Texture2D)settings.settings.skyboxTexture, Camera.main, light.transform.forward, light.intensity, 1);
            //await CPURaytracer.Render(dagData, (Texture2D) settings.skyboxTexture, Camera.main, light.transform.forward, light.intensity, 1);
        }

        private float IntersectPlane(Ray ray, Plane plane)
        {
            float ln = math.dot(ray.direction, plane.normal);
            if (ln != 0) //Single point of intersection
            {
                float u = math.dot(plane.position - ray.origin, plane.normal);
                float t = u / ln;

                if (t > 0)
                {
                    return t;
                }
            }

            return math.INFINITY;
        }

        private float3 IntersectPlanes(Ray ray, float3 planePositions)
        {
            var invDir = 1.0f / ray.direction;

            var t = invDir * planePositions - ray.origin * invDir;
            return t;
        }

        private float3 IntersectPlanes(OptRay ray, float3 planePositions)
        {
            var t = ray.invDirection * planePositions - ray.origin * ray.invDirection;

            t.x = t.x < 0 ? math.INFINITY : t.x;
            t.y = t.y < 0 ? math.INFINITY : t.y;
            t.z = t.z < 0 ? math.INFINITY : t.z;

            return t;
        }

        private struct OptRay 
        {
            public float3 invDirection;
            public float3 origin;
        }


#endif

    }
}