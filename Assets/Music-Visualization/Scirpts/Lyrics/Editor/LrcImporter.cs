using UnityEngine;
using System.IO;

#if UNITY_EDITOR
[UnityEditor.AssetImporters.ScriptedImporter(1, "lrc")]
public class LrcImporter : UnityEditor.AssetImporters.ScriptedImporter
{
    public override void OnImportAsset(UnityEditor.AssetImporters.AssetImportContext ctx)
    {
        string fileContent = File.ReadAllText(ctx.assetPath);
        
        TextAsset textAsset = new TextAsset(fileContent);
        textAsset.name = Path.GetFileNameWithoutExtension(ctx.assetPath);
        
        ctx.AddObjectToAsset("main", textAsset);
        ctx.SetMainObject(textAsset);
    }
}
#endif