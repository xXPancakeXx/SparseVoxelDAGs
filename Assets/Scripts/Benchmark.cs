using System;
using System.Collections;
using System.IO;
using System.Timers;
using Assets.Scripts.Entities;
using Tayx.Graphy;
using Unity.Mathematics;
using UnityEngine;
using Utils;

namespace Assets.Scripts
{
    public class Benchmark : MonoBehaviour
    {
        private Animator camPivotAnimator;
        
        public BenchmarkEntry[] benchmarks;

        private string resX, resY;
        public Material wireframeMat;

        private DataController dataController;

        private void Awake()
        {
            Screen.SetResolution(1920, 1080, false);

            dataController = FindObjectOfType<DataController>();
            camPivotAnimator = Camera.main.transform.parent.GetComponent<Animator>();
            camPivotAnimator.enabled = false;
        }

        private void Update()
        {
            if (Input.GetKey(KeyCode.H)) DebugDrawer.DrawBoxSolid(new AABB(1, 2), wireframeMat);
        }

        private void OnGUI()
        {
            
            if (!Globals.DrawIMGUI) return;

            GUILayout.BeginHorizontal();
            resX = GUILayout.TextField(resX);
            resY = GUILayout.TextField(resY);
            if (GUILayout.Button("Change"))
            {
                Screen.SetResolution(Convert.ToInt32(resX), Convert.ToInt32(resY), false);
            }
            GUILayout.EndHorizontal();
            
            if (GUILayout.Button($"Capture 1 second"))
            {
                StartCoroutine(TrackFrameTime(1.0f));
            }
            
            if (GUILayout.Button($"Resolution Benchmark"))
            {
                StartCoroutine(TrackFrameTimeResolutionsPerModel());
            }

            
            GUILayout.Label("Available Benchmarks");
            for (int i = 0; i < benchmarks.Length; i++)
            {
                if (GUILayout.Button($"Start {benchmarks[i].name}"))
                {
                    //StartCoroutine(TrackFrameTime(1.0f));
                    StartCoroutine(TrackFrameTimeWhileAnim( benchmarks[i]));
                }
            }
        }

        public IEnumerator TrackPerformance(BenchmarkEntry b)
        {
            Globals.DrawIMGUI = false;
            
            camPivotAnimator.enabled = true;
            camPivotAnimator.runtimeAnimatorController = b.animationOverrider;
            camPivotAnimator.SetTrigger("Play");
            
            var con = GameObject.FindObjectOfType<DataController>();
            var modelData = con.CurrentlyLoadedInfo;
            var renderData = con.CurrentlyLoadedRenderData;

            string format = ""; string dataType = "";
            if (renderData is SvoRenderData x) {format = x.format.ToString(); dataType = "svo"; }
            else if (renderData is DagRenderData y) {format = y.format.ToString(); dataType = "dag"; }
            
            var main = Camera.main;
            long screenPixels = main.pixelHeight * main.pixelWidth;

            yield return new WaitUntil(() => camPivotAnimator.GetCurrentAnimatorStateInfo(0).IsName("Play"));
            Log.LogPerformanceHeader(modelData.ModelName, dataType, format, modelData.Resolution);

            long frameCountStart = Time.frameCount;
            long framesElapsed = Time.frameCount;
            
            while (!camPivotAnimator.GetCurrentAnimatorStateInfo(0).IsName("Default"))
            {
                long framesDelta = Time.frameCount - framesElapsed;
                Log.LogPerformance(Time.frameCount - frameCountStart , Time.deltaTime,framesDelta * screenPixels, GraphyManager.Instance.CurrentFPS);
                framesElapsed += framesDelta;
            
                yield return new WaitForSeconds(1);
            }
            
            camPivotAnimator.enabled = false;

            Globals.DrawIMGUI = true;
            yield return null;

        }

