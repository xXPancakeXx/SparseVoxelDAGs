using System;
using System.Collections.Generic;
using System.IO;
using Assets.Scripts.Dag;
using Assets.Scripts.Octree;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Assets.Scripts
{
    [CreateAssetMenu(menuName = "Data Info")]
    [Serializable]
    public class DataInfo : ScriptableObject
    {
        public const string INFO_PATH = @"Assets\Data\";
        
        [Serializable]
        public class Data
        {
            public SvoInfo oct;
            public SvoInfo colOct;
            
            [Space]
            public DagInfo dag;
            public DagInfo colDag;

            [Space] 
            public GameObject mesh;
        }
    
        public List<Data> data;

#if UNITY_EDITOR
        [ContextMenu("Copy Data To Folder")]
        public void CopyToBuildFolder()
        {
            string folder = @"D:\Bsc\SparseVoxelDAGs\build\ObjectRendering";
            SelectFolderDialog(ref folder);
            
            var destPath = folder.Replace(@"\", "/");
            
            if (!Directory.Exists(Path.Combine(destPath, INFO_PATH))) Directory.CreateDirectory(Path.Combine(destPath, INFO_PATH));

            //Delete everything in folder
            System.IO.DirectoryInfo di = new DirectoryInfo(Path.Combine(destPath, INFO_PATH));
            foreach (FileInfo file in di.GetFiles())
            {
                file.Delete(); 
            }
            foreach (DirectoryInfo dir in di.GetDirectories())
            {
                dir.Delete(true); 
            }
            
            foreach (var d in data)
            {
                if (d.dag != null) File.Copy(d.dag.Path, Path.Combine(destPath, d.dag.Path));
                if (d.oct != null) File.Copy(d.oct.Path, Path.Combine(destPath, d.oct.Path));
                if (d.colOct != null) File.Copy(d.colOct.Path, Path.Combine(destPath, d.colOct.Path));
                if (d.colDag != null) File.Copy(d.colDag.Path, Path.Combine(destPath, d.colDag.Path));
            };
        }
        
        private void SelectFolderDialog(ref string folderPath)
        {
            folderPath = EditorUtility.OpenFolderPanel("Select destination folder", folderPath, "");
        }
#endif
    }
}
