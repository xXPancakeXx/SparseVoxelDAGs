//#define MY_DEBUG

using Assets.Scripts.Octree;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using Ray = Assets.Scripts.Entities.Ray;
using Plane = Assets.Scripts.Entities.Plane;
using Assets.Scripts.Entities;

namespace Assets.Scripts.Raytracer
{
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

    [Obsolete]
    public partial class CPURaytracer
    {
        private Texture2D rt;

        private float4x4 cameraToWorld;
        private float4x4 cameraInverseProjection;
        private float3 directionalLightDirection;
        private float directionalLightIntensity;

        private Texture2D skybox;

        public static async Task<Color> SampleScreenPixel(int2 screenPos, DagRenderData dag, Texture2D skybox, Camera cam, float3 directionalLightDirection, float directionalLightIntensity, int downSamplingRate)
        {
            var cameraToWorld = cam.cameraToWorldMatrix;
            var cameraInverseProjection = cam.projectionMatrix.inverse;

            // uv e [-1, 1]
            var uv = new float2(screenPos.x / (float)(cam.pixelWidth - 1), screenPos.y / (float)(cam.pixelHeight - 1)) * 2 - new float2(1, 1);
            var ray = CreateCameraRay(uv, cameraToWorld, cameraInverseProjection);

            TraceResultColor res;
            unsafe
            {
                TraceDag((int*)dag.dataPtr, 0, dag.maxDepth, ray, out res, 0);
            }

            return Shade(ray, res.hitPos, new float3(1, 1, 1), skybox, directionalLightDirection, directionalLightIntensity);
        }

        public static async Task Render(DagRenderData dag, Texture2D skybox, Camera cam, float3 directionalLightDirection, float directionalLightIntensity, int downSamplingRate)
        {
            Texture2D rt = null;
            SetupRT(ref rt, cam, downSamplingRate);

            var cameraToWorld = cam.cameraToWorldMatrix;
            var cameraInverseProjection = cam.projectionMatrix.inverse;

            for (int y = 0; y < rt.height; y++)
            {
                for (int x = 0; x < rt.width; x++)
                {
                    // uv range needs to be [-1, 1]

                    var uv = new float2(x / (float)(rt.width - 1), y / (float)(rt.height - 1)) * 2 - new float2(1, 1);
                    var ray = CreateCameraRay(uv, cameraToWorld, cameraInverseProjection);

                    TraceResultColor res;
                    unsafe
                    {
                        TraceDag((int*) dag.dataPtr, 0, dag.maxDepth, ray, out res, 0);
                    }

                    var color = Shade(ray, res.hitPos, new float3(1,1,1), skybox, directionalLightDirection, directionalLightIntensity);
                    rt.SetPixel(x, y, color);
                }
            }

            rt.Apply();

            File.Delete(@"C:\Users\phste\Desktop\image.png");
            File.WriteAllBytes(@"C:\Users\phste\Desktop\image.jpg", rt.EncodeToJPG());
        }


        public async Task Render(DenseOctreeNode octree, Texture2D skybox, Camera cam, float3 directionalLightDirection, float directionalLightIntensity, int downSamplingRate)
        {
            SetupRT(ref rt, cam, downSamplingRate);

            this.cameraToWorld = cam.cameraToWorldMatrix;
            this.cameraInverseProjection = cam.projectionMatrix.inverse;
            this.directionalLightDirection = directionalLightDirection;
            this.directionalLightIntensity = directionalLightIntensity;
            this.skybox = skybox;

            for (int y = 0; y < rt.height; y++)
            {
                for (int x = 0; x < rt.width; x++)
                {
                    // uv range needs to be [-1, 1]

                    var uv = new float2(x / (float)(rt.width - 1), y / (float)(rt.height - 1)) * 2 - new float2(1, 1);
                    var ray = CreateCameraRay(uv, cameraToWorld, cameraInverseProjection);

#if MY_DEBUG
                    Debug.Log($"{x + y * rt.width} / {rt.height * rt.width}");


                    RaytraceDebugStack.Reset();
                    RaytraceDebugStack.SetRay(ray);

                    //Debug.Log($"Distance: {hit.distance} | Color: {color}");

                      if (x == 181 && y == rt.height - 285) 
                        RaytraceDebugStack.WaitForUserInput(true);
#endif

                    var hit = await Trace(ray, octree);
                    var color = Shade(ray, hit, skybox, directionalLightDirection, directionalLightIntensity);

                    rt.SetPixel(x, y, color);
                }
            }

            rt.Apply();

            File.Delete(@"C:\Users\phste\Desktop\image.png");
            File.WriteAllBytes(@"C:\Users\phste\Desktop\image.jpg", rt.EncodeToJPG());
        }

        //public async Task Render(SvoUnmanaged<SvoNodeLaine> octree, Texture2D skybox, Camera cam, float3 directionalLightDirection, float directionalLightIntensity, int downSamplingRate)
//        {
//            SetupRT(ref rt, cam, downSamplingRate);

//            this.cameraToWorld = cam.cameraToWorldMatrix;
//            this.cameraInverseProjection = cam.projectionMatrix.inverse;
//            this.directionalLightDirection = directionalLightDirection;
//            this.directionalLightIntensity = directionalLightIntensity;
//            this.skybox = skybox;

//            for (int y = 0; y < rt.height; y++)
//            {
//                for (int x = 0; x < rt.width; x++)
//                {
//                    // uv range needs to be [-1, 1]

//                    var uv = new float2(x / (float)(rt.width - 1), y / (float)(rt.height - 1)) * 2 - new float2(1, 1);
//                    var ray = CreateCameraRay(uv);

//#if MY_DEBUG
//                    Debug.Log($"{x + y * rt.width} / {rt.height * rt.width}");


//                    RaytraceDebugStack.Reset();
//                    RaytraceDebugStack.SetRay(ray);

//                    //Debug.Log($"Distance: {hit.distance} | Color: {color}");

//                    //if (x == 200 && y == 0)
//                        RaytraceDebugStack.WaitForUserInput(true);
//#endif
//                    var hit = await TraceOctreeUnmanaged(ray, octree);
//                    var color = Shade(ray, hit);



//                    rt.SetPixel(x, y, color);
//                }
//            }

//            rt.Apply();

//            File.Delete(@"C:\Users\phste\Desktop\image.png");
//            File.WriteAllBytes(@"C:\Users\phste\Desktop\image.jpg", rt.EncodeToJPG());
//        }

        //public void RenderNoDebug(SvoUnmanaged octree, Texture2D skybox, Camera cam, float3 directionalLightDirection, float directionalLightIntensity, int downSamplingRate)
        //{
        //    SetupRT(ref rt, cam, downSamplingRate);

        //    this.cameraToWorld = cam.cameraToWorldMatrix;
        //    this.cameraInverseProjection = cam.projectionMatrix.inverse;
        //    this.directionalLightDirection = directionalLightDirection;
        //    this.directionalLightIntensity = directionalLightIntensity;
        //    this.skybox = skybox;

        //    for (int y = 0; y < rt.height; y++)
        //    {
        //        for (int x = 0; x < rt.width; x++)
        //        {
        //            // uv range needs to be [-1, 1]

        //            var uv = new float2(x / (float)(rt.width - 1), y / (float)(rt.height - 1)) * 2 - new float2(1, 1);
        //            var ray = CreateCameraRay(uv);

        //            var hit = TraceOctreeUnmanagedNoDebug(ray, octree);
        //            var color = Shade(ray, hit);

