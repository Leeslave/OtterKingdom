using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>구역 블록이 커지는 한 단계: 이 발전이 열리면 이 칸들이 블록에 들어온다 (그 방향 영토 확장 한 단계)</summary>
[Serializable]
public class DistrictStage
{
    [Tooltip("이 단계가 열리는 발전 (비우면 처음부터, 예: territory_ne_1)")]
    [SerializeField] private string _development;

    [Tooltip("이 단계에 들어오는 칸 (세이브 좌표, 여러 직사각형)")]
    [SerializeField] private List<RectInt> _areas = new List<RectInt>();

    public string Development => _development;
    public IReadOnlyList<RectInt> Areas => _areas;

    public DistrictStage() { }

    public DistrictStage(string development, IEnumerable<RectInt> areas)
    {
        _development = development;
        _areas = areas != null ? new List<RectInt>(areas) : new List<RectInt>();
    }

    public bool IsOpen(Func<string, bool> hasDevelopment) =>
        string.IsNullOrEmpty(_development) || (hasDevelopment != null && hasDevelopment(_development));
}

/// <summary>구역 블록 안에 미리 정해 둔 부지 칸 하나. 건물은 부지 칸 안에 놓인다 (이사 · 구역 건물)</summary>
[Serializable]
public class DistrictSlot
{
    [Tooltip("세이브에 남는 ID (예: district_northeast_01). 저장 데이터가 생긴 뒤에는 바꾸지 않는다")]
    [SerializeField] private string _slotId;

    [Tooltip("차지하는 칸 (광장 꾸미기 격자의 세이브 좌표 = 칸 − 격자 기준 칸, 왼쪽 아래 기준)")]
    [SerializeField] private RectInt _area;

    [Tooltip("이 칸이 쓰이기 시작하는 블록 단계 (0 = 첫 단계)")]
    [Min(0)]
    [SerializeField] private int _stage;

    public string SlotId => _slotId;
    public RectInt Area => _area;
    public int Stage => Mathf.Max(0, _stage);

    public DistrictSlot() { }

    public DistrictSlot(string slotId, RectInt area, int stage = 0)
    {
        _slotId = slotId;
        _area = area;
        _stage = stage;
    }
}

/// <summary>
/// 구역 후보 블록 하나 (구역·이사 기획). 플레이어는 땅을 칠하지 않고, 지도의 길 사이에 미리 정해 둔 블록에 용도만 고른다.
/// 블록은 작게 시작해 그 방향의 영토를 넓힐 때마다 커진다 (단계마다 칸 · 부지 칸이 열림 = 구역 최대 크기).
/// 첫 단계가 열리면 용도를 고를 수 있고, 안에 미리 정해 둔 부지 칸이 있어 어떻게 놓아도 정돈돼 보인다.
/// 중앙 공터처럼 용도가 정해진 블록은 고르지 않는다 (_fixedType).
/// 좌표는 모두 광장 꾸미기 격자의 세이브 좌표 (ConstructionPlotDefinition 기본 칸과 같은 기준). 칸 · 부지 칸은 지도 도구가 만든다
/// (Tools/MapExpansion/build_territory_art.py → territory_layout.json의 districts)
/// </summary>
[CreateAssetMenu(fileName = "District", menuName = "Game Data/Settlement/District Block")]
public class DistrictBlockDefinition : ScriptableObject
{
    [Header("식별")]
    [Tooltip("세이브에 남는 ID (예: district_northeast). 저장 데이터가 생긴 뒤에는 바꾸지 않는다")]
    [SerializeField] private string _blockId;

    [Tooltip("땅 이름 (예: 북동쪽 언덕)")]
    [SerializeField] private string _displayName;

    [Header("용도")]
    [Tooltip("정해진 용도 (중앙 공터 = 중앙). None이면 플레이어가 고름")]
    [SerializeField] private DistrictType _fixedType;

    [Tooltip("지도 배치안의 예시 용도. 고르기 화면의 추천 · 옛 세이브 자동 배치에 씀")]
    [SerializeField] private DistrictType _suggestedType;

    [Header("칸")]
    [Tooltip("블록이 커지는 단계 (차례대로). 첫 단계가 열리면 용도를 고를 수 있음")]
    [SerializeField] private List<DistrictStage> _stages = new List<DistrictStage>();

    [Tooltip("미리 정해 둔 부지 칸")]
    [SerializeField] private List<DistrictSlot> _slots = new List<DistrictSlot>();

