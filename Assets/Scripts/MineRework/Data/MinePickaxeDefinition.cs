using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>재료 하나 (광석 · 개수)</summary>
[Serializable]
public class MineCost
{
    [Tooltip("광석")]
    [SerializeField] private MineOreDefinition _ore;
    [Tooltip("개수")]
    [SerializeField] private int _amount = 1;

    public MineCost() { }

    public MineCost(MineOreDefinition ore, int amount)
    {
        _ore = ore;
        _amount = amount;
    }

    public MineOreDefinition Ore => _ore;
    public int Amount => Mathf.Max(0, _amount);
}

/// <summary>
/// 곡괭이 한 단계 (조약돌 → 조개 구리 → … → 별빛 다이아). 광석 + 골드로 만들고, 만들면 다음 층이 열린다.
/// 힘이 셀수록 바위를 적게 쳐도 깨지고, 광부가 빨리 캔다. 단계마다 날 갈기(골드)로 치명타를 올린다
/// </summary>
[CreateAssetMenu(menuName = "OtterKingdom/Mine Rework/Pickaxe", fileName = "Pickaxe_")]
public class MinePickaxeDefinition : ScriptableObject
{
    [Header("이름")]
    [Tooltip("보이는 이름 (예: 조개 구리 곡괭이)")]
    [SerializeField] private string _displayName;
    [Tooltip("한 줄 소개")]
    [SerializeField] private string _description;
    [Tooltip("아이콘")]
    [SerializeField] private Sprite _icon;

    [Header("만들기")]
    [Tooltip("재료 광석 (처음 곡괭이는 비움)")]
    [SerializeField] private List<MineCost> _recipe = new List<MineCost>();
    [Tooltip("만드는 데 드는 골드")]
    [SerializeField] private int _goldCost;

    [Header("성능")]
    [Tooltip("한 번 칠 때 깎는 바위 체력")]
    [SerializeField] private int _power = 1;
    [Tooltip("광부가 캐는 간격 줄임 (0.1 = 10% 빨리)")]
    [SerializeField, Range(0f, 0.9f)] private float _minerSpeedBonus;
    [Tooltip("희귀 광석 확률 더하기 (0.02 = +2%p)")]
    [SerializeField, Range(0f, 1f)] private float _rareBonus;

    [Header("날 갈기")]
    [Tooltip("날 갈기 비용 (골드, 순서대로 +1 · +2 …)")]
    [SerializeField] private List<int> _sharpenCosts = new List<int>();

    public string DisplayName => _displayName;
    public string Description => _description;
    public Sprite Icon => _icon;
    public IReadOnlyList<MineCost> Recipe => _recipe;
    public int GoldCost => Mathf.Max(0, _goldCost);
    public int Power => Mathf.Max(1, _power);
    public float MinerSpeedBonus => _minerSpeedBonus;
    public float RareBonus => _rareBonus;
    public IReadOnlyList<int> SharpenCosts => _sharpenCosts;
    public int MaxSharpen => _sharpenCosts.Count;
}
