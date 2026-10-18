using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>나올 광석 하나와 무게 (무게가 클수록 자주)</summary>
[Serializable]
public class MineDrop
{
    [Tooltip("광석")]
    [SerializeField] private MineOreDefinition _ore;
    [Tooltip("무게 (같은 목록 안에서 비율)")]
    [SerializeField] private int _weight = 1;

    public MineDrop() { }

    public MineDrop(MineOreDefinition ore, int weight)
    {
        _ore = ore;
        _weight = weight;
    }

    public MineOreDefinition Ore => _ore;
    public int Weight => Mathf.Max(0, _weight);
}

/// <summary>
/// 광산 한 층 (조약돌 굴 → 용궁 보석방). 바위마다 박히는 광맥 종류, 깨면 덤으로 나오는 희귀 광석, 화석, 바위 단단함, 색감.
/// 열리려면 곡괭이 단계가 필요하고, 처음 갈 때 막힌 길(큰 바위)을 깨야 한다
/// </summary>
[CreateAssetMenu(menuName = "OtterKingdom/Mine Rework/Layer", fileName = "Layer_")]
public class MineLayerDefinition : ScriptableObject
{
    [Header("이름")]
    [Tooltip("층 이름 (예: 조약돌 굴)")]
    [SerializeField] private string _displayName;
    [Tooltip("한 줄 소개")]
    [SerializeField] private string _subtitle;

    [Header("열림")]
    [Tooltip("이 층에 가려면 필요한 곡괭이 단계 (0 = 처음 곡괭이)")]
    [SerializeField] private int _requiredPickaxeTier;

    [Header("바위")]
    [Tooltip("바위 체력 (곡괭이 힘만큼 깎임 → 칠 횟수 = 체력 ÷ 힘)")]
    [SerializeField] private int _rockHp = 4;
    [Tooltip("바위에 박히는 광맥 (돌이면 그냥 바위). 깨면 이 광석이 2~3개")]
    [SerializeField] private List<MineDrop> _veins = new List<MineDrop>();
    [Tooltip("깨면 가끔 덤으로 나오는 희귀 광석 (반짝 광맥은 반드시)")]
    [SerializeField] private List<MineDrop> _rareDrops = new List<MineDrop>();
    [Tooltip("희귀 광석이 나올 확률 (0~1, 바위 하나에)")]
    [SerializeField, Range(0f, 1f)] private float _rareChance = 0.05f;
    [Tooltip("이 층의 화석 · 유물 (한 번만 나옴)")]
    [SerializeField] private MineOreDefinition _relic;
    [Tooltip("화석이 나올 확률 (0~1, 바위 하나에)")]
    [SerializeField, Range(0f, 1f)] private float _relicChance = 0.015f;

    [Header("색감")]
    [Tooltip("이 층 배경 그림 (비우면 처음 배경)")]
    [SerializeField] private Sprite _background;
    [Tooltip("배경에 곱할 색")]
    [SerializeField] private Color _backgroundTint = Color.white;
    [Tooltip("바위에 곱할 색")]
    [SerializeField] private Color _rockTint = Color.white;
    [Tooltip("떠다니는 빛가루 색")]
    [SerializeField] private Color _moteColor = new Color(1f, 0.95f, 0.8f, 0.8f);
    [Tooltip("가장자리 어둡게 (0~1)")]
    [SerializeField, Range(0f, 1f)] private float _darkness;

    public string DisplayName => _displayName;
    public string Subtitle => _subtitle;
    public int RequiredPickaxeTier => Mathf.Max(0, _requiredPickaxeTier);
    public int RockHp => Mathf.Max(1, _rockHp);
    public IReadOnlyList<MineDrop> Veins => _veins;
    public IReadOnlyList<MineDrop> RareDrops => _rareDrops;
    public float RareChance => _rareChance;
    public MineOreDefinition Relic => _relic;
    public float RelicChance => _relicChance;
    public Sprite Background => _background;
    public Color BackgroundTint => _backgroundTint;
    public Color RockTint => _rockTint;
    public Color MoteColor => _moteColor;
    public float Darkness => _darkness;
}
