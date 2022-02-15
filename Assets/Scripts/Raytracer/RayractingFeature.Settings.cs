using Assets.Scripts.Dag;
using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace Assets.Scripts.Raytracer
{
    public partial class RaytracingFeature
    {
        [Serializable]
        public enum SSNormalType
        {
            Forward,
            Cross,
            Central
        }

        public enum AAType
        {
            None,
            Temporal,
            Supersampling
        }

        public enum RenderMode
        {
            ScreenSpaceNormals,
            WorldSpaceNormals,
            PositionGrayscale,
            DagNodeHighlighting,
            LODHighlighting
        }

        [Serializable]
        public class RaytracingData
        {
            public RaytracingSettings settings = RaytracingSettings.CreateDefault();
            
            [HideInInspector] public float4x4 cameraToWorld;
            [HideInInspector] public float4x4 cameraInverseProjection;

            #region DAG NodeHighlightingData
            [NonSerialized] public int[] LevelStartIndices;
            #endregion

            [HideInInspector] public float directionalLightIntensity;
            [HideInInspector] public Vector3 directionalLightDirection;

            [HideInInspector] public ComputeShader rtShader;
            public RenderData renderData;

            [NonSerialized] public ComputeBuffer dataBuffer;
            [NonSerialized] public ComputeBuffer colorBuffer;

            public bool HasColorData => renderData.colorByteSize > 0;
        }

        [Serializable]
        public struct RaytracingSettings
        {
            public ComputeShader depthShadeShader;
            
            public RenderMode renderMode;
            public SSNormalType ssNormalMethod;
            
            #region AA Settings
            public AAType aaMethod;
            public int SSAASampleMultiplier;

            public float maxCameraMoveDistanceResetTAA ;
            public float maxCameraRotDistanceResetTAA;
            [Range(0, 1)] public float subpixelMovement;
            #endregion

            public bool shadows;
            
            public Texture skyboxTexture;
            public float skyboxIntensity;
            [Range(1, 8)] public int rayBounces;
            
            [Range(0, .01f)] public float projFactor;


            public static RaytracingSettings CreateDefault()
            {
                RaytracingSettings x = new RaytracingSettings();
                
                x.depthShadeShader = null;
                x.renderMode = RenderMode.ScreenSpaceNormals;
                x.ssNormalMethod = SSNormalType.Central;
                x.aaMethod = AAType.Temporal;
                x.SSAASampleMultiplier = 2;
                x.maxCameraMoveDistanceResetTAA =  1e-3f;
                x.maxCameraRotDistanceResetTAA = .3f;
                x.subpixelMovement = .6f;
                x.skyboxTexture = null;
                x.skyboxIntensity = .3f;
                x.rayBounces = 4;

                return x;
            }
        } 
    }
}
