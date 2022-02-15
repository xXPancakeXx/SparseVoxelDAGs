using Assets.Scripts;
using Assets.Scripts.Dag;
using Assets.Scripts.Octree;
using Assets.Scripts.Raytracer;
using System;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Assets.Scripts.Streaming
{
    public class WorldStreamingController : MonoBehaviour
    {
        [SerializeField] private ForwardRendererData rendererData;
        [HideInInspector] private StreamingFeature feature;
        public StreamingSettings CurrentlyLoadedSettings => feature.settings;

        public int chunkLoadRange;
        public float chunkStreamingDelay;

        private int chunkX;
        private int chunkY;

        public WorldController World => feature?.world;

        void Start()
        {
            feature = rendererData.rendererFeatures.OfType<StreamingFeature>().FirstOrDefault();
        }


        void Update()
        {
            if (Input.GetKeyDown(KeyCode.R)) Debug.Log("Pos: " + Camera.main.transform.position);
        }

        private void OnGUI()
        {
            if (!Globals.DrawIMGUI) return;
            
            GUILayout.BeginArea(new Rect(new Vector2(400, 0), new Vector2(300, 100)));
            GUILayout.BeginHorizontal();
            chunkX = Convert.ToInt32(GUILayout.TextField(chunkX.ToString()));
            chunkY = Convert.ToInt32(GUILayout.TextField(chunkY.ToString()));
            if (GUILayout.Button("Load chunk"))
            {
                feature.world.LoadChunkIntoMemoryAsync(new int2(chunkX, chunkY));
            }

            if (GUILayout.Button("Unload chunk"))
            {
                feature.world.UnloadChunk(new int2(chunkX, chunkY));
            }
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        private void OnValidate()
        {

        }
    }
}