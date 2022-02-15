using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static Assets.Scripts.Raytracer.RaytracingFeature;
using Random = UnityEngine.Random;

namespace Assets.Scripts.Raytracer
{
    public class RaytracingPass : ScriptableRenderPass
    {
        private const string PROFILER_TAG = "RaytracingPass";

        public RenderTargetIdentifier cameraColorTexture;
        public RenderTexture lastFrameTexture;

        private RenderTargetHandle traversalRt;
        private RenderTargetHandle normalRt;
        private RenderTargetHandle colorIndexRt;

        private RaytracingData data;

        private Material aaMat;
        private uint aaCurrentSample;

        public RaytracingPass(RaytracingData data)
        {
            this.data = data;
            this.aaMat = new Material(Shader.Find("Hidden/AddShader"));
            this.aaCurrentSample = 0;

            traversalRt.Init("traversalRt");
            normalRt.Init("normalRt");
            colorIndexRt.Init("colorIndexRt");
        }

        public void ResetAccumulationMaterial()
        {
            aaCurrentSample = 0;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            ConfigureClear(ClearFlag.None, Color.black);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (data.rtShader == null) return;

            var cb = CommandBufferPool.Get(PROFILER_TAG);

            var cam = Camera.main;
            int width = cam.pixelWidth;
            int height = cam.pixelHeight;

            //Check if last frame result is created and is the same size
            if ((lastFrameTexture == null || !lastFrameTexture.IsCreated() || lastFrameTexture.width != width || lastFrameTexture.height != height))
            {
                ResetAccumulationMaterial();
                lastFrameTexture = new RenderTexture(
                    width,
                    height,
                    0,
                    RenderTextureFormat.ARGBFloat,
                    RenderTextureReadWrite.Linear
                );
                lastFrameTexture.Create();
            }

            if (data.settings.aaMethod != AAType.Supersampling)
            {
                if (data.settings.renderMode == RaytracingFeature.RenderMode.ScreenSpaceNormals)
                {
                    if (data.HasColorData)
                        RenderColorScreenNormals(cb, width, height);
                    else
                        RenderScreenNormals(cb, width, height);
                }
                else
                {
                    Render(cb, width, height);
                }
            }
            else
            {
                ResetAccumulationMaterial();
                for (int i = 0; i < data.settings.SSAASampleMultiplier * data.settings.SSAASampleMultiplier; i++)
                {
                    if (data.settings.renderMode == RaytracingFeature.RenderMode.ScreenSpaceNormals)
                    {
                        if (data.HasColorData)
                            RenderColorScreenNormals(cb, width, height);
                        else
                            RenderScreenNormals(cb, width, height);
                    }
                    else
                    {
                        Render(cb, width, height);
                    }
                }
            }
            

            context.ExecuteCommandBuffer(cb);
            CommandBufferPool.Release(cb);
        }

        private void Render(CommandBuffer cb, int width, int height)
        {
            const float GROUP_SIZE = 8f;
            int threadGroupsX, threadGroupsY;

            cb.GetTemporaryRT(traversalRt.id,
                width,
                height,
                0,
                FilterMode.Point,
                RenderTextureFormat.ARGBFloat,
                RenderTextureReadWrite.Linear,
                1,
                true
            );

            threadGroupsX = Mathf.CeilToInt(width / GROUP_SIZE);
            threadGroupsY = Mathf.CeilToInt(height / GROUP_SIZE);

            var kernelIdx = GetTraversalKernelIndex();
            SetTraversalParams(cb, kernelIdx);
            cb.DispatchCompute(data.rtShader, kernelIdx, threadGroupsX, threadGroupsY, 1);

            BlitToCamera(cb, traversalRt.id);

            cb.ReleaseTemporaryRT(traversalRt.id);
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
            cb.DispatchCompute(data.rtShader, kernelIdx, threadGroupsX, threadGroupsY, 1);


            var normalMethodKernel = GetShadingKernelIndex();
            threadGroupsX = Mathf.CeilToInt(width / GROUP_SIZE);
            threadGroupsY = Mathf.CeilToInt(height / GROUP_SIZE);
            SetShadingParams(cb, normalMethodKernel);
            cb.DispatchCompute(data.settings.depthShadeShader, normalMethodKernel, threadGroupsX, threadGroupsY, 1);

            BlitToCamera(cb, normalRt.id);

            cb.ReleaseTemporaryRT(traversalRt.id);
            cb.ReleaseTemporaryRT(normalRt.id);
        }
        private void RenderColorScreenNormals(CommandBuffer cb, int width, int height)
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

            cb.GetTemporaryRT(colorIndexRt.id,
                   width + BORDER_WIDTH * 2,
                   height + BORDER_WIDTH * 2,
                   0,
                   FilterMode.Point,
                   RenderTextureFormat.RInt,
                   RenderTextureReadWrite.Linear,
                   1,
                   true
               );

            cb.GetTemporaryRT(normalRt.id,
                width,
                height,
                0,
                FilterMode.Point,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear,
                1,
                true
            );


            int threadGroupsX, threadGroupsY;
            threadGroupsX = Mathf.CeilToInt(width / GROUP_SIZE) + BORDER_WIDTH * 2;
            threadGroupsY = Mathf.CeilToInt(height / GROUP_SIZE) + BORDER_WIDTH * 2;

            var kernelIdx = GetTraversalKernelIndex();
            SetTraversalParams(cb, kernelIdx);
            SetTraversalColorParams(cb, kernelIdx);
            cb.DispatchCompute(data.rtShader, kernelIdx, threadGroupsX, threadGroupsY, 1);

            threadGroupsX = Mathf.CeilToInt(width / GROUP_SIZE);
            threadGroupsY = Mathf.CeilToInt(height / GROUP_SIZE);