    public string BlockId => _blockId;
    public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;
    public DistrictType FixedType => _fixedType;
    public DistrictType SuggestedType => _fixedType != DistrictType.None ? _fixedType : _suggestedType;
    public bool IsFixed => _fixedType != DistrictType.None;
    public IReadOnlyList<DistrictStage> Stages => _stages;
    public IReadOnlyList<DistrictSlot> Slots => _slots;

    /// <summary>블록이 열리는 발전 (첫 단계, 비우면 처음부터)</summary>
    public string RequiredDevelopment => _stages.Count > 0 && _stages[0] != null ? _stages[0].Development : null;

    /// <summary>열린 단계 수 (차례대로 — 앞 단계가 닫혀 있으면 뒤는 세지 않음)</summary>
    public int OpenStages(Func<string, bool> hasDevelopment)
    {
        int open = 0;
        foreach (var stage in _stages)
        {
            if (stage == null || !stage.IsOpen(hasDevelopment))
                break;
            open++;
        }
        return open;
    }

    /// <summary>이 칸이 블록 안인지 (stages = 앞에서부터 볼 단계 수, 음수면 모든 단계)</summary>
    public bool ContainsCell(Vector2Int cell, int stages = -1)
    {
        int count = stages < 0 ? _stages.Count : Mathf.Min(stages, _stages.Count);
        for (int i = 0; i < count; i++)
        {
            var stage = _stages[i];
            if (stage == null)
                continue;
            foreach (var area in stage.Areas)
            {
                if (area.Contains(cell))
                    return true;
            }
        }
        return false;
    }

    /// <summary>이 영역(세이브 좌표)이 블록 안(앞 stages 단계, 음수면 모든 단계)에 다 들어 있는지</summary>
    public bool Contains(RectInt area, int stages = -1)
    {
        if (area.width <= 0 || area.height <= 0)
            return false;
        for (int x = area.xMin; x < area.xMax; x++)
        {
            for (int y = area.yMin; y < area.yMax; y++)
            {
                if (!ContainsCell(new Vector2Int(x, y), stages))
                    return false;
            }
        }
        return true;
    }

    /// <summary>모든 단계의 칸을 감싸는 직사각형</summary>
    public RectInt Bounds
    {
        get
        {
            bool any = false;
            int xMin = 0, yMin = 0, xMax = 0, yMax = 0;
            foreach (var stage in _stages)
            {
                if (stage == null)
                    continue;
                foreach (var area in stage.Areas)
                {
                    xMin = any ? Mathf.Min(xMin, area.xMin) : area.xMin;
                    yMin = any ? Mathf.Min(yMin, area.yMin) : area.yMin;
                    xMax = any ? Mathf.Max(xMax, area.xMax) : area.xMax;
                    yMax = any ? Mathf.Max(yMax, area.yMax) : area.yMax;
                    any = true;
                }
            }
            return any ? new RectInt(xMin, yMin, xMax - xMin, yMax - yMin) : default;
        }
    }

    public DistrictSlot FindSlot(string slotId)
    {
        if (string.IsNullOrEmpty(slotId))
            return null;
        foreach (var slot in _slots)
        {
            if (slot != null && slot.SlotId == slotId)
                return slot;
        }
        return null;
    }

    /// <summary>테스트·설정 도구용</summary>
    public void Setup(string blockId, string displayName, DistrictType fixedType, DistrictType suggestedType,
        IEnumerable<DistrictStage> stages, IEnumerable<DistrictSlot> slots)
    {
        _blockId = blockId;
        _displayName = displayName;
        _fixedType = fixedType;
        _suggestedType = suggestedType;
        _stages = stages != null ? new List<DistrictStage>(stages) : new List<DistrictStage>();
        _slots = slots != null ? new List<DistrictSlot>(slots) : new List<DistrictSlot>();
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(_blockId))
            Debug.LogWarning($"[{name}] BlockId가 비어 있습니다.", this);
        if (_fixedType != DistrictType.None && _fixedType != DistrictType.Central && !Districts.IsAssignable(_fixedType))
            Debug.LogWarning($"[{name}] 정해진 용도가 올바르지 않습니다.", this);
        foreach (var slot in _slots)
        {
            if (slot != null && !Contains(slot.Area, slot.Stage + 1))
                Debug.LogWarning($"[{name}] 부지 칸 '{slot.SlotId}'이 {slot.Stage + 1}단계까지의 블록 밖으로 나갔습니다.", this);
        }
    }
}
