using Assets.Scripts.Octree;
using System;
using System.IO;
using System.Linq;
using Assets.Scripts.Dag;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;
using Utils;

namespace Assets.Scripts.Raytracer
{
    public partial class RaytracingFeature : ScriptableRendererFeature
    {
        public ComputeShader octreeShader;
        public ComputeShader dagShader;
        public RenderPassEvent @event = RenderPassEvent.AfterRenderingOpaques;

        [FormerlySerializedAs("settings")] public RaytracingData data;
        public RenderInfo renderInfo;

        public RaytracingPass pass;
        
        private Camera camera;
        private Light mainLight;
        private float3 oldCamPos;
        private float3 oldCamRot;

        public override void Create()
        {
            if (isActive)
            {
                camera = Camera.main;
                mainLight = GameObject.FindObjectOfType<Light>();
                InitData();
            }
            else
            {
                Dispose();
            }
        }

        public void OnValidate()
        {
            //Read pointer counts bc it is required to generate an unique index for every node
            if (data.settings.renderMode == RenderMode.DagNodeHighlighting || data.settings.renderMode == RenderMode.LODHighlighting)
            {
                //skip for svos and colored dags
                if (renderInfo is SvoInfo || this.data.HasColorData) 
                {
                    data.settings.renderMode = RenderMode.ScreenSpaceNormals;
                    return;
                }
                
                data.LevelStartIndices = new int[23 * 4];
            
                using (var fs = new FileStream(renderInfo.Path, FileMode.Open))
                using (var br = new BinaryReader(fs))
                {
                    var h = new DagHeader();
                    h.version = br.ReadInt32();
                    h.format = (DagFormat)br.ReadInt32();

                    var builder = DagInfo.GetBuilderForFormat((int)h.format);
                    builder.ReadHeader(br, ref h);

                    int accumulatedPtrs = 0;
                    int accNodes = 0;
                    for (int l = 0; l < h.maxDepth; l++)
                    {
                        for (int i = 0; i < h.NodesPerLevel[l]; i++)
                        {
                            var validMask = br.ReadInt32() & 0xFF;
                            var childrenCount = math.countbits(validMask);
                    
                            accumulatedPtrs += childrenCount;
                            accNodes++;
                            br.ReadBytes(childrenCount* 4);
                        }

                        data.LevelStartIndices[(l + 1)*4] += accumulatedPtrs;
                        data.LevelStartIndices[(l + 1)*4] += accNodes;
                    }
                }
                
            }
        }

        public void SetData(RenderInfo renderInfo)
        {
            this.renderInfo = renderInfo;
            this.Dispose(false);

            OnValidate();
            InitData();
        }

        public void ResetTAA()
        {
            pass?.ResetAccumulationMaterial();
        }

        public void OverrideShader(ComputeShader rtShader)
        {
            data.rtShader = rtShader;
        }

        private void InitData()
        {
            if (pass == null)
            {
                if (renderInfo == null) throw new Exception("No voxel data specified!");

                data.renderData?.Dispose();
                data.renderData = renderInfo.ReadFromFileNew();

                unsafe
                {
                    var dataArr = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<int>(
                        data.renderData.dataPtr,
                        (int)(data.renderData.dataByteSize / 4), 
                        Allocator.None
                    );
#if UNITY_EDITOR
                    NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref dataArr, AtomicSafetyHandle.Create());
#endif
                    data.dataBuffer?.Dispose();
                    data.dataBuffer = new ComputeBuffer((int)data.renderData.dataByteSize / 4, 4);
                    data.dataBuffer.SetData(dataArr);
                    dataArr.Dispose();
                    
                    if (data.renderData.colorByteSize != 0)
                    {
                        var colArr = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<int>(
                            data.renderData.colorPtr,
                            (int)(data.renderData.colorByteSize / 4),
                            Allocator.None
                        );
#if UNITY_EDITOR
                        NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref colArr, AtomicSafetyHandle.Create());
#endif

                        data.colorBuffer?.Dispose();
                        data.colorBuffer = new ComputeBuffer((int)data.renderData.colorByteSize / 4, 4);
                        data.colorBuffer.SetData(colArr);
                        
                        colArr.Dispose();
                    }
                    

                    //int writtenLongs = 0;
                    //int bufferSize = Int16.MaxValue;
                    //long[] buffer = new long[bufferSize];
                    //while (writtenLongs < settings.renderData.byteSize / 8)
                    //{
                    //    int longsToWrite = (int)Math.Min(settings.renderData.byteSize / 8 - writtenLongs, bufferSize);
                    //    fixed (long* ptr = buffer)
                    //    {
                    //        UnsafeUtility.MemCpy(ptr, ((long*)settings.renderData.ptr) + writtenLongs, longsToWrite * 8);
                    //    }

                    //    settings.dataBuffer.SetData(buffer, 0, writtenLongs, longsToWrite);
                    //    writtenLongs += longsToWrite;
                    //}


                    //int writtenLongs = 0;
                    //var data = settings.dataBuffer.BeginWrite<long>(0, (int)(settings.renderData.byteSize / 8));
                    //long* dataPtr = (long*) data.GetUnsafePtr();
                    //UnsafeUtility.MemCpy(dataPtr, ((long*)settings.renderData.ptr) + writtenLongs, settings.renderData.byteSize);
                    //settings.dataBuffer.EndWrite<long>((int)(settings.renderData.byteSize / 8));
                }

                if (renderInfo is SvoInfo)
                {
                    data.rtShader = octreeShader;
                }
                else
                {
                    data.rtShader = dagShader;
                }

                pass = new RaytracingPass(data);
            }
        }

        private void UpdateData()
        {
            data.cameraInverseProjection = camera.projectionMatrix.inverse;
            data.cameraToWorld = camera.cameraToWorldMatrix;

            data.directionalLightIntensity = mainLight.intensity;
            data.directionalLightDirection = mainLight.transform.forward;

            var camPos = camera.transform.position;
            var camRot = camera.transform.rotation.eulerAngles;
            if (math.distance(oldCamPos, camPos) >= data.settings.maxCameraMoveDistanceResetTAA
                || math.distance(oldCamRot, camRot) >= data.settings.maxCameraRotDistanceResetTAA) 
            {
                pass.ResetAccumulationMaterial();
                oldCamPos = camPos;
                oldCamRot = camRot;
            }
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            pass.cameraColorTexture = renderer.cameraColorTarget;
            pass.renderPassEvent = @event;

            UpdateData();
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            pass = null;

            data.dataBuffer?.Dispose();
            data.colorBuffer?.Dispose();
            
            data.dataBuffer = null;
            data.colorBuffer = null;
        }
    }
}