        //            rt.SetPixel(x, y, color);
        //        }
        //    }

        //    rt.Apply();

        //    File.Delete(@"C:\Users\phste\Desktop\image.png");
        //    File.WriteAllBytes(@"C:\Users\phste\Desktop\image.jpg", rt.EncodeToJPG());
        //}


        private static void SetupRT(ref Texture2D rt, Camera cam, int downSamplingRate)
        {
            if (rt == null)
            {
                rt = new Texture2D(cam.pixelWidth / downSamplingRate, cam.pixelHeight / downSamplingRate, TextureFormat.RGBAFloat, false, true);
            }
            else if (rt.width != cam.pixelWidth || rt.height != cam.pixelHeight)
            {
                rt = new Texture2D(cam.pixelWidth / downSamplingRate, cam.pixelHeight / downSamplingRate, TextureFormat.RGBAFloat, false, true);
            }
        }

        private static Ray CreateCameraRay(float2 uv, float4x4 cameraToWorld, float4x4 cameraInverseProjection)
        {
            // Transform the camera origin to world space
            float3 origin = math.mul(cameraToWorld, new float4(0.0f, 0.0f, 0.0f, 1.0f)).xyz;

            // Invert the perspective projection of the view-space position
            float3 direction = math.mul(cameraInverseProjection, new float4(uv, 0.0f, 1.0f)).xyz;
            // Transform the direction from camera to world space and normalize
            direction = math.mul(cameraToWorld, new float4(direction, 0.0f)).xyz;
            direction = math.normalize(direction);
            return new Ray(origin, direction);
        }

        private async Task<RayHit> Trace(Ray ray, DenseOctreeNode octree)
        {
            var hit = new RayHit(float3.zero, math.INFINITY, float3.zero);

#if MY_DEBUG
            RaytraceDebugStack.PushBox(new AABB(octree.MinBounds, octree.MaxBounds));
#endif

            var bounds = new AABB(octree.MinBounds, octree.MaxBounds);

            //Check if bounding box of octree was hit
            var hasHit = AABBIntersect(bounds, ray, ref hit);
            if (!hasHit) return RayHit.NoHit;


            //start ray from intersection point with bounding box
            var rayOriginAABB = new Ray(hit.hitPos, ray.direction);
            var v = await IntersectOctreeAsync(octree, rayOriginAABB, bounds);

            return v;
        }

//        private async Task<RayHit> TraceOctreeUnmanaged(Ray ray, SvoUnmanaged<SvoNodeLaine> octree)
//        {
//            var hit = new RayHit(float3.zero, math.INFINITY, float3.zero);

//#if MY_DEBUG
//            RaytraceDebugStack.PushBox(new AABB(octree.minBounds, octree.maxBounds));
//#endif

//            var bounds = new AABB(octree.minBounds, octree.maxBounds);

//            //Check if bounding box of octree was hit
//            var hasHit = AABBIntersectDistance(bounds, ray, out float t);
//            if (!hasHit) return RayHit.NoHit;

//            var hitPos = t < 0 ? ray.origin : ray.direction * t + ray.origin;
//            //start ray from intersection point with bounding box
//            var rayOriginAABB = new Ray(hitPos, ray.direction);

//            Pointer<SvoNodeLaine> w;
//            unsafe
//            {
//                w = (SvoNodeLaine*) octree.ptr;
//            }
//            hit = await IntersectOctreeUnmanagedIter(w, rayOriginAABB, bounds);

//            return hit;
//        }


        #region DAG

//        public async Task RenderDag(DagUnmanaged dag, Texture2D skybox, Camera cam, float3 directionalLightDirection, float directionalLightIntensity, int downSamplingRate)
//        {
//            SetupRT(ref rt, cam, downSamplingRate);

//            this.cameraToWorld = cam.cameraToWorldMatrix;
//            this.cameraInverseProjection = cam.projectionMatrix.inverse;
//            this.directionalLightDirection = directionalLightDirection;
//            this.directionalLightIntensity = directionalLightIntensity;
//            this.skybox = skybox;

//            for (int y = rt.height / 2; y < rt.height; y++)
//            {
//                for (int x = rt.width / 2; x < rt.width; x++)
//                {
//                    var uv = new float2(x / (float)(rt.width - 1), y / (float)(rt.height - 1)) * 2 - new float2(1, 1);
//                    var ray = CreateCameraRay(uv);


//#if MY_DEBUG
//                    Debug.Log($"({x},{rt.height - y})");


//                    RaytraceDebugStack.Reset();
//                    RaytraceDebugStack.SetRay(ray);
//                    //Debug.Log($"Distance: {hit.distance} | Color: {color}");

//                    RaytraceDebugStack.WaitForUserInput(true);
//#endif

//                    var hit = await TraceDAG(ray, dag);
//                    var color = Shade(ray, hit);

//                    rt.SetPixel(x, y, color);
//                }
//            }

//            rt.Apply();

//            File.Delete(@"C:\Users\phste\Desktop\image.png");
//            File.WriteAllBytes(@"C:\Users\phste\Desktop\image.jpg", rt.EncodeToJPG());
//        }

//        private async Task<RayHit> TraceDAG(Ray ray, DagUnmanaged dag)
//        {
//            var hit = new RayHit(float3.zero, math.INFINITY, float3.zero);
//            var bounds = new AABB(dag.minBounds, dag.maxBounds);

//#if MY_DEBUG
//            RaytraceDebugStack.PushBox(bounds);
//#endif


//            //Check if bounding box of octree was hit
//            var hasHit = AABBIntersectDistance(bounds, ray, out float t);
//            if (!hasHit) return RayHit.NoHit;

//            var hitPos = t < 0 ? ray.origin : ray.direction * t + ray.origin;
//            //start ray from intersection point with bounding box
//            var rayOriginAABB = new Ray(hitPos, ray.direction);

//            Pointer<int> w;
//            unsafe
//            {
//                w = (int*) dag.ptr;
//            }
//            hit = await IntersectDAG(w, 0, rayOriginAABB, bounds);


//            return hit;
//        }

