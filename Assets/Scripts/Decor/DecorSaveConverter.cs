using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>세이브 한 줄: 놓인 물건 하나 (배치 개체 ID, 물건 종류, 위치, 방향)</summary>
[Serializable]
public class PlacedDecorSaveEntry
{
    public int instanceId;
    public string itemId;
    public int x;
    public int y;
    public DecorRotation rotation;

    public PlacedDecorSaveEntry() { }

    public PlacedDecorSaveEntry(int instanceId, string itemId, int x, int y, DecorRotation rotation)
    {
        this.instanceId = instanceId;
        this.itemId = itemId;
        this.x = x;
        this.y = y;
        this.rotation = rotation;
    }
}

/// <summary>격자 하나(장소 하나)의 세이브: 놓인 물건, 열린 구역</summary>
[Serializable]
public class DecorBoardSaveData
{
    public string boardId;
    public List<PlacedDecorSaveEntry> placed = new List<PlacedDecorSaveEntry>();
    public List<string> unlockedRegions = new List<string>();
}

/// <summary>꾸미기 세이브 전체 (JsonUtility용 리스트만 사용)</summary>
[Serializable]
public class DecorSaveData
{
    public List<DecorBoardSaveData> boards = new List<DecorBoardSaveData>();
}

/// <summary>
/// DecorLayout(격자 하나) ↔ 세이브 형식 변환. 모델이 세이브 타입을 모르도록 여기서만 한다.
/// </summary>
public static class DecorSaveConverter
{
    public static void Write(DecorLayout layout, DecorBoardSaveData result)
    {
        if (layout == null) throw new ArgumentNullException(nameof(layout));
        if (result == null) throw new ArgumentNullException(nameof(result));

        result.placed.Clear();
        var placed = new List<PlacedDecor>(layout.Placed);
        placed.Sort((a, b) => a.InstanceId.CompareTo(b.InstanceId));
        foreach (var p in placed)
            result.placed.Add(new PlacedDecorSaveEntry(p.InstanceId, p.Decor.ItemId, p.Origin.x, p.Origin.y, p.Rotation));

        result.unlockedRegions.Clear();
        result.unlockedRegions.AddRange(layout.UnlockedRegions);
        result.unlockedRegions.Sort(string.CompareOrdinal);
    }

    /// <summary>
    /// 저장된 구역·물건을 넣는다. 구역을 먼저 열어야 그 구역의 물건을 놓을 수 있다.
    /// 없는 물건이나 자리가 맞지 않는 물건은 건너뛴다 (보관함으로 돌아간 것처럼 됨)
    /// </summary>
    /// <returns>건너뛴 물건 수</returns>
    public static int Read(DecorBoardSaveData saved, DecorLayout layout, DecorCatalog catalog)
    {
        if (saved == null) throw new ArgumentNullException(nameof(saved));
        if (layout == null) throw new ArgumentNullException(nameof(layout));
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));

        if (saved.unlockedRegions != null)
        {
            foreach (var regionId in saved.unlockedRegions)
                layout.LoadUnlockedRegion(regionId);
        }

        int skipped = 0;
        if (saved.placed == null)
            return skipped;

        foreach (var line in saved.placed)
        {
            bool loaded = line != null
                && !string.IsNullOrEmpty(line.itemId)
                && Enum.IsDefined(typeof(DecorRotation), line.rotation)
                && catalog.TryGetDecor(line.itemId, out var decor)
                && layout.LoadPlaced(line.instanceId, decor, new Vector2Int(line.x, line.y), line.rotation);
            if (!loaded)
                skipped++;
        }
        return skipped;
    }
}
