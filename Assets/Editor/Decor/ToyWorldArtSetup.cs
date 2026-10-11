using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 장난감의 배치용 그림(광장에 놓였을 때)을 장난감 데이터에 연결한다. 아이콘(가방 · 뽑기 카드)은 그대로 둔다.
/// 그림은 Assets/Art/Decor/World/WORLD_Toy_[이름].png (이름 = 아이콘 ICON_Toy_[이름]과 같음). 넣는 방법은 Tools/UIGen/toy_world_art.py.
/// 배치용 그림은 위에서 비스듬히 본 모습이라 돌리면 어색하므로 돌리기를 끈다 (가구와 같음).
/// 그림이 아직 없는 장난감은 아이콘을 그대로 쓴다.
/// 배치 모드: -executeMethod ToyWorldArtSetup.Apply
/// </summary>
public static class ToyWorldArtSetup
{
    private const string DecorFolder = "Assets/Scriptable Obejects/Decor/Decors";
    private const string WorldFolder = "Assets/Art/Decor/World";
    private const string IconPrefix = "ICON_Toy_";
    private const string WorldPrefix = "WORLD_Toy_";

    [MenuItem("Tools/Decor/Apply Toy World Art")]
    public static void Apply()
    {
        var linked = new List<string>();
        var missing = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:DecorDefinition", new[] { DecorFolder }))
        {
            var decor = AssetDatabase.LoadAssetAtPath<DecorDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (decor == null || decor.IsFurniture || decor.IsBuilding || decor.Item == null || decor.Item.Icon == null)
                continue;
            string iconName = decor.Item.Icon.name;
            if (!iconName.StartsWith(IconPrefix))
                continue;
            string toy = iconName.Substring(IconPrefix.Length);
            string path = $"{WorldFolder}/{WorldPrefix}{toy}.png";
            if (!System.IO.File.Exists(path))
            {
                missing.Add(toy);
                continue;
            }

            var sprite = CollectionSetup.ImportSprite(WorldFolder, WorldPrefix + toy);
            var so = new SerializedObject(decor);
            so.FindProperty("_worldSprite").objectReferenceValue = sprite;
            so.FindProperty("_canRotate").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(decor);
            linked.Add(toy);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[ToyWorldArtSetup] 배치용 그림 연결 {linked.Count}개: {string.Join(", ", linked)}"
            + (missing.Count > 0 ? $"\n아직 그림이 없어 아이콘을 쓰는 장난감 {missing.Count}개: {string.Join(", ", missing)}" : string.Empty));
    }
}
