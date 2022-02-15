using UnityEngine;
using UnityEditor.AssetImporters;
using Assets.Scripts.Octree;
using Assets.Scripts.Dag;

[ScriptedImporter(1, "dag")]
public class DagImporter : ScriptedImporter
{
    public override void OnImportAsset(AssetImportContext ctx)
    {
        var path = ctx.assetPath;
        var info = ScriptableObject.CreateInstance<DagInfo>();
        info.Path = path;

        // (Only the 'Main Asset' is eligible to become a Prefab.)
        ctx.AddObjectToAsset("main obj", info);
        ctx.SetMainObject(info);
        
        // Assets that are not passed into the context as import outputs must be destroyed
        //var tempMesh = new Mesh();
        //DestroyImmediate(tempMesh);
    }
}