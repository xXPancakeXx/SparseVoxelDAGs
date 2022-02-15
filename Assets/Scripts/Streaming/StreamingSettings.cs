using Assets.Scripts.Dag;
using Assets.Scripts.Streaming.Entities;
using System;
using System.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;
using static Assets.Scripts.Raytracer.RaytracingFeature;

namespace Assets.Scripts.Streaming
{
    [Serializable]
    public class StreamingSettings
    {
        public ComputeShader rtShader;
        public ComputeShader depthShadeShader;

        [HideInInspector] public float4x4 cameraToWorld;
        [HideInInspector] public float4x4 cameraInverseProjection;
        [HideInInspector] public float directionalLightIntensity;
        [HideInInspector] public Vector3 directionalLightDirection;
        public Texture skyboxTexture;
        public float skyboxIntensity = .3f;

        public int chunkLoadRange = 2;
        public int chunkUploadSizeLimitPerFrameMBytes = 1;
        public float chunkUploadTimeDelay = 0.0f;

        public bool drawPositionGrayScale;
        public bool enableScreenSpaceNormals;
        [FormerlySerializedAs("normalMethod")] public SSNormalType ssNormalMethod = SSNormalType.Central;

        public bool enableTAA;
        public bool useDeltaResetTAA;
        public float maxCameraMoveDistanceResetTAA = 1e-3f;
        public float maxCameraRotDistanceResetTAA = .3f;

        public WorldInfo worldInfo;
    }
}