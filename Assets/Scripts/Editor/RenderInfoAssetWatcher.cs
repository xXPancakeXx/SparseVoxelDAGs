using Assets.Scripts.Octree;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Editor
{
    public class RenderInfoAssetWatcher : UnityEditor.AssetModificationProcessor
    {
        public static AssetDeleteResult OnWillDeleteAsset(string path, RemoveAssetOptions opt)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (asset is RenderInfo ri)
            {
                var dataPath = ri.Path;
                dataPath += ri is SvoInfo ? ".octree" : ".dag";

                if (File.Exists(dataPath))
                {
                    File.Delete(dataPath);
                    Debug.Log($"Deleted file {dataPath}");
                }
            }


            return AssetDeleteResult.DidNotDelete;
        }


        public static AssetMoveResult OnWillMoveAsset(string sourcePath, string destinationPath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Object>(sourcePath);
            if (asset is RenderInfo ri)
            {
                ri.Path = destinationPath;
                EditorUtility.SetDirty(ri);
            }

            return AssetMoveResult.DidNotMove;
        }
    }
}