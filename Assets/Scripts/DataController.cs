using Assets.Scripts;
using Assets.Scripts.Dag;
using Assets.Scripts.Octree;
using Assets.Scripts.Raytracer;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tayx.Graphy;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Utils;
using static Assets.Scripts.Raytracer.RaytracingFeature;
using Math = System.Math;
using ShadowQuality = UnityEngine.ShadowQuality;

public class DataController : MonoBehaviour
{
    [SerializeField] private ForwardRendererData rendererData;
    private RaytracingFeature rtFeature;
    public UniversalRenderPipelineAsset shadowsAsset;
    public UniversalRenderPipelineAsset noShadowsAsset;

    public DataInfo info;
    public GameObject meshObject;
    public long tris;
    public long verts;

    public bool useColors;
    public bool useDags;
    public bool useMesh;
    public int index;

    private int resolution;
    private int maxDepth;
    
    public RenderInfo CurrentlyLoadedInfo => useColors ? 
        (useDags ? (RenderInfo) Data[index].colDag : Data[index].colOct) :
        (useDags ? (RenderInfo) Data[index].dag : Data[index].oct);
    public RenderData CurrentlyLoadedRenderData => rtFeature.data.renderData;
    public RaytracingData CurrentlyLoadedData => rtFeature.data;
    public List<DataInfo.Data> Data => info.data;

    void Start()
    {
        rtFeature = rendererData.rendererFeatures.OfType<RaytracingFeature>().FirstOrDefault();

        SetData();
    }
    
    
    void Update()
    {
        if (!Input.anyKeyDown) return;

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            useDags = !useDags;
            SetData();
        }
        
        if (Input.GetKeyDown(KeyCode.X))
        {
            Globals.DrawIMGUI = !Globals.DrawIMGUI;
            GraphyManager.Instance.ToggleActive();
        }

        ChangeData(KeyCode.F1, 0);   
        ChangeData(KeyCode.F2, 1);
        ChangeData(KeyCode.F3, 2);
        ChangeData(KeyCode.F4, 3);
        ChangeData(KeyCode.F5, 4);
        ChangeData(KeyCode.F6, 5);
        ChangeData(KeyCode.F7, 6);
        ChangeData(KeyCode.F8, 7);
        ChangeData(KeyCode.F9, 8);
    }

    private void OnGUI()
    {
        if (!Globals.DrawIMGUI) return;

        GUILayout.BeginArea(new Rect(new Vector2(10, 300), new Vector2(170, 800)), "","box");
        
        GUILayout.Label($"Mesh Stats");
        GUILayout.Label($"Tris: {tris:##,###}, Verts: {verts:##,###}");
        GUILayout.Label("");
        
        var dataTypeString = !useDags ? "Octree" : "Dag";
        GUILayout.Label($"{dataTypeString}, {this.resolution}" );
        GUILayout.Label($"Iterations: {maxDepth}");
        int oldDepth = maxDepth;
        maxDepth = (int)GUILayout.HorizontalSlider(maxDepth, 1, math.log2(resolution));
        if (maxDepth != oldDepth)
        {
            rtFeature.data.renderData.maxDepth = maxDepth;
            rtFeature.ResetTAA();
        }

        if (CGUILayout.Toggle(ref rtFeature.data.settings.shadows, "Shadows"))
        {
            rtFeature.ResetTAA();
            if (useMesh)
            {
                Debug.Log("Change");
                
                QualitySettings.SetQualityLevel(rtFeature.data.settings.shadows ? 0 : 1);
                QualitySettings.renderPipeline = rtFeature.data.settings.shadows ? shadowsAsset : noShadowsAsset;

                // qualityAsset.supportsMainLightShadows = rtFeature.data.settings.shadows;
                // qualityAsset.shadows = rtFeature.data.settings.shadows ? ShadowQuality.HardOnly : ShadowQuality.Disable;
            }
        }

        if (CGUILayout.Toggle(ref useMesh, "Use Mesh")) SetData();
        GUILayout.BeginHorizontal();
        if (CGUILayout.Toggle(ref useDags, "Use Dags")) SetData();
        if (CGUILayout.Toggle(ref useColors, "Use Colors")) SetData();
        GUILayout.EndHorizontal();
        
        

        GUILayout.Label("Load Model: ");
        for (int i = 0; i < Data.Count; i++)
        {
            if (GUILayout.Button($"{Data[i].oct.name}"))
            {
                ChangeData(i);
            }
        }
        
        
        GUILayout.EndArea();
    }

    public void ChangeData(KeyCode key, int num)
    {
        if (!Input.GetKeyDown(key)) return;
        
        ChangeData(num);
    }
    
    public void ChangeData( int num)
    {
        if (Data[num] != null)
        {
            index = num;
            SetData();
        }
    }

    public void SetData()
    {
        Destroy(meshObject);
        
        if (useMesh)
        {
            rtFeature.SetActive(false);
            
            var prefab = Data[index].mesh;
            meshObject = GameObject.Instantiate(prefab);

            if (meshObject.transform.position.x == 0.0f)
            {
                meshObject.transform.position = Vector3.one;
                meshObject.transform.rotation = quaternion.identity;
                meshObject.transform.localScale = Vector3.one;
            }

            tris = meshObject.GetComponent<MeshFilter>().mesh.triangles.Length;
            verts = meshObject.GetComponent<MeshFilter>().mesh.vertexCount;
        }
        else
        {
            rtFeature.SetActive(true);
            
            RenderInfo rd = CurrentlyLoadedInfo;
            if (rd == null) return;
        
            this.maxDepth = rd.GetMaxDepth();
            this.resolution = Convert.ToInt32(Math.Pow(2, maxDepth));

            rtFeature.SetData(rd);
        }
    }
}
