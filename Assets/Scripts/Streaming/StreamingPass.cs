using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Random = UnityEngine.Random;

namespace Assets.Scripts.Streaming
{
    public class StreamingPass : ScriptableRenderPass
    {
        private const string PROFILER_TAG = "RaytracingPass";

        public RenderTargetIdentifier cameraColorTexture;
        public RenderTexture lastFrameTexture;

        private RenderTargetHandle traversalRt;
        private RenderTargetHandle normalRt;
        private RenderTargetHandle colorIndexRt;

        private StreamingSettings settings;
        private WorldController world;

        private Material aaMat;
        private uint aaCurrentSample;

        public StreamingPass(StreamingSettings data, WorldController world)
        {
            this.settings = data;
            this.aaMat = new Material(Shader.Find("Hidden/AddShader"));
            this.aaCurrentSample = 0;
            this.world = world;

            traversalRt.Init("traversalRt");
            normalRt.Init("normalRt");
            colorIndexRt.Init("colorIndexRt");
        }

        public void ResetTAA()
        {
            aaCurrentSample = 0;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            ConfigureClear(ClearFlag.None, Color.black);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (settings.rtShader == null) return;

            var cb = CommandBufferPool.Get(PROFILER_TAG);

            var cam = Camera.main;
            int width = cam.pixelWidth;
            int height = cam.pixelHeight;

            //Check if last frame result is created and is the same size
            if (settings.enableTAA && (lastFrameTexture == null || !lastFrameTexture.IsCreated() || lastFrameTexture.width != width || lastFrameTexture.height != height))
            {
                ResetTAA();
                lastFrameTexture = new RenderTexture(
                    width,
                    height,
                    0,
                    RenderTextureFormat.ARGBFloat,
                    RenderTextureReadWrite.Linear
                );
                lastFrameTexture.Create();
            }

            RenderScreenNormals(cb, width, height);

            context.ExecuteCommandBuffer(cb);
            CommandBufferPool.Release(cb);
        }

        
        private void RenderScreenNormals(CommandBuffer cb, int width, int height)
        {
            const int BORDER_WIDTH = 1;
            const float GROUP_SIZE = 8.0f;
            
            cb.GetTemporaryRT(traversalRt.id,
                width + BORDER_WIDTH * 2,
                height + BORDER_WIDTH * 2,
                0,
                FilterMode.Point,
                RenderTextureFormat.ARGBFloat,
                RenderTextureReadWrite.Linear,
                1,
                true
            );

            cb.GetTemporaryRT(normalRt.id,
                width,
                height,
                0,
                FilterMode.Point,
                RenderTextureFormat.ARGBFloat,
                RenderTextureReadWrite.Linear,
                1,
                true
            );

           
            int threadGroupsX, threadGroupsY;
            threadGroupsX = Mathf.CeilToInt(width / GROUP_SIZE) + BORDER_WIDTH * 2;
            threadGroupsY = Mathf.CeilToInt(height / GROUP_SIZE) + BORDER_WIDTH * 2;

            var kernelIdx = GetTraversalKernelIndex();
            SetTraversalParams(cb, kernelIdx);
            cb.DispatchCompute(settings.rtShader, kernelIdx, threadGroupsX, threadGroupsY, 1);


            var normalMethodKernel = GetShadingKernelIndex();
            threadGroupsX = Mathf.CeilToInt(width / GROUP_SIZE);
            threadGroupsY = Mathf.CeilToInt(height / GROUP_SIZE);
            SetShadingParams(cb, normalMethodKernel);
            cb.DispatchCompute(settings.depthShadeShader, normalMethodKernel, threadGroupsX, threadGroupsY, 1);

            BlitToCamera(cb, normalRt.id);

            cb.ReleaseTemporaryRT(traversalRt.id);
            cb.ReleaseTemporaryRT(normalRt.id);
        }


        private void BlitToCamera(CommandBuffer cb, RenderTargetIdentifier currentFrameTarget)
        {
            if (settings.enableTAA)
            {
                aaMat.SetFloat("_sample", aaCurrentSample++);
                cb.Blit(currentFrameTarget, lastFrameTexture, aaMat);
                cb.Blit(lastFrameTexture, cameraColorTexture);
            }
            else
            {
                cb.Blit(currentFrameTarget, cameraColorTexture);
            }
        }