        public IEnumerator TrackFrameTimeWhileAnim(BenchmarkEntry b)
        {
            Globals.DrawIMGUI = false;
            camPivotAnimator.enabled = true;
            camPivotAnimator.runtimeAnimatorController = b.animationOverrider;
            camPivotAnimator.SetTrigger("Play");
            
            var con = GameObject.FindObjectOfType<DataController>();
            var modelData = con.CurrentlyLoadedInfo;
            var renderData = con.CurrentlyLoadedRenderData;

            string format = ""; string dataType = "";
            if (renderData is SvoRenderData x) format = x.format.ToString();
            else if (renderData is DagRenderData y) format = y.format.ToString();
            
            var main = Camera.main;
            long screenPixels = main.pixelHeight * main.pixelWidth;

            float[] dts = new float[20000]; 

            yield return new WaitUntil(() => camPivotAnimator.GetCurrentAnimatorStateInfo(0).IsName("Play"));
            Log.LogFrameTimeHeader(modelData.ModelName, dataType, format, modelData.Resolution, screenPixels);
            
            int frameCount = 0;
            while (!camPivotAnimator.GetCurrentAnimatorStateInfo(0).IsName("Default"))
            {
                var dt = Time.deltaTime;
                dts[frameCount++] = dt;
                yield return null;
            }

            var elapsedTime = 0.0f;
            for (int i = 0; i < dts.Length; i++)
            {
                if (dts[i] == 0.0f) break;
                elapsedTime += dts[i];
                Log.LogFrameTime(i, elapsedTime, dts[i]);
            }
            
            
            camPivotAnimator.enabled = false;
            Globals.DrawIMGUI = true;
            yield return null;
        }
        
        public IEnumerator TrackFrameTime(float time)
        {
            Globals.DrawIMGUI = false;
            var con = GameObject.FindObjectOfType<DataController>();
            var modelData = con.CurrentlyLoadedInfo;
            var renderData = con.CurrentlyLoadedRenderData;

            string format = ""; string dataType = "";
            if (renderData is SvoRenderData x) format = x.format.ToString();
            else if (renderData is DagRenderData y) format = y.format.ToString();
            
            var main = Camera.main;
            long screenPixels = main.pixelHeight * main.pixelWidth;

            Log.LogFrameTimeHeader(modelData.ModelName, dataType, format, modelData.Resolution, screenPixels);

            float[] dts = new float[1000]; 
            
            float elapsedTime = 0;
            int frameCount = 0;
            while (elapsedTime <= time)
            {
                var dt = Time.deltaTime;
                dts[frameCount++] = dt;
                elapsedTime += dt;
                yield return null;
            }

            elapsedTime = 0.0f;
            for (int i = 0; i < dts.Length; i++)
            {
                if (dts[i] == 0.0f) break;
                elapsedTime += dts[i];
                Log.LogFrameTime(i, elapsedTime, dts[i]);
            }
            
            Globals.DrawIMGUI = true;
            yield return null;
        }

        public IEnumerator TrackFrameTimeResolutionsPerModel()
        {
            int minModel = 0;
            int maxModel = 4;
            int modelCount = 4;
            
            int minRes = 500;
            int maxRes = 1500;
            int increment = 250;
            
            for (int j = 500; j <= 1500; j+=250)
            {
                Screen.SetResolution(j, j, false);
                File.AppendAllText(Log.PERFORMANCE_STATS_PATH,$"{j}");
                File.AppendAllText(Log.PERFORMANCE_AVERAGE_STATS_PATH,$"{j}");
                
                float dagmin = float.MaxValue; float dagmax = float.MinValue; float dagavg = .0f;
                float svomin = float.MaxValue; float svomax = float.MinValue; float svoavg = .0f;
                for (int i = minModel; i <= maxModel; i++)
                {
                    dataController.index = i;
                
                    dataController.useDags = false;
                    dataController.SetData();
                    yield return new WaitForSeconds(1.0f);
                    float dtSVO = Time.deltaTime;
                
                    dataController.useDags = true;
                    dataController.SetData();
                    yield return new WaitForSeconds(1.0f);
                    float dtDag = Time.deltaTime;

                    svomin = math.min(svomin, dtSVO);
                    svomax = math.max(svomax, dtSVO);
                    svoavg += dtSVO;
                        
                    dagmin = math.min(dagmin, dtDag);
                    dagmax = math.max(dagmax, dtDag);
                    dagavg += dtDag;
                    
                    File.AppendAllText(Log.PERFORMANCE_STATS_PATH, $"&{dtSVO}&{dtDag}");
                }
                
                File.AppendAllText(Log.PERFORMANCE_AVERAGE_STATS_PATH, $"&{svoavg/(float)modelCount}&{svomin}&{svomax}&{dagavg/(float)modelCount}&{dagmin}&{dagmax}\\\\\n");
                File.AppendAllText(Log.PERFORMANCE_STATS_PATH, "\\\\\n");
            }

            yield return null;
        }
        
        
        [Serializable]
        public class BenchmarkEntry {
            public string name;
            public AnimatorOverrideController animationOverrider;
        }
    }
}
