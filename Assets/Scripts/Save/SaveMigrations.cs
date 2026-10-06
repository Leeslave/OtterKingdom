using System;
using System.Collections.Generic;

/// <summary>
/// 옛 세이브를 지금 형식으로 올리는 단계 목록 (상세기획서 11.4 버전 관리).
/// 단계마다 "이 버전보다 낮은 세이브에 적용"을 적고, 낮은 것부터 차례로 돈다. 다 돌면 schemaVersion = 지금 버전.
/// 새 단계를 넣을 때: SaveData.CurrentSchemaVersion을 올리고 그 번호로 여기에 한 줄 추가.
/// 세이브 값만 보고 고친다 (불러온 직후, 게임이 읽기 전에 GameManager가 한 번 부름).
/// </summary>
public static class SaveMigrations
{
    private readonly struct Step
    {
        public readonly int Before;
        public readonly string Name;
        public readonly Action<SaveData> Apply;

        public Step(int before, string name, Action<SaveData> apply)
        {
            Before = before;
            Name = name;
            Apply = apply;
        }
    }

    private static readonly Step[] Steps =
    {
        // 2: 첫 심기 안내가 생김. 이미 심었거나 판 적 있는 세이브는 안내가 필요 없음
        new Step(2, "first-plant guide", save =>
        {
            if (HasPlantedAnything(save) || save.lifetimeSales > 0)
                save.firstPlantGuideDone = true;
        }),
        // 3: 장소 튜토리얼이 생김. 그 전 세이브는 모든 장소·기능 안내를 건너뜀
        new Step(3, "zone tutorials", save =>
        {
            AddMissing(save.tutorialsDone, ZoneTutorials.AllIds);
            AddMissing(save.tutorialsDone, ZoneTutorials.FeatureIds);
        }),
        // 4: 정착 진행이 생김. 그 전 세이브는 게시판 부탁을 모두 끝낸 것으로 (쓰던 밭이 다시 잠기지 않게)
        new Step(4, "legacy settlement", save =>
        {
            if (!save.settlement.initialized)
                save.settlement.legacyComplete = true;
        }),
    };

    /// <summary>적용한 단계 이름 목록 (이미 지금 버전이면 비어 있음)</summary>
    public static List<string> Run(SaveData save)
    {
        if (save == null)
            throw new ArgumentNullException(nameof(save));

        var applied = new List<string>();
        if (save.schemaVersion >= SaveData.CurrentSchemaVersion)
            return applied;

        save.tutorialsDone ??= new List<string>();
        save.settlement ??= new SettlementSaveData();
        foreach (var step in Steps)
        {
            if (save.schemaVersion >= step.Before)
                continue;
            step.Apply(save);
            applied.Add(step.Name);
        }
        save.schemaVersion = SaveData.CurrentSchemaVersion;
        return applied;
    }

    private static bool HasPlantedAnything(SaveData save)
    {
        if (save.plots == null)
            return false;
        foreach (var plot in save.plots)
        {
            if (plot?.slots == null)
                continue;
            foreach (var slot in plot.slots)
            {
                if (slot != null && slot.state != FurrowSlotState.Empty)
                    return true;
            }
        }
        return false;
    }

    private static void AddMissing(List<string> list, IEnumerable<string> ids)
    {
        foreach (var id in ids)
        {
            if (!list.Contains(id))
                list.Add(id);
        }
    }
}