        private async Task<RayHit> IntersectDAG(Pointer<int> o, int index, Ray ray, AABB bounds)
        {
            uint depth = 0;
            var castStack = new DepthDataDAG[16];

            DepthDataDAG depthData;
            float3 test;
            unsafe
            {
                Plane planeX, planeY, planeZ;
                GetMidPlanes(bounds, out planeX, out planeY, out planeZ);
                float3 planeOffsets = GetMidPlaneOffsets(planeX, planeY, planeZ);
                int3 aboveAxis = (int3)(ray.origin >= planeOffsets);

                depthData.bounds = bounds;
                depthData.ray = ray;
                depthData.ptr = index;
                depthData.copiedNode = o.ptr[index];
                depthData.octantId = (aboveAxis.x << 2) | (aboveAxis.y << 1) | (aboveAxis.z);
                test = IntersectPlanes(ray, planeOffsets);
                depthData.distances = new float3(
                    IntersectPlane(ray, planeX),
                    IntersectPlane(ray, planeY),
                    IntersectPlane(ray, planeZ)
                );
            }


            bool exit = false;
            float3 hitPos = depthData.ray.origin;
            while (!exit)
            {
                AABB childBounds = CreateChildAABBForOctant(depthData.bounds, depthData.octantId);


                int childBit = 1 << depthData.octantId;
                bool isValidNode = (depthData.copiedNode & childBit) == childBit;
                bool isLeafNode = depthData.copiedNode == 0;

#if MY_DEBUG
                RaytraceDebugStack.SetIntersection(hitPos);
                RaytraceDebugStack.PushBox(childBounds);
                await RaytraceDebugStack.WaitForStep();
#endif

                if (isValidNode)
                {
                    //Move down the octree (push)

                    //a bitmask with 1 to the bit of octantId 
                    uint lowerIndexLeafOctantMask = (uint)childBit - 1u;

                    //zero out all higher bits than octantId (& op) and count bits which are 0 (tells you how many octants are before this one = offset)

                    int originalValidMask;
                    unsafe
                    {
                        originalValidMask = o.ptr[depthData.ptr];
                    }
                    int childIndexOffset = Utils.Bits.Count1Bits((uint)(originalValidMask & lowerIndexLeafOctantMask)) + 1;

                    //Invalidate current path in copied node by setting valid for this octant to 0
                    depthData.copiedNode &= ~childBit;

                    //Push to stack
                    castStack[depth] = depthData;
                    depth++;

                    unsafe
                    {
                        int childRelativePtr = o.ptr[depthData.ptr + childIndexOffset];
                        int childIndex = depthData.ptr + childIndexOffset + childRelativePtr;
                        int childNode = o.ptr[childIndex];

                        if (childNode == 0)
                        {
                            float t;
                            bool hasHit = AABBIntersectDistance(childBounds, depthData.ray, out t);
                            if (!hasHit) return RayHit.NoHit;

                            return AABBHitAndNormal(childBounds, depthData.ray, t);
                        }

                        PrepareChildData(ref depthData, childBounds, hitPos, depthData.ray.direction, childIndex, childNode);
                    }

                    continue;
                }

                float minDist = math.min(math.min(depthData.distances.x, depthData.distances.y), depthData.distances.z);
                if (math.isinf(minDist))
                {
                    //no hit in this depth (pop)
                    // => move up 1 level 
#if MY_DEBUG
                    RaytraceDebugStack.PopBox();
#endif

                    if (depth == 0u) return RayHit.NoHit;

                    depth--;
                    depthData = castStack[depth];
                    continue;
                }

                hitPos = depthData.ray.origin + depthData.ray.direction * minDist;
                if (!AABBContains(depthData.bounds, hitPos))
                {
#if MY_DEBUG
                    RaytraceDebugStack.PopBox();
                    RaytraceDebugStack.PopBox();
#endif

                    //no hit anymore in parent bounding box => ray moves to another parent
                    if (depth == 0u) return RayHit.NoHit;
                    depth--;

                    depthData = castStack[depth];
                    //copiedNode = castStack[depth].copiedNode;

                    //depthData = castStack[2];
                    continue;
                }

                if (minDist == depthData.distances.x)
                {
                    depthData.octantId ^= 4;
                    depthData.distances.x = math.INFINITY;
                }
                else if (minDist == depthData.distances.y)
                {
                    depthData.octantId ^= 2;
                    depthData.distances.y = math.INFINITY;
                }
                else if (minDist == depthData.distances.z)
                {
                    depthData.octantId ^= 1;
                    depthData.distances.z = math.INFINITY;
                }

#if MY_DEBUG
                RaytraceDebugStack.PopBox();
#endif
            }

            return RayHit.NoHit;
        }

        private async Task<RayHit> IntersectDAGOpt(Pointer<int> o, int index, Ray ray, AABB bounds)
        {
            var castStack = new DepthDataDAGOpt[16];

            DepthDataDAGOpt depthData;
            float3 test;
            
                AABBIntersectTRange(bounds, ray, out float tmin, out float tmax);
                
                Plane planeX, planeY, planeZ;
                GetMidPlanes(bounds, out planeX, out planeY, out planeZ);
                float3 planeOffsets = GetMidPlaneOffsets(planeX, planeY, planeZ);

                float3 intersectPointMin = ray.direction * tmin + ray.origin;
                float3 intersectPointMax = ray.direction * tmax + ray.origin;
                float3 intersectPointMid = ray.direction * (tmax - tmin) * .5f + ray.origin;
            int3 aboveAxis = (int3)(intersectPointMin >= tmin);

#if MY_DEBUG
                RaytraceDebugStack.PushPoint(intersectPointMin);
                RaytraceDebugStack.PushPoint(intersectPointMax);
                RaytraceDebugStack.PushPoint(intersectPointMid);
                RaytraceDebugStack.PushBox(bounds);
                await RaytraceDebugStack.WaitForStep();

                RaytraceDebugStack.PopPoint();
                RaytraceDebugStack.PopPoint();
                RaytraceDebugStack.PopPoint();
#endif


            unsafe
            {
                depthData.ray = ray;
                depthData.ptr = index;
                depthData.copiedNode = o.ptr[index];
                depthData.octantId = (aboveAxis.x << 2) | (aboveAxis.y << 1) | (aboveAxis.z);
                test = IntersectPlanes(ray, planeOffsets);
                depthData.distances = new float3(
                    IntersectPlane(ray, planeX),
                    IntersectPlane(ray, planeY),
                    IntersectPlane(ray, planeZ)
                );
            }


//            bool exit = false;
//            float3 hitPos = depthData.ray.origin;
//            while (!exit)
//            {
//                AABB childBounds = CreateChildAABBForOctant(depthData.bounds, depthData.octantId);


//                int childBit = 1 << depthData.octantId;
//                bool isValidNode = (depthData.copiedNode & childBit) == childBit;
//                bool isLeafNode = depthData.copiedNode == 0;

//#if MY_DEBUG
//                RaytraceDebugStack.SetIntersection(hitPos);
//                RaytraceDebugStack.PushBox(childBounds);
//                await RaytraceDebugStack.WaitForStep();
//#endif

//                if (isValidNode)
//                {
//                    //Move down the octree (push)

//                    //a bitmask with 1 to the bit of octantId 
//                    uint lowerIndexLeafOctantMask = (uint)childBit - 1u;

//                    //zero out all higher bits than octantId (& op) and count bits which are 0 (tells you how many octants are before this one = offset)

//                    int originalValidMask;
//                    unsafe
//                    {
//                        originalValidMask = o.ptr[depthData.ptr];
//                    }
//                    int childIndexOffset = Utils.Bits.Count1Bits((uint)(originalValidMask & lowerIndexLeafOctantMask)) + 1;

//                    //Invalidate current path in copied node by setting valid for this octant to 0
//                    depthData.copiedNode &= ~childBit;

//                    //Push to stack
//                    castStack[depth] = depthData;
//                    depth++;

//                    unsafe
//                    {
//                        int childRelativePtr = o.ptr[depthData.ptr + childIndexOffset];
//                        int childIndex = depthData.ptr + childIndexOffset + childRelativePtr;
//                        int childNode = o.ptr[childIndex];

//                        if (childNode == 0)
//                        {
//                            float t;
//                            bool hasHit = AABBIntersectDistance(childBounds, depthData.ray, out t);
//                            if (!hasHit) return RayHit.NoHit;

//                            return AABBHitAndNormal(childBounds, depthData.ray, t);
//                        }

//                        PrepareChildData(ref depthData, childBounds, hitPos, depthData.ray.direction, childIndex, childNode);
//                    }

//                    continue;
//                }

//                float minDist = math.min(math.min(depthData.distances.x, depthData.distances.y), depthData.distances.z);
//                if (math.isinf(minDist))
//                {
//                    //no hit in this depth (pop)
//                    // => move up 1 level 
//#if MY_DEBUG
//                    RaytraceDebugStack.PopBox();
//#endif

//                    if (depth == 0u) return RayHit.NoHit;

//                    depth--;
//                    depthData = castStack[depth];
//                    continue;
//                }

//                hitPos = depthData.ray.origin + depthData.ray.direction * minDist;
//                if (!AABBContains(depthData.bounds, hitPos))
//                {
//#if MY_DEBUG
//                    RaytraceDebugStack.PopBox();
//                    RaytraceDebugStack.PopBox();
//#endif

//                    //no hit anymore in parent bounding box => ray moves to another parent
//                    if (depth == 0u) return RayHit.NoHit;
//                    depth--;

//                    depthData = castStack[depth];
//                    //copiedNode = castStack[depth].copiedNode;

//                    //depthData = castStack[2];
//                    continue;
//                }

//                if (minDist == depthData.distances.x)
//                {
//                    depthData.octantId ^= 4;
//                    depthData.distances.x = math.INFINITY;
//                }
//                else if (minDist == depthData.distances.y)
//                {
//                    depthData.octantId ^= 2;
//                    depthData.distances.y = math.INFINITY;
//                }
//                else if (minDist == depthData.distances.z)
//                {
//                    depthData.octantId ^= 1;
//                    depthData.distances.z = math.INFINITY;
//                }

//#if MY_DEBUG
//                RaytraceDebugStack.PopBox();
//#endif
//            }

            return RayHit.NoHit;
        }