        #region Methods for parameter configuration


        private int GetTraversalKernelIndex()
        {
            return 0;
        }

        private void SetTraversalParams(CommandBuffer cb, int kernelIndex)
        {
            cb.SetComputeTextureParam(settings.rtShader, kernelIndex, "_outPositions", traversalRt.id);

            cb.SetComputeBufferParam(settings.rtShader, kernelIndex, "_inVoxels", world.chunkVoxelBuffer);
            cb.SetComputeBufferParam(settings.rtShader, kernelIndex, "_inChunkToVoxels", world.chunkDataBuffer);

            cb.SetComputeIntParam(settings.rtShader, "_chunkIntSize", world.ChunkSizeInt);
            cb.SetComputeIntParams(settings.rtShader, "_chunkDims", world.ChunkDims.x, world.ChunkDims.y);
            cb.SetComputeIntParam(settings.rtShader, "_chunkLoadRange", world.ChunkLoadRange);

            cb.SetComputeMatrixParam(settings.rtShader, "_cameraToWorld", settings.cameraToWorld);
            cb.SetComputeMatrixParam(settings.rtShader, "_cameraInverseProjection", settings.cameraInverseProjection);
            cb.SetComputeTextureParam(settings.rtShader, kernelIndex, "_skyboxTexture", settings.skyboxTexture);
            cb.SetComputeFloatParam(settings.rtShader, "_skyboxIntensity", settings.skyboxIntensity);
            cb.SetComputeVectorParam(settings.rtShader, "_octreeLowerBounds", new Vector3(1, 1, 1));
            cb.SetComputeVectorParam(settings.rtShader, "_octreeUpperBounds", new Vector3(2, 2, 2));
            

            cb.SetComputeVectorParam(settings.rtShader, "_directionalLightDirection", settings.directionalLightDirection);
            cb.SetComputeFloatParam(settings.rtShader, "_directionalLightIntensity", settings.directionalLightIntensity);


            var randomPlacement = settings.enableTAA ? new float4(Random.value, Random.value, 0, 0) : new float4(.5f, .5f, 0, 0);
            cb.SetComputeVectorParam(settings.rtShader, "_pixelOffset", randomPlacement);
        }

        private void SetTraversalColorParams(CommandBuffer cb, int kernelIndex)
        {
            cb.SetComputeTextureParam(settings.rtShader, kernelIndex, "_outColorIndices", colorIndexRt.id);
        }

        private int GetShadingKernelIndex()
        {
            return 3 * (false ? 1 : 0) + (int) settings.ssNormalMethod;
        }

        private void SetShadingParams(CommandBuffer cb, int kernel)
        {
            cb.SetComputeTextureParam(settings.depthShadeShader, kernel, "_inPositions", traversalRt.id);
            cb.SetComputeTextureParam(settings.depthShadeShader, kernel, "_outColors", normalRt.id);

            cb.SetComputeVectorParam(settings.depthShadeShader, "_directionalLightDirection", settings.directionalLightDirection);
            cb.SetComputeFloatParam(settings.depthShadeShader, "_directionalLightIntensity", settings.directionalLightIntensity);
            cb.SetComputeMatrixParam(settings.depthShadeShader, "_cameraToWorld", settings.cameraToWorld);
            cb.SetComputeMatrixParam(settings.depthShadeShader, "_cameraInverseProjection", settings.cameraInverseProjection);
            cb.SetComputeTextureParam(settings.depthShadeShader, kernel, "_skyboxTexture", settings.skyboxTexture);
        }

        //private void SetShadingColorParams(CommandBuffer cb, int kernel)
        //{
        //    cb.SetComputeTextureParam(settings.depthShadeShader, kernel, "_inColorIndices", colorIndexRt.id);
        //    cb.SetComputeBufferParam(settings.depthShadeShader, kernel, "_inColors", settings.colorBuffer);
        //}

        #endregion

        public override void FrameCleanup(CommandBuffer cmd)
        {
            base.FrameCleanup(cmd);
        }
    }
}
