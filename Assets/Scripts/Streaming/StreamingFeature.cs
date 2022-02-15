using System;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Assets.Scripts.Streaming
{
    public partial class StreamingFeature : ScriptableRendererFeature
    {
        public RenderPassEvent @event = RenderPassEvent.AfterRenderingOpaques;

        public StreamingSettings settings;
        public WorldController world;

        private Camera camera;
        private Light mainLight;
        private StreamingPass pass;

        private float3 oldCamPos;
        private float3 oldCamRot;
        
        public override void Create()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) return;
#endif

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

        private void InitData()
        {
            if (pass == null)
            {
                if (settings.worldInfo == null) throw new Exception("No world data specified!");

                try
                {
                    world?.Dispose();
                    world = new WorldController(settings.worldInfo, settings.chunkLoadRange, settings.chunkUploadSizeLimitPerFrameMBytes, settings.chunkUploadTimeDelay);
                    pass = new StreamingPass(settings, world);
                }
                catch (Exception e)
                {
#if UNITY_EDITOR
                    Dispose(false);
                    EditorApplication.ExitPlaymode();

                    throw e;
#endif
                }
            }
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) return;
#endif

            pass.cameraColorTexture = renderer.cameraColorTarget;
            pass.renderPassEvent = @event;

            settings.cameraInverseProjection = camera.projectionMatrix.inverse;
            settings.cameraToWorld = camera.cameraToWorldMatrix;

            settings.directionalLightIntensity = mainLight.intensity;
            settings.directionalLightDirection = mainLight.transform.forward;

            var camPos = camera.transform.position;
            var camRot = camera.transform.rotation.eulerAngles;
            if (settings.useDeltaResetTAA)
            {
                if (math.distance(oldCamPos, camPos) >= settings.maxCameraMoveDistanceResetTAA
                || math.distance(oldCamRot, camRot) >= settings.maxCameraRotDistanceResetTAA)
                {
                    pass.ResetTAA();
                    oldCamPos = camPos;
                    oldCamRot = camRot;
                }
            }
            else
            {
                if (camera.transform.hasChanged)
                {
                    pass.ResetTAA();
                    camera.transform.hasChanged = false;
                }
            }
            

            world.Update();
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            pass = null;

            world?.Dispose();
            world = null;
        }
    }
}