        #endregion


        //private RayHit TraceOctreeUnmanagedNoDebug(Ray ray, SvoUnmanaged<SvoNodeLaine> octree)
        //{
        //    var hit = new RayHit(float3.zero, math.INFINITY, float3.zero);

        //    var bounds = new AABB(octree.minBounds, octree.maxBounds);

        //    //Check if bounding box of octree was hit
        //    var hasHit = AABBIntersectDistance(bounds, ray, out float t);
        //    if (!hasHit) return RayHit.NoHit;

        //    var hitPos = t < 0 ? ray.origin : ray.direction * t + ray.origin;
        //    //start ray from intersection point with bounding box
        //    var rayOriginAABB = new Ray(hitPos, ray.direction);

        //    unsafe 
        //    {
        //        return IntersectOctreeUnmanagedIterNoDebug((SvoNodeLaine*) octree.ptr, rayOriginAABB, bounds);
        //    }
        //}

        private async Task<RayHit> IntersectOctreeAsync(DenseOctreeNode o, Ray ray, AABB bounds)
        {
            var node = o as DenseOctreeNode;

            Plane xPlane, yPlane, zPlane;
            GetMidPlanes(bounds, out xPlane, out yPlane, out zPlane);
            var planeOffsets = GetMidPlaneOffsets(xPlane, yPlane, zPlane);

            var hitPos = ray.origin;
            int3 aboveAxis = (int3)(hitPos >= planeOffsets);
            var distances = new float3(
                IntersectPlane(ray, xPlane),
                IntersectPlane(ray, yPlane),
                IntersectPlane(ray, zPlane)
            );

            if (o.IsLeaf)
            {
                if (o.ContainsVoxel) 
                {
                    RayHit hit = default;
                    AABBIntersect(new AABB(node.MinBounds, node.MaxBounds), ray, ref hit);
                    return hit;
                }
                else
                {
                    return RayHit.NoHit;
                }
            }

            var octantId = (aboveAxis.x << 2) | (aboveAxis.y << 1) | (aboveAxis.z);
            for (var i = 0; i < 4; ++i)
            {
                //right-top-back   right-top-front    right-bottom-back   right-bottom-front      left-top-back      left-top-front     left-bottom-back   left-bottom-front
                //7                6                  5                   4                         3                    2              1                   0

                var childBounds = CreateChildAABBForOctant(bounds, octantId);

#if MY_DEBUG
                RaytraceDebugStack.SetIntersection(hitPos);
                RaytraceDebugStack.PushBox(childBounds);
                await RaytraceDebugStack.WaitForStep();
#endif

                var ret = await IntersectOctreeAsync(node.Children[octantId], new Ray(hitPos, ray.direction), childBounds);

#if MY_DEBUG
                RaytraceDebugStack.PopBox();
#endif

                if (ret.distance != math.INFINITY) return ret;

                var minDist = math.min(math.min(distances.x, distances.y), distances.z);
                if (math.isinf(minDist)) return RayHit.NoHit;

                hitPos = ray.origin + ray.direction * minDist;

                if (!AABBContains(bounds, hitPos)) return RayHit.NoHit;
                if (minDist == distances.x)
                {
                    octantId ^= 4;
                    distances.x = math.INFINITY;
                }
                else if (minDist == distances.y)
                {
                    octantId ^= 2;
                    distances.y = math.INFINITY;
                }
                else if (minDist == distances.z)
                {
                    octantId ^= 1;
                    distances.z = math.INFINITY;
                }
            }


            // no hit in this octant
            // return to parent octant (pop)
            return RayHit.NoHit;
        }

        private async Task<RayHit> IntersectOctreeUnmanaged(Pointer<SvoNodeLaine> o, Ray ray, AABB bounds)
        {
            var node = o.Data;
            Plane xPlane, yPlane, zPlane;
            GetMidPlanes(bounds, out xPlane, out yPlane, out zPlane);
            var planeOffsets = GetMidPlaneOffsets(xPlane, yPlane, zPlane);

            var hitPos = ray.origin;
            int3 aboveAxis = (int3)(hitPos >= planeOffsets);
            var distances = new float3(
                IntersectPlane(ray, xPlane),
                IntersectPlane(ray, yPlane),
                IntersectPlane(ray, zPlane)
            );

            var octantId = (aboveAxis.x << 2) | (aboveAxis.y << 1) | (aboveAxis.z);
            for (var i = 0; i < 4; ++i)
            {
                var childBounds = CreateChildAABBForOctant(bounds, octantId);

#if MY_DEBUG
                RaytraceDebugStack.SetIntersection(hitPos);
                RaytraceDebugStack.PushBox(childBounds);
                await RaytraceDebugStack.WaitForStep();
#endif
                byte childBit = (byte)(1 << octantId);
                bool isValidNode = (node.validMask & childBit) == childBit;
                bool isLeafNode = (node.leafMask & childBit) == childBit;

                if (isValidNode && !isLeafNode)
                {
                    //a bitmask with 1 to the bit of octantId 
                    byte lowerIndexLeafOctantMask = (byte) (childBit - 1u);
                    //zero out all higher bits than octantId (& op) and count bits which are 1 (tells you how many octants are before this one = offset)
                    var offfset = Utils.Bits.Count1Bits((uint)(~node.leafMask & lowerIndexLeafOctantMask));

                    Pointer<SvoNodeLaine> w;
                    unsafe
                    {
                        w = o.ptr + node.childrenRelativePointer + offfset;
                    }
                    var res = await IntersectOctreeUnmanaged(w, new Ray(hitPos, ray.direction), childBounds);
                    if (res.distance < math.INFINITY) return res;
                }
                else if (isValidNode && isLeafNode)
                {
#if MY_DEBUG
                    RaytraceDebugStack.PopBox();
#endif
                    RayHit hit = default;
                    var hasHit = AABBIntersect(childBounds, ray, ref hit);
                    if (!hasHit) return RayHit.NoHit;
                    return hit;
                }

                var minDist = math.min(math.min(distances.x, distances.y), distances.z);
                if (math.isinf(minDist)) return RayHit.NoHit;

                hitPos = ray.origin + ray.direction * minDist;
                if (!AABBContains(bounds, hitPos))
                {
#if MY_DEBUG
                    RaytraceDebugStack.SetIntersection(hitPos);
                    await RaytraceDebugStack.WaitForStep();
                    RaytraceDebugStack.PopBox();
#endif

                    //no hit anymore in parent bounding box => ray moves to another octant
                    return RayHit.NoHit;
                }

                if (minDist == distances.x)
                {
                    octantId ^= 4;
                    distances.x = math.INFINITY;
                }
                else if (minDist == distances.y)
                {
                    octantId ^= 2;
                    distances.y = math.INFINITY;
                }
                else if (minDist == distances.z)
                {
                    octantId ^= 1;
                    distances.z = math.INFINITY;
                }
#if MY_DEBUG
                RaytraceDebugStack.PopBox();
#endif
            }

            // no hit in this octant
            // return to parent octant (pop)
            return RayHit.NoHit;
        }

