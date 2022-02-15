using Assets.Scripts.Dag;
using Assets.Scripts.Dag.Builders;
using Assets.Scripts.Octree;
using Assets.Scripts.Octree.Builders;
using Assets.Scripts.Voxelization;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Unity.Jobs;
using UnityEditor;
using UnityEngine;
using Utils;
using static Assets.Scripts.Octree.SvoInfo;
using Debug = UnityEngine.Debug;

namespace Assets.Scripts.Editor
{
    public class DataCreatorWindow : EditorWindow
    {
        public const string SVO_BUILDER_PATH = @"D:\Bsc\VoxelData\bin\svo_builder_omp.exe";
        public const string TRI_BUILDER_PATH = @"D:\Bsc\VoxelData\bin\tri_convert.exe";
        public const string DATA_PATH = @"D:\Bsc\VoxelData\";
        public const string INFO_PATH = @"Assets/Data/";

        private SvoInfo octInfo;
        private DagInfo dagInfo;

        private string modelPathName = @"D:\Bsc\VoxelData\models\";
        private string folderPath = @"D:\Bsc\VoxelData\models\";
        private string outAssetPath = @"D:\Bsc\SparseVoxelDAGs\Assets\Data";

        private int gridDimension;
        private GameObject obj;
        private bool useColors;
        private SvoFormat svoFormat;

        private bool deleteOctConverterFiles;
        private bool overrideFiles;
        private int conversionMaxDepthDag;
        private int conversionDepthDag;
        private DagFormat dagFormat;

        private bool foldoutFromPly;
        private bool foldoutFromOctreeConverter;
        private bool foldoutVoxelizeFolder;
        private bool foldoutVoxelizeFast;
        private bool toDag;

        private List<(JobHandle jh, object data, Action action)> backgroundJobs = new List<(JobHandle, object, Action)>();

        [DllImport("Converter", CallingConvention = CallingConvention.Cdecl)]
        private static extern void ConvertToDag(string svoPath, string dagPath, string modelName, int format, int depth = 999);
        
        
        [MenuItem("Window/DataCreator")]
        public static void ShowWindow()
        {
            GetWindow<DataCreatorWindow>("Data");
        }

        private void OnEnable()
        {
            EditorApplication.update += OnUpdateEditor;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnUpdateEditor;
        }

        private void OnUpdateEditor()
        {
            JobHandle.ScheduleBatchedJobs();
            for (int i = backgroundJobs.Count - 1; i >= 0; i--)
            {
                if (backgroundJobs[i].jh.IsCompleted)
                {
                    try
                    {
                        backgroundJobs[i].action?.Invoke();
                        backgroundJobs.RemoveAt(i);
                    }
                    catch (Exception)
                    {
                        backgroundJobs.RemoveAt(i);
                    }
                }
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("CONVERT MODEL TO OCT");
            EditorGUILayout.LabelField("____________________________________________");

            EditorGUILayout.BeginHorizontal();
            useColors = EditorGUILayout.Toggle("Use colors", useColors);
            deleteOctConverterFiles = EditorGUILayout.Toggle("Delete OctConverter files", deleteOctConverterFiles);
            EditorGUILayout.EndHorizontal();

            //VoxelizeFolderUI();
            ConvertToOctPlyUI();
            ConvertToOctConverterUI();
            ConvertToOctFastUI();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("____________________________________________");

            if (octInfo != null)
            {
                EditorGUILayout.LabelField("Selected OCT: " + octInfo.name);

                EditorGUILayout.BeginHorizontal();
                conversionDepthDag = EditorGUILayout.IntSlider("Depth", conversionDepthDag, 2, conversionMaxDepthDag);
                EditorGUILayout.LabelField($"({1 << conversionDepthDag})", GUILayout.Width(55));
                EditorGUILayout.EndHorizontal();

                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Override", GUILayout.Width(55));
                overrideFiles = EditorGUILayout.Toggle(overrideFiles, GUILayout.Width(15));
                dagFormat = (DagFormat)EditorGUILayout.EnumPopup(dagFormat);
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("Convert to DAG"))
                    ConvertToDAG(overrideFiles, dagFormat, conversionDepthDag);
                
                if (GUILayout.Button("Open in Folder"))
                    OpenFolder(AssetDatabase.GetAssetPath(octInfo.GetInstanceID()));
            }

            if (dagInfo != null)
            {
                EditorGUILayout.LabelField("Selected DAG: " + dagInfo.name);
                if (GUILayout.Button("Open in Folder"))
                {
                    OpenFolder(AssetDatabase.GetAssetPath(dagInfo.GetInstanceID()));
                }
            }
        }