            var shadingKernelIdx = GetShadingKernelIndex();
            SetShadingParams(cb, shadingKernelIdx);
            SetShadingColorParams(cb, shadingKernelIdx);

            cb.DispatchCompute(data.settings.depthShadeShader, shadingKernelIdx, threadGroupsX, threadGroupsY, 1);
            BlitToCamera(cb, normalRt.id);

            cb.ReleaseTemporaryRT(traversalRt.id);
            cb.ReleaseTemporaryRT(normalRt.id);
            cb.ReleaseTemporaryRT(colorIndexRt.id);
        }

        private void BlitToCamera(CommandBuffer cb, RenderTargetIdentifier currentFrameTarget)
        {
            if (data.settings.aaMethod != AAType.None)
            {
                cb.SetGlobalFloat("_sample", aaCurrentSample++);
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
            if (data.HasColorData)
            {
                var dag = data.renderData as DagRenderData;
                if (dag != null)
                {
                    return 5 + (dag.format == Dag.DagFormat.ColorPerPointer ? 1 : 0);
                }

                return 3;

            }

            return (int) data.settings.renderMode;
        }
        private void SetTraversalParams(CommandBuffer cb, int kernelIndex)
        {
            if (data.settings.shadows) cb.EnableShaderKeyword("SHADOWS");
            else cb.DisableShaderKeyword("SHADOWS");
            
            cb.SetComputeBufferParam(data.rtShader, kernelIndex, "_inVoxels", data.dataBuffer);
            cb.SetComputeTextureParam(data.rtShader, kernelIndex, "_outPositions", traversalRt.id);

            cb.SetComputeMatrixParam(data.rtShader, "_cameraToWorld", data.cameraToWorld);
            cb.SetComputeMatrixParam(data.rtShader, "_cameraInverseProjection", data.cameraInverseProjection);
            cb.SetComputeTextureParam(data.rtShader, kernelIndex, "_skyboxTexture", data.settings.skyboxTexture);
            cb.SetComputeFloatParam(data.rtShader, "_skyboxIntensity", data.settings.skyboxIntensity);
            cb.SetComputeIntParam(data.rtShader, "_rayBounces", data.settings.rayBounces);
            cb.SetComputeIntParam(data.rtShader, "_octreeMaxDepth", data.renderData.maxDepth);

            cb.SetComputeVectorParam(data.rtShader, "_directionalLightDirection", data.directionalLightDirection);
            cb.SetComputeFloatParam(data.rtShader, "_directionalLightIntensity", data.directionalLightIntensity);
            cb.SetComputeFloatParam(data.rtShader, "_projFactor", data.settings.projFactor);

            float4 randomPlacement;
            switch (data.settings.aaMethod)
            {
                case AAType.Supersampling:
                    randomPlacement = new float4(GetUniformSample(), 0, 0);
                    break;
                case AAType.Temporal:
                    randomPlacement = new float4(Random.value * data.settings.subpixelMovement, Random.value * data.settings.subpixelMovement, 0, 0);
                    break;
                case AAType.None:
                    randomPlacement = new float4(.5f, .5f, 0, 0);
                    break;
                default:
                    randomPlacement = new float4(.5f, .5f, 0, 0);
                    break;
            }

            if (data.settings.renderMode == RaytracingFeature.RenderMode.DagNodeHighlighting)
            {
                cb.SetComputeIntParams(data.rtShader, "_levelStartIndices", data.LevelStartIndices);
            }

            cb.SetComputeVectorParam(data.rtShader, "_pixelOffset", randomPlacement);
        }

        private float2 GetUniformSample()
        {
            var xStep = aaCurrentSample % data.settings.SSAASampleMultiplier;
            var yStep = aaCurrentSample / data.settings.SSAASampleMultiplier;

            var stepSize = 1 / (float) data.settings.SSAASampleMultiplier;

            return new float2(
                stepSize * xStep + stepSize / 2f,
                stepSize * yStep + stepSize / 2f
            );
        }

        private void SetTraversalColorParams(CommandBuffer cb, int kernelIndex)
        {
            cb.SetComputeTextureParam(data.rtShader, kernelIndex, "_outColorIndices", colorIndexRt.id);
        }

        private int GetShadingKernelIndex()
        {
            return (data.HasColorData ? 3 : 0) + (int) data.settings.ssNormalMethod;
        }

        private void SetShadingParams(CommandBuffer cb, int kernel)
        {
            cb.SetComputeTextureParam(data.settings.depthShadeShader, kernel, "_inPositions", traversalRt.id);
            cb.SetComputeTextureParam(data.settings.depthShadeShader, kernel, "_outColors", normalRt.id);

            cb.SetComputeVectorParam(data.settings.depthShadeShader, "_directionalLightDirection", data.directionalLightDirection);
            cb.SetComputeFloatParam(data.settings.depthShadeShader, "_directionalLightIntensity", data.directionalLightIntensity);
            cb.SetComputeMatrixParam(data.settings.depthShadeShader, "_cameraToWorld", data.cameraToWorld);
            cb.SetComputeMatrixParam(data.settings.depthShadeShader, "_cameraInverseProjection", data.cameraInverseProjection);
            cb.SetComputeTextureParam(data.settings.depthShadeShader, kernel, "_skyboxTexture", data.settings.skyboxTexture);
        }

        private void SetShadingColorParams(CommandBuffer cb, int kernel)
        {
            cb.SetComputeTextureParam(data.settings.depthShadeShader, kernel, "_inColorIndices", colorIndexRt.id);
            cb.SetComputeBufferParam(data.settings.depthShadeShader, kernel, "_inColors", data.colorBuffer);
        }

        #endregion

        public override void FrameCleanup(CommandBuffer cmd)
        {
            base.FrameCleanup(cmd);
        }
    }
}