        private async Task<RayHit> IntersectOctreeUnmanagedIter(Pointer<SvoNodeLaine> o, Ray ray, AABB bounds)
        {
            var castStack = new CastStack(true);

            DepthDataSVO depthData;
            {
                Plane xPlane, yPlane, zPlane;
                GetMidPlanes(bounds, out xPlane, out yPlane, out zPlane);
                var planeOffsets = GetMidPlaneOffsets(xPlane, yPlane, zPlane);
                int3 aboveAxis = (int3)(ray.origin >= planeOffsets);

                unsafe
                {
                    depthData.bounds = bounds;
                    depthData.ray = ray;
                    depthData.ptr = o.ptr;
                    depthData.copiedNode = *o.ptr;
                    depthData.octantId = (aboveAxis.x << 2) | (aboveAxis.y << 1) | (aboveAxis.z);
                    depthData.distances = new float3(
                        IntersectPlane(ray, xPlane),
                        IntersectPlane(ray, yPlane),
                        IntersectPlane(ray, zPlane)
                    );
                }
            }

            bool exit = false;
            var hitPos = depthData.ray.origin;
            while (!exit)
            {
                var childBounds = CreateChildAABBForOctant(depthData.bounds, depthData.octantId);

#if MY_DEBUG
                RaytraceDebugStack.SetIntersection(hitPos);
                RaytraceDebugStack.PushBox(childBounds);
                await RaytraceDebugStack.WaitForStep();
#endif
                byte childBit = (byte)(1 << depthData.octantId);
                bool isValidNode = (depthData.copiedNode.validMask & childBit) == childBit;
                bool isLeafNode = (depthData.copiedNode.leafMask & childBit) == childBit;

                if (isValidNode)
                {
                    if (isLeafNode)
                    {
#if MY_DEBUG
                        RaytraceDebugStack.PopBox();
#endif
                        var hasHit = AABBIntersectDistance(childBounds, depthData.ray, out float t);
                        if (!hasHit) return RayHit.NoHit;
                        return AABBHitAndNormal(childBounds, depthData.ray, t);
                    }
                    else
                    {
                        //Move down the octree (push)

                        //a bitmask with 1 to the bit of octantId 
                        byte lowerIndexLeafOctantMask = (byte)(childBit - 1u);
                        //zero out all higher bits than octantId (& op) and count bits which are 0 (tells you how many octants are before this one = offset)
                        var offfset = Utils.Bits.Count1Bits((uint)(~depthData.copiedNode.leafMask & lowerIndexLeafOctantMask));

                        Pointer<SvoNodeLaine> w;
                        unsafe
                        {
                            //Invalidate current path in copied node by setting valid for this octant to 0
                            depthData.copiedNode.validMask &= (byte) ~childBit;
                            //Push to stack
                            castStack.Push(depthData);

                            w = depthData.ptr + depthData.copiedNode.childrenRelativePointer + offfset;
                            
                            PrepareChildData(ref depthData, childBounds, hitPos, depthData.ray.direction, w);
                            
                            continue;
                        }
                    }
                }

                var minDist = math.min(math.min(depthData.distances.x, depthData.distances.y), depthData.distances.z);
                if (math.isinf(minDist))
                {
                    //no hit in this depth (pop)
                    // => move up 1 level 

                    if (castStack.index == 0) return RayHit.NoHit;

                    depthData = castStack.Pop();
                    continue;
                }

                hitPos = depthData.ray.origin + depthData.ray.direction * minDist;
                if (!AABBContains(depthData.bounds, hitPos))
                {
#if MY_DEBUG
                    RaytraceDebugStack.SetIntersection(hitPos);
                    await RaytraceDebugStack.WaitForStep();
                    RaytraceDebugStack.PopBox();
                    RaytraceDebugStack.PopBox();
#endif

                    //no hit anymore in parent bounding box => ray moves to another parent
                    if (castStack.index == 0) return RayHit.NoHit;

                    depthData = castStack.Pop();
                    continue;
                }

                if (minDist == depthData.distances.x)
                {
                    depthData.octantId ^= 4;
                    depthData.distances.x = math.INFINITY;
                }
                else if (minDist == depthData.distances.y)
                {
                    depthData.octantId ^= 2;
                    depthData.distances.y = math.INFINITY;
                }
                else if (minDist == depthData.distances.z)
                {
                    depthData.octantId ^= 1;
                    depthData.distances.z = math.INFINITY;
                }
#if MY_DEBUG
                RaytraceDebugStack.PopBox();
#endif
            }

            return RayHit.NoHit;
        }