        private void ConvertToOctPlyUI()
        {
            using (new FoldoutBlock(ref foldoutFromPly, "From .ply file"))
            {
                if (!foldoutFromPly) return;

                EditorGUILayout.BeginHorizontal();
                modelPathName = EditorGUILayout.TextField("Path to model", modelPathName);
                SelectFileDialog("ply");
                EditorGUILayout.EndHorizontal();

                gridDimension = EditorGUILayout.IntField("Grid Dimension", gridDimension);

                var exists = File.Exists(modelPathName + ".ply");
                if (!exists)
                {
                    GUI.contentColor = Color.red;
                    EditorGUILayout.LabelField($"File {modelPathName}.ply not found!");
                    GUI.contentColor = Color.white;

                }

                if (exists && GUILayout.Button("Convert to OCT"))
                    ConvertToOct();
                if (exists && GUILayout.Button("Convert Mesh"))
                    ConvertMesh(modelPathName, gridDimension);

            }
        }

        private void ConvertToOctConverterUI()
        {
            using (new FoldoutBlock(ref foldoutFromOctreeConverter, "From .octree file"))
            {
                if (!foldoutFromOctreeConverter) return;

                EditorGUILayout.BeginHorizontal();
                modelPathName = EditorGUILayout.TextField("Path to octree", modelPathName);
                SelectFileDialog("octree");
                EditorGUILayout.EndHorizontal();

                var exists = File.Exists(modelPathName + ".octree");
                if (!exists)
                {
                    GUI.contentColor = Color.red;
                    EditorGUILayout.LabelField($"File {modelPathName}.octree not found!");
                    GUI.contentColor = Color.white;
                }

                if (exists && GUILayout.Button("Convert to OCT"))
                {
                    var modelName = modelPathName.Substring(modelPathName.LastIndexOf("\\") + 1);

                    ConvertToOctChangeFormat(modelPathName, modelName, 0, useColors);
                    return;
                }
            }
        }

        public void VoxelizeFolderUI()
        {
            using (new FoldoutBlock(ref foldoutVoxelizeFolder, "Voxelize folder"))
            {
                if (!foldoutVoxelizeFolder) return;

                gridDimension = EditorGUILayout.IntField("Grid Dimension", gridDimension);
                useColors = EditorGUILayout.Toggle("Colors", useColors);
                toDag = EditorGUILayout.Toggle("To DAG?", toDag);

                EditorGUILayout.BeginHorizontal();
                folderPath = EditorGUILayout.TextField("input folder", folderPath);
                SelectFolderDialog(ref folderPath);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                outAssetPath = EditorGUILayout.TextField("Output assetpath", outAssetPath);
                SelectFolderDialog(ref outAssetPath);
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("Convert Folder"))
                {
                    VoxelizeFolder(folderPath, outAssetPath, gridDimension, toDag, useColors);
                }
            }
        }

        public void ConvertToOctFastUI()
        {
            using (new FoldoutBlock(ref foldoutVoxelizeFast, "From mesh"))
            {
                if (!foldoutVoxelizeFast) return;

                gridDimension = EditorGUILayout.IntField("Grid Dimension", gridDimension);
                obj = (GameObject) EditorGUILayout.ObjectField("Mesh", obj, typeof(GameObject));
                svoFormat = (SvoFormat) EditorGUILayout.EnumPopup("Format", svoFormat);

                if (GUILayout.Button("Convert To OCT"))
                {
                    ConvertToOctFast(obj, gridDimension, svoFormat);
                }
            }

           
        }

