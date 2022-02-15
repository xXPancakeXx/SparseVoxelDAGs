using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assets.Scripts
{
    public class VoxelPipeline : RenderPipeline
    {
        private const string PROFILER_TAG = "VoxelPipeline";

        public Texture t;

        private RenderTexture screenTexture;

        private Material mat;
        private RenderTexture cameraTexture;
        private RenderTargetIdentifier rtid;

        public VoxelPipeline()
        {
            mat = new Material(Shader.Find("Hidden/Blue"));

            var camera = Camera.main;
            this.cameraTexture = camera.targetTexture;
            this.rtid = cameraTexture != null ?
                new RenderTargetIdentifier(cameraTexture) :
                new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget);


            screenTexture = new RenderTexture(camera.pixelWidth, camera.pixelHeight, 0, RenderTextureFormat.ARGBFloat);
            screenTexture.Create();
        }

        protected override void Render(ScriptableRenderContext context, Camera[] cameras)
        {
            var cmd = new CommandBuffer { name = PROFILER_TAG };
            cmd.ClearRenderTarget(true, true, Color.white);



            cmd.Blit(t, rtid);
            context.ExecuteCommandBuffer(cmd);

            cmd.Release();
            //CommandBufferPool.Release(cmd);
            context.Submit();
        }
    }
}