        private unsafe RayHit IntersectOctreeUnmanagedIterNoDebug(SvoNodeLaine* o, Ray ray, AABB bounds)
        {
            var castStack = new CastStack(true);

            DepthDataSVO depthData;
            {
                Plane xPlane, yPlane, zPlane;
                GetMidPlanes(bounds, out xPlane, out yPlane, out zPlane);
                var planeOffsets = GetMidPlaneOffsets(xPlane, yPlane, zPlane);
                int3 aboveAxis = (int3)(ray.origin >= planeOffsets);

                unsafe
                {
                    depthData.bounds = bounds;
                    depthData.ray = ray;
                    depthData.ptr = o;
                    depthData.copiedNode = *o;
                    depthData.octantId = (aboveAxis.x << 2) | (aboveAxis.y << 1) | (aboveAxis.z);
                    depthData.distances = new float3(
                        IntersectPlane(ray, xPlane),
                        IntersectPlane(ray, yPlane),
                        IntersectPlane(ray, zPlane)
                    );
                }
            }



            bool exit = false;
            var hitPos = depthData.ray.origin;
            while (!exit)
            {
                var childBounds = CreateChildAABBForOctant(depthData.bounds, depthData.octantId);

                byte childBit = (byte)(1 << depthData.octantId);
                bool isValidNode = (depthData.copiedNode.validMask & childBit) == childBit;
                bool isLeafNode = (depthData.copiedNode.leafMask & childBit) == childBit;

                if (isValidNode)
                {
                    if (isLeafNode)
                    {
                        var hasHit = AABBIntersectDistance(childBounds, depthData.ray, out float t);
                        if (!hasHit) return RayHit.NoHit;
                        return AABBHitAndNormal(childBounds, depthData.ray, t);
                    }
                    else
                    {
                        //Move down the octree (push)

                        //a bitmask with 1 to the bit of octantId 
                        byte lowerIndexLeafOctantMask = (byte)(childBit - 1u);
                        //zero out all higher bits than octantId (& op) and count bits which are 0 (tells you how many octants are before this one = offset)
                        var offfset = Utils.Bits.Count1Bits((uint)(~depthData.copiedNode.leafMask & lowerIndexLeafOctantMask));

                        Pointer<SvoNodeLaine> w;
                        unsafe
                        {
                            //Invalidate current path in copied node by setting valid for this octant to 0
                            depthData.copiedNode.validMask &= (byte)~childBit;
                            //Push to stack
                            castStack.Push(depthData);

                            w = depthData.ptr + depthData.copiedNode.childrenRelativePointer + offfset;

                            PrepareChildData(ref depthData, childBounds, hitPos, depthData.ray.direction, w);

                            continue;
                        }
                    }
                }

                var minDist = math.min(math.min(depthData.distances.x, depthData.distances.y), depthData.distances.z);
                if (math.isinf(minDist))
                {
                    //no hit in this depth (pop)
                    // => move up 1 level 

                    if (castStack.index == 0) return RayHit.NoHit;

                    depthData = castStack.Pop();
                    continue;
                }

                hitPos = depthData.ray.origin + depthData.ray.direction * minDist;
                if (!AABBContains(depthData.bounds, hitPos))
                {
                    //no hit anymore in parent bounding box => ray moves to another parent
                    if (castStack.index == 0) return RayHit.NoHit;

                    depthData = castStack.Pop();
                    continue;
                }

                if (minDist == depthData.distances.x)
                {
                    depthData.octantId ^= 4;
                    depthData.distances.x = math.INFINITY;
                }
                else if (minDist == depthData.distances.y)
                {
                    depthData.octantId ^= 2;
                    depthData.distances.y = math.INFINITY;
                }
                else if (minDist == depthData.distances.z)
                {
                    depthData.octantId ^= 1;
                    depthData.distances.z = math.INFINITY;
                }
            }

            return RayHit.NoHit;
        }

        #region DAG LAINE

        //public async Task RenderDAGLaine(DagUnmanaged dag, Texture2D skybox, Camera cam, float3 directionalLightDirection, float directionalLightIntensity, int downSamplingRate, bool useScreenSpaceNormals)
        //{
        //    SetupRT(ref rt, cam, downSamplingRate);

        //    this.cameraToWorld = cam.cameraToWorldMatrix;
        //    this.cameraInverseProjection = cam.projectionMatrix.inverse;
        //    this.directionalLightDirection = directionalLightDirection;
        //    this.directionalLightIntensity = directionalLightIntensity;
        //    this.skybox = skybox;

        //    if (useScreenSpaceNormals)
        //    {
        //        var _posTex = await RenderPosTex(dag, cam);
        //        ShadeSSN(_posTex);
        //    }
        //    else
        //    {

        //        for (int y = 0; y < rt.height; y++)
        //        {
        //            for (int x = 0; x < rt.width; x++)
        //            {
        //                var uv = new float2(x / (float)(rt.width - 1), y / (float)(rt.height - 1)) * 2 - new float2(1, 1);
        //                var ray = CreateCameraRay(uv);

        //                var res = await TraceDAGLaine(dag, ray, 0);
        //                var normal = CalculateNormal(res);

        //                var color = Shade(ray, res.hitPos, normal);
        //                rt.SetPixel(x, y, color);
        //            }
        //        }
        //    }

        //    rt.Apply();

        //    File.Delete(@"C:\Users\phste\Desktop\image.png");
        //    File.WriteAllBytes(@"C:\Users\phste\Desktop\image.jpg", rt.EncodeToJPG());
        //}

        //private void ShadeSSN(float3[,] _posTex)
//        {

//            for (int y = rt.height - 401; y < rt.height - 1; y++)
//            {
//                for (int x = 936; x < rt.width - 1; x++)
//                {
//                    // uv range needs to be [-1, 1]
//                    var uv = new float2(x / (float)(rt.width - 1), y / (float)(rt.height - 1)) * 2 - new float2(1, 1);
//                    var ray = CreateCameraRay(uv);

//                    var pos = _posTex[x, y];
//                    var p1 = _posTex[x + 1, y];
//                    var p2 = _posTex[x, y + 1];

//                    var dir1 = p1 - pos;
//                    var dir2 = p2 - pos;

//                    var c = math.cross(dir1, dir2);
//                    if (math.abs(c.x) < math.exp2(-23)) c.x = 0;
//                    if (math.abs(c.y) < math.exp2(-23)) c.y = 0;
//                    if (math.abs(c.z) < math.exp2(-23)) c.z = 0;


//                    pos = _posTex[x, y].xyz;

//                    float3 right = _posTex[x + 1, y].xyz - pos;
//                    float3 left = _posTex[x - 1, y].xyz - pos;
//                    float3 up = _posTex[x, y + 1].xyz - pos;
//                    float3 down = _posTex[x, y - 1].xyz - pos;

//                    float3 horizontal = (math.abs(right.z)) < (math.abs(left.z)) ? right : left * -1;
//                    float3 vertical = (math.abs(up.z)) < (math.abs(down.z)) ? up : down * -1;

//                    c = math.cross(horizontal, vertical);
//                    if (math.abs(c.x) < math.exp2(-23)) c.x = 0;
//                    if (math.abs(c.y) < math.exp2(-23)) c.y = 0;
//                    if (math.abs(c.z) < math.exp2(-23)) c.z = 0;

//                    var normal = math.normalize(c);

//#if MY_DEBUG
//                    Debug.Log($"({x},{y}): {pos}, normal: {normal}");
//#endif
//                    var color = Shade(ray, pos, normal);
//                    rt.SetPixel(x, y, color);
//                }
//            }

        
//        }

//        private async Task<float3[,]> RenderPosTex(DagUnmanaged dag, Camera cam)
//        {
//            var posTex = new float3[cam.pixelWidth, cam.pixelHeight];
//            for (int y = 0; y < rt.height; y++)
//            {
//                for (int x = 0; x < rt.width; x++)
//                {
//                    // uv range needs to be [-1, 1]
//                    var uv = new float2(x / (float)(rt.width - 1), y / (float)(rt.height - 1)) * 2 - new float2(1, 1);
//                    var ray = CreateCameraRay(uv);

//#if MY_DEBUG
//                    RaytraceDebugStack.SetRay(ray);
//                    RaytraceDebugStack.WaitForUserInput(true);
//#endif

//                    var res = await TraceDAGLaine(dag, ray, 0);
//                    posTex[x, y] = res.hitPos;
//                }
//            }

//            return posTex;
//        }

//#endregion

        #region Utility


        private float GetProjectionFactor(float fov, int screenHeight, float pixelTolerance, float screenDivisor = 1.0f)
        {
            //Projection scale function of projected point
            float halfFov = fov / 2.0f;
            float inv_2tan_half_fov = 1.0f / (2.0f * math.tan(halfFov * math.PI / 180.0f));

            float screen_tolerance = pixelTolerance / (screenHeight / screenDivisor);
            return inv_2tan_half_fov / screen_tolerance;

            //original code
            //const float inv_2tan_half_fovy = 1.0f / (2.0f * tan(0.5f * _fovy));
            //const float screen_tolerance = _pixelTolerance / (_screenRes[1] / screenDivisor);
            //return inv_2tan_half_fovy / screen_tolerance;
        }