        public async void ConvertToOctFast(GameObject obj, int gridDimension, SvoFormat format)
        {
            var materials = obj.GetComponent<MeshRenderer>()?.sharedMaterials;
            var mesh = obj.GetComponent<MeshFilter>()?.sharedMesh;

            if (!mesh) throw new Exception($"No mesh found on obj {obj.name}");
            if (materials.Length == 1) materials = null;            //remove default material (this can infere with a textured model having possibly only one submesh)

            var useColors = format == SvoFormat.ColorFull || format == SvoFormat.Color;
            var outPath = Path.Combine(INFO_PATH, $"{mesh.name}_{gridDimension}{(useColors ? "_col" : "")}.oct");


            var sw1 = Stopwatch.StartNew();
            var maxDepth = Utils.Math.FastLog2(gridDimension);
            var vox = useColors ? 
                new BurstVoxelizerColor(mesh, maxDepth, materials) : 
               (IVoxelizer) new BurstVoxelizer(mesh, maxDepth);
            
            var jobHandle = vox.Voxelize();
            backgroundJobs.Add((jobHandle, vox, () =>
            {
                sw1.Stop();

                var sw2 = Stopwatch.StartNew();
                var writer = new BinaryWriter(File.Open(outPath, FileMode.Create));

                var builder = SvoInfo.GetBuilderForFormat((int) format);

                builder.WriteToFile(writer, vox);
                sw2.Stop();

                writer.Dispose();

                Debug.Log($"Wrote into: {outPath}");
                Debug.Log($"Voxelization: {sw1.ElapsedMilliseconds} ms \nIO: {sw2.ElapsedMilliseconds} ms\n");
                Utils.Log.LogVoxelizationTime(mesh.name, gridDimension, sw1.ElapsedMilliseconds, sw2.ElapsedMilliseconds, vox.VoxelCount, vox.NodeCount);

                AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceUpdate);
            }
            ));
        }

     

        public async void VoxelizeFolder(string folder, string outAssetFolder, int gridDimension, bool toDag, bool useColors)
        {
            var fs = Directory.GetFiles(folder, "*.ply");
            for (int i = 0; i < fs.Length; i++)
            {
                Debug.Log($"Voxelizing: {Path.GetFileName(fs[i])} ({i + 1} / {fs.Length})");

                var files = Directory.GetFiles(folder, Path.GetFileNameWithoutExtension(fs[i]) + "*_?.octree", SearchOption.TopDirectoryOnly);
                if (files != null && files.Length > 0) continue;

                //Check if dags already exist in destination directory
                files = Directory.GetFiles(outAssetFolder, Path.GetFileNameWithoutExtension(fs[i]) + "*_?.dag", SearchOption.TopDirectoryOnly);
                if (files != null && files.Length > 0) continue;

                await ConvertMesh(fs[i].Substring(0, fs[i].LastIndexOf('.')), gridDimension);
            }

            fs = Directory.GetFiles(folder, "*.octree");
            for (int i = 0; i < fs.Length; i++)
            {
                Debug.Log($"Converting format: {Path.GetFileName(fs[i])} ({i+1} / {fs.Length})");
                var filePathNameNoExt = fs[i].Substring(0, fs[i].LastIndexOf('.'));
                var fileNameNoExt = Path.GetFileNameWithoutExtension(fs[i]);

                //Terrain.000#0#0#4096_8_4096_col.oct
                var files = Directory.GetFiles(outAssetFolder, fileNameNoExt + ".oct", SearchOption.TopDirectoryOnly);
                if (files != null && files.Length > 0) continue;

                await ConvertToOctChangeFormat(filePathNameNoExt, outAssetFolder + "/" + fileNameNoExt, Path.GetFileNameWithoutExtension(fs[i]), gridDimension, useColors);
            }

            if (!toDag) return;

            fs = Directory.GetFiles(outAssetFolder, "*.oct");
            for (int i = 0; i < fs.Length; i++)
            {
                Debug.Log($"Converting to DAG: {Path.GetFileName(fs[i])} ({i + 1} / {fs.Length})");
                var fileNameNoExt = Path.GetFileNameWithoutExtension(fs[i]);

                var files = Directory.GetFiles(outAssetFolder, fileNameNoExt + ".dag", SearchOption.TopDirectoryOnly);
                if (files != null && files.Length > 0) continue;

                //await ConvertToDAG(fs[i], DagFormat.Gray);
            }
        }


        private async Task ConvertMesh(string modelPathName, int gridDimension, int memoryLimitMb = 16384)
        {
            int exitCode;

            //Convert .ply to .tri
            if (!File.Exists(modelPathName + ".tri"))
            {
                var triBuilder = System.Diagnostics.Process.Start(TRI_BUILDER_PATH, $"-f {modelPathName}.ply");
                exitCode = await triBuilder;
            }

            //Voxelize and build svo
            var svoBuilder = System.Diagnostics.Process.Start(SVO_BUILDER_PATH, $"-f {modelPathName}.tri -l {memoryLimitMb} -v -s {gridDimension}");
            exitCode = await svoBuilder;
        }

        

        private async void ConvertToOct()
        {
            int exitCode;

            //Convert .ply to .tri
            if (!File.Exists(modelPathName + ".tri"))
            {
                var triBuilder = System.Diagnostics.Process.Start(TRI_BUILDER_PATH, $"-f {modelPathName}.ply");
                exitCode = await triBuilder;
            }

            //Voxelize and build svo
            var svoBuilder = System.Diagnostics.Process.Start(SVO_BUILDER_PATH, $"-f {modelPathName}.tri -l 16384 -v -s {gridDimension}");
            exitCode = await svoBuilder;

            var modelOutputPath = modelPathName.Substring(0, modelPathName.LastIndexOf("\\"));
            var modelName = modelPathName.Substring(modelPathName.LastIndexOf("\\") + 1);

            //Convert to own format
            var files = Directory.GetFiles(modelOutputPath, modelName + "*.octree");
            if (files.Length == 0) throw new System.Exception($"Octree data not found in path {modelOutputPath}. Make sure it has been properly generated");
            var filePath = files[0].Split('.')[0];

            await ConvertToOctChangeFormat(filePath, modelName, gridDimension, useColors);
        }

        /// <summary>
        /// Converts voxel generator format to own format
        /// </summary>
        /// <param name="filePath">Path to .octree file without ending</param>
        /// <returns></returns>
        private async Task ConvertToOctChangeFormat(string filePath, string modelName, int gridDimension, bool useColors)
        {
            //Create reference holder for unity
            var fileName = $"{modelName}_{gridDimension}{(useColors ? "_col" : "")}.oct";

            var assetPath = Path.Combine(INFO_PATH, fileName);
            await Task.Run(() => SvoInfo.ConvertFormatForceFlow2Own(filePath, assetPath, useColors));

            if (deleteOctConverterFiles)
            {
                File.Delete($"{filePath}.octree");
                File.Delete($"{filePath}.octreenodes");
                File.Delete($"{filePath}.octreedata");
            }

            //Reference inside unity
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        private async Task ConvertToOctChangeFormat(string filePathNoExt, string outFilePathNoExt, string modelName, int gridDimension, bool useColors)
        {
            //Create reference holder for unity
            await Task.Run(() => SvoInfo.ConvertFormatForceFlow2Own(filePathNoExt, outFilePathNoExt + ".oct", useColors));

            if (deleteOctConverterFiles)
            {
                File.Delete($"{filePathNoExt}.octree");
                File.Delete($"{filePathNoExt}.octreenodes");
                File.Delete($"{filePathNoExt}.octreedata");
            }
        }

        private async void ConvertToDAG(bool overrideFiles, DagFormat format, int depth)
        {
            if (format == DagFormat.ColorPerPointer)
            {
                var fileName = octInfo.name.Split('.')[0];
                IDagBuilder dag = DagInfo.GetBuilderForFormat((int)format);
                
                var sw1 = System.Diagnostics.Stopwatch.StartNew();
                await Task.Run(() => dag.ConstructDagMemory(octInfo));
                sw1.Stop();
                dag.PrintLevels();
                
                var dagPath = Path.Combine(INFO_PATH, fileName + "_" + format);
                
                var sw2 = System.Diagnostics.Stopwatch.StartNew();
                await Task.Run(() => DagInfo.WriteToFile(dagPath, dag, overrideFiles)).ConfigureAwait(true);
                
                Debug.Log($"Conversion took: {sw1.ElapsedMilliseconds} ms\n IO: {sw2.ElapsedMilliseconds} ms");
                Log.LogConversionTime(fileName, format, 1 << dag.MaxDepth, sw1.ElapsedMilliseconds, sw2.ElapsedMilliseconds);
            }
            else
            {
                var sw3 = System.Diagnostics.Stopwatch.StartNew();
            
                Debug.Log($"Starting conversion...");
                var fileName = octInfo.name.Split('.')[0].Split('_')[0];
                await Task.Run(() => ConvertToDag(octInfo.Path, INFO_PATH, fileName, (int) format, depth));
            
                var dagPath = Path.Combine(INFO_PATH, fileName + "_" + (1<<depth) + "_" +format);
                AssetDatabase.ImportAsset(dagPath + ".dag", ImportAssetOptions.ForceUpdate);
            
                Debug.Log($"Conversion took: {sw3.ElapsedMilliseconds} ms\n");
                Log.LogConversionTime(fileName, format, 1 << octInfo.GetMaxDepth(), sw3.ElapsedMilliseconds, 0);
            }
        }

        private void OpenFolder(string assetPath)
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), assetPath.Replace("/", "\\"));
            System.Diagnostics.Process.Start("explorer.exe", "/select," + path);
        }

        private void SelectFileDialog(string fileType)
        {
            if (GUILayout.Button("Select File"))
            {
                modelPathName = EditorUtility.OpenFilePanel("Select file", modelPathName, fileType).Split('.')[0].Replace('/', '\\');
            }
        }

        private void SelectFolderDialog(ref string folderPath)
        {
            if (GUILayout.Button("Select Folder"))
            {
                folderPath = EditorUtility.OpenFolderPanel("Select folder to voxelize", folderPath, "");
            }
        }

        private void OnSelectionChange()
        {
            if (Selection.activeObject is RenderInfo)
            {
                if (Selection.assetGUIDs.Length > 0)
                {
                    if (Selection.activeObject is SvoInfo oct)
                    {
                        octInfo = oct;
                        dagInfo = null;
                        
                        this.conversionMaxDepthDag = octInfo.GetMaxDepth();
                        this.conversionDepthDag = octInfo.GetMaxDepth();
                    }
                    else if (Selection.activeObject is DagInfo dag)
                    {
                        dagInfo = dag;
                        octInfo = null;
                    }
                }
            }
            
            Repaint();
        }
    }
}