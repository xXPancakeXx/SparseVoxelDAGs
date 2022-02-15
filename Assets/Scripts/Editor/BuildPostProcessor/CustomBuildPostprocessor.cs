using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Assets.Scripts.Editor.BuildPostProcessor
{
    public static class CustomBuildPostprocessor
    {
        [PostProcessBuild(1)]
        public static void OnPostprocessBuild(BuildTarget target, string pathToExeFile)
        {
            // Debug.Log("Copy requested");
            // Debug.Log($"Trying to copy from {DataCreatorWindow.INFO_PATH} to {pathToExeFile}");
            //
            // var destPath = Path.Combine(Path.GetDirectoryName(pathToExeFile), DataCreatorWindow.INFO_PATH);
            // if (!Directory.Exists(destPath)) Directory.CreateDirectory(destPath);
            //
            // //Delete everything in folder
            // // System.IO.DirectoryInfo di = new DirectoryInfo(destPath);
            // // foreach (FileInfo file in di.GetFiles())
            // // {
            // //     file.Delete(); 
            // // }
            // // foreach (DirectoryInfo dir in di.GetDirectories())
            // // {
            // //     dir.Delete(true); 
            // // }
            // // foreach (var data in ScriptableObject.FindObjectOfType<DataInfo>().data)
            // // {
            // //     if (data.dag != null) FileUtil.CopyFileOrDirectory(data.dag.Path, destPath);
            // //     if (data.oct != null) FileUtil.CopyFileOrDirectory(data.oct.Path, destPath);
            // // };
            //
            // FileUtil.ReplaceDirectory(DataCreatorWindow.INFO_PATH, destPath);
            // Debug.Log($"Copied to {pathToExeFile}");
        }
    }
}