        private unsafe void PrepareChildData(ref DepthDataDAG depthData, AABB childBounds, float3 voxelEntryPos, float3 rayDir, int childPtr, int copiedNode)
        {
            Plane xPlane, yPlane, zPlane;
            GetMidPlanes(childBounds, out xPlane, out yPlane, out zPlane);
            var planeOffsets = GetMidPlaneOffsets(xPlane, yPlane, zPlane);
            int3 aboveAxis = (int3)(voxelEntryPos >= planeOffsets);

            var childRay = new Ray(voxelEntryPos, rayDir);
            unsafe
            {
                depthData.bounds = childBounds;
                depthData.ray = new Ray(voxelEntryPos, rayDir);
                depthData.ptr = childPtr;
                depthData.copiedNode = copiedNode;
                depthData.octantId = (aboveAxis.x << 2) | (aboveAxis.y << 1) | (aboveAxis.z);
                depthData.distances = new float3(
                    IntersectPlane(childRay, xPlane),
                    IntersectPlane(childRay, yPlane),
                    IntersectPlane(childRay, zPlane)
                );
            }
        }

        private unsafe void PrepareChildData(ref DepthDataSVO depthData, AABB childBounds, float3 voxelEntryPos, float3 rayDir, SvoNodeLaine* childPtr)
        {
            Plane xPlane, yPlane, zPlane;
            GetMidPlanes(childBounds, out xPlane, out yPlane, out zPlane);
            var planeOffsets = GetMidPlaneOffsets(xPlane, yPlane, zPlane);
            int3 aboveAxis = (int3)(voxelEntryPos >= planeOffsets);

            var childRay = new Ray(voxelEntryPos, rayDir);
            unsafe
            {
                depthData.bounds = childBounds;
                depthData.ray = new Ray(voxelEntryPos, rayDir);
                depthData.ptr = childPtr;
                depthData.copiedNode = *childPtr;
                depthData.octantId = (aboveAxis.x << 2) | (aboveAxis.y << 1) | (aboveAxis.z);
                depthData.distances = new float3(
                    IntersectPlane(childRay, xPlane),
                    IntersectPlane(childRay, yPlane),
                    IntersectPlane(childRay, zPlane)
                );
            }
        }

        private void InvalidateMinDistance(ref float3 distances, ref int octantId)
        {
            var minDist = math.min(math.min(distances.x, distances.y), distances.z);

            if (minDist == distances.x)
            {
                octantId ^= 4;
                distances.x = math.INFINITY;
            }
            else if (minDist == distances.y)
            {
                octantId ^= 2;
                distances.y = math.INFINITY;
            }
            else if (minDist == distances.z)
            {
                octantId ^= 1;
                distances.z = math.INFINITY;
            }
        }

        private AABB CreateChildAABBForOctant(AABB bounds, int octantId)
        {
            var childOffset = (float3)DenseOctree.childIndexOffsets[octantId];
            var childSize = (bounds[1].x - bounds[0].x) / 2f;
            var childMin = bounds[0] + childSize * childOffset;
            var childMax = bounds[0] + childSize * (childOffset + new float3(1, 1, 1));
            return new AABB(childMin, childMax);
        }

