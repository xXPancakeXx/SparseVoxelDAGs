using UnityEngine;
using UnityEditor.AssetImporters;
using Assets.Scripts.Octree;

[ScriptedImporter(1, "oct")]
public class SvoImporter : ScriptedImporter
{
    public override void OnImportAsset(AssetImportContext ctx)
    {
        var path = ctx.assetPath;
        var svoInfo = ScriptableObject.CreateInstance<SvoInfo>();
        svoInfo.Path = path;

        // (Only the 'Main Asset' is eligible to become a Prefab.)
        ctx.AddObjectToAsset("main obj", svoInfo);
        ctx.SetMainObject(svoInfo);
        
        // Assets that are not passed into the context as import outputs must be destroyed
        //var tempMesh = new Mesh();
        //DestroyImmediate(tempMesh);
    }
}