        private float IntersectPlane(Ray ray, Plane plane, ref RayHit bestHit)
        {
            float ln = math.dot(ray.direction, plane.normal);
            if (ln != 0) //Single point of intersection
            {
                float u = math.dot(plane.position - ray.origin, plane.normal);
                float d = u / ln;

                if (d > 0 && d < bestHit.distance)
                {
                    bestHit.distance = d;
                    bestHit.hitPos = ray.origin + d * ray.direction;
                }

                return d;
            }

            return math.INFINITY;
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


        private void GetMidPlanes(AABB box, out Plane xPlane, out Plane yPlane, out Plane zPlane)
        {
            xPlane = new Plane(new float3(1, 0, 0), box[0].x + ((box[1].x - box[0].x) / 2));
            yPlane = new Plane(new float3(0, 1, 0), box[0].y + ((box[1].y - box[0].y) / 2));
            zPlane = new Plane(new float3(0, 0, 1), box[0].z + ((box[1].z - box[0].z) / 2));
        }

        private float3 GetMidPlaneOffsets(Plane xPlane, Plane yPlane, Plane zPlane)
        {
            return new float3(xPlane.position, yPlane.position, zPlane.position);
        }

        private static bool AABBIntersect(AABB box, Ray ray, ref RayHit hit)
        {
            var invDir = 1.0f / ray.direction;
            var sign = new int3(invDir < 0.0f);

            var boundsMin = new float3(box[sign.x].x, box[sign.y].y, box[sign.z].z);
            var boundsMax = new float3(box[1-sign.x].x, box[1-sign.y].y, box[1-sign.z].z);

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

            hit.distance = tmin.x;
            hit.hitPos = ray.origin + tmin.x * ray.direction;

            var originHit = hit.hitPos - box[0] - ((box[1] - box[0]) / 2f);

            var index = 0;
            hit.normal = new float3(0);
            if (math.abs(originHit.y) > math.abs(originHit.x)) index = 1;
            if (math.abs(originHit.z) > math.abs(originHit[index])) index = 2;
            hit.normal[index] = originHit[index];
            hit.normal = math.normalize(hit.normal);

            return true;
        }

        bool AABBIntersect2(AABB box, Ray ray, ref RayHit hit)
        {
            float epsilon = 0.001f; // required to prevent self intersection

            float3 tmin = (box[0] - ray.origin) / ray.direction;
            float3 tmax = (box[1] - ray.origin) / ray.direction;

            float3 real_min = math.min(tmin, tmax);
            float3 real_max = math.max(tmin, tmax);

            float minmax = math.min(math.min(real_max.x, real_max.y), real_max.z);
            float maxmin = math.max(math.max(real_min.x, real_min.y), real_min.z);

            hit.distance = minmax;
            hit.hitPos = ray.origin + minmax * ray.direction;

            float3 originHit = hit.hitPos - box[0] - ((box[1] - box[0]) / 2.0f);
            hit.normal = new float3(0, 0, 0);


            if (math.abs(originHit.y) > math.abs(originHit.x))
            {
                hit.normal = math.abs(originHit.z) > math.abs(originHit.y) ? new float3(0, 0, originHit.z) : new float3(0, originHit.y, 0);
            }
            else
            {
                hit.normal = math.abs(originHit.z) > math.abs(originHit.x) ? new float3(0, 0, originHit.z) : new float3(originHit.x, 0, 0);
            }

            if (minmax >= maxmin) return maxmin > epsilon ? true : false;
            else return false;
        }

        //private bool AABBIntersect(AABB box, Ray ray, out float distance, out float3 normal, bool canStartInBox, in bool oriented, in float3 _invRayDir)
        //{
        //    ray.origin = ray.origin - box.center;

        //    float winding = canStartInBox && (math.max(math.abs(ray.origin) * box.invRadius) < 1.0) ? -1 : 1;
        //    float3 sgn = -math.sign(ray.direction);

        //    // Distance to plane
        //    var d = box.radius * winding * sgn - ray.origin;
        //    d *= _invRayDir;

        //    distance = (sgn.x != 0) ? d.x : ((sgn.y != 0) ? d.y : d.z);
        //    normal = sgn;

        //    return (sgn.x != 0) || (sgn.y != 0) || (sgn.z != 0);
        //}


        private bool AABBIntersectDistance(AABB box, Ray ray, out float t)
        {
            // This is actually correct, even though it appears not to handle edge cases
            // (ray.n.{x,y,z} == 0).  It works because the infinities that result from
            // dividing by zero will still behave correctly in the comparisons.  Rays
            // which are parallel to an axis and outside the box will have tmin == inf
            // or tmax == -inf, while rays inside the box will have tmin and tmax
            // unchanged.

            var rayInverse = 1f / ray.direction;

            var tx1 = (box[0].x - ray.origin.x) * rayInverse.x;
            var tx2 = (box[1].x - ray.origin.x) * rayInverse.x;

            var tmin = math.min(tx1, tx2);
            var tmax = math.max(tx1, tx2);

            var ty1 = (box[0].y - ray.origin.y) * rayInverse.y;
            var ty2 = (box[1].y - ray.origin.y) * rayInverse.y;

            tmin = math.max(tmin, math.min(ty1, ty2));
            tmax = math.min(tmax, math.max(ty1, ty2));

            var tz1 = (box[0].z - ray.origin.z) * rayInverse.z;
            var tz2 = (box[1].z - ray.origin.z) * rayInverse.z;

            tmin = math.max(tmin, math.min(tz1, tz2));
            tmax = math.min(tmax, math.max(tz1, tz2));
            
            t = tmin;
            return tmax >= math.max(0.0f, tmin);
        }

        private void AABBIntersectTRange(AABB box, Ray ray, out float tmin, out float tmax)
        {
            // This is actually correct, even though it appears not to handle edge cases
            // (ray.n.{x,y,z} == 0).  It works because the infinities that result from
            // dividing by zero will still behave correctly in the comparisons.  Rays
            // which are parallel to an axis and outside the box will have tmin == inf
            // or tmax == -inf, while rays inside the box will have tmin and tmax
            // unchanged.

            var rayInverse = 1f / ray.direction;

            var tx1 = (box[0].x - ray.origin.x) * rayInverse.x;
            var tx2 = (box[1].x - ray.origin.x) * rayInverse.x;

            tmin = math.min(tx1, tx2);
            tmax = math.max(tx1, tx2);

            var ty1 = (box[0].y - ray.origin.y) * rayInverse.y;
            var ty2 = (box[1].y - ray.origin.y) * rayInverse.y;

            tmin = math.max(tmin, math.min(ty1, ty2));
            tmax = math.min(tmax, math.max(ty1, ty2));

            var tz1 = (box[0].z - ray.origin.z) * rayInverse.z;
            var tz2 = (box[1].z - ray.origin.z) * rayInverse.z;

            tmin = math.max(tmin, math.min(tz1, tz2));
            tmax = math.min(tmax, math.max(tz1, tz2));

            //t = tmin;
            //return tmax >= math.max(0.0f, tmin);
        }

        private RayHit AABBHitAndNormal(AABB box, Ray ray, float t)
        {
            RayHit hit = new RayHit();

            hit.hitPos = ray.origin + ray.direction * t;

            float3 originHit = hit.hitPos - box[0] - ((box[1] - box[0]) / 2.0f);
            if (math.abs(originHit.y) > math.abs(originHit.x))
            {
                hit.normal = math.abs(originHit.z) > math.abs(originHit.y) ? new float3(0, 0, originHit.z) : new float3(0, originHit.y, 0);
            }
            else
            {
                hit.normal = math.abs(originHit.z) > math.abs(originHit.x) ? new float3(0, 0, originHit.z) : new float3(originHit.x, 0, 0);
            }
            hit.normal = math.normalize(hit.normal);

            return hit;
        }

        private RayHit AABBHitAndNormal(float3 pos, float t, float3 bT, float3 dT)
        {
            RayHit hit = new RayHit();

            hit.hitPos = (t + bT) / dT;

            float3 originHit = hit.hitPos - pos;
            if (math.abs(originHit.y) > math.abs(originHit.x))
            {
                hit.normal = math.abs(originHit.z) > math.abs(originHit.y) ? new float3(0, 0, originHit.z) : new float3(0, originHit.y, 0);
            }
            else
            {
                hit.normal = math.abs(originHit.z) > math.abs(originHit.x) ? new float3(0, 0, originHit.z) : new float3(originHit.x, 0, 0);
            }
            hit.normal = math.normalize(hit.normal);

            return hit;
        }

        private float3 CalculateNormal(TraceResult res)
        {
            float3 normal;
            float3 originHit = res.hitPos - res.voxelPos;
            if (math.abs(originHit.y) > math.abs(originHit.x))
            {
                normal = math.abs(originHit.z) > math.abs(originHit.y) ? new float3(0, 0, originHit.z) : new float3(0, originHit.y, 0);
            }
            else
            {
                normal = math.abs(originHit.z) > math.abs(originHit.x) ? new float3(0, 0, originHit.z) : new float3(originHit.x, 0, 0);
            }
            normal = math.normalize(normal);
            return normal;
        }

        private bool AABBContains(AABB box, float3 point)
        {
            return point.x >= box[0].x && point.x <= box[1].x
                && point.y >= box[0].y && point.y <= box[1].y
                && point.z >= box[0].z && point.z <= box[1].z;
        }

        private static Color Shade(Ray ray, RayHit hit, Texture2D skybox, float3 directionalLightDirection, float directionalLightIntensity)
        {
            if (hit.distance < math.INFINITY)
            {
                var albedo = new float3(.6f);

                float3 color = math.saturate(math.dot(hit.normal, directionalLightDirection) * -1) * directionalLightIntensity * albedo;

                //float3 reflectVec = math.reflect(_directionalLightDirection, hit.normal);
                //float3 camWorldPos = math.mul(cameraToWorld, new float4(0.0f, 0.0f, 0.0f, 1.0f)).xyz;
                //float3 viewVec = math.normalize(camWorldPos - hit.position);
                //color += math.pow(math.saturate(math.dot(reflectVec, viewVec) * -1), 3) * hit.specular;

                return new Color(color.x, color.y, color.z);
            }
            else
            {
                float theta = math.acos(ray.direction.y) / -math.PI;
                float phi = math.atan2(ray.direction.x, -ray.direction.z) / -math.PI * 0.5f;
                return skybox.GetPixelBilinear(phi, theta);
            }
        }

        private static Color Shade(Ray ray, float3 hitPos, float3 normal, Texture2D skybox, float3 directionalLightDirection, float directionalLightIntensity)
        {
            if (math.any(hitPos))
            {
                var albedo = new float3(.6f);

                float3 color = math.saturate(math.dot(normal, directionalLightDirection) * -1) * directionalLightIntensity * albedo;

                //float3 reflectVec = math.reflect(_directionalLightDirection, hit.normal);
                //float3 camWorldPos = math.mul(cameraToWorld, new float4(0.0f, 0.0f, 0.0f, 1.0f)).xyz;
                //float3 viewVec = math.normalize(camWorldPos - hit.position);
                //color += math.pow(math.saturate(math.dot(reflectVec, viewVec) * -1), 3) * hit.specular;

                return new Color(color.x, color.y, color.z);
            }
            else
            {
                float theta = math.acos(ray.direction.y) / -math.PI;
                float phi = math.atan2(ray.direction.x, -ray.direction.z) / -math.PI * 0.5f;
                return skybox.GetPixelBilinear(phi, theta);
            }
        }
        #endregion

        #endregion
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously

    }
}