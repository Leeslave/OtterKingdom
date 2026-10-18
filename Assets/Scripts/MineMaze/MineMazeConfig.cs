using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>보물 상자에서 나오는 광석 하나 (개수 범위)</summary>
[Serializable]
public class MazeLoot
{
    [Tooltip("광석")]
    [SerializeField] private MineOreDefinition _ore;
    [Tooltip("최소 개수")]
    [SerializeField] private int _min = 1;
    [Tooltip("최대 개수")]
    [SerializeField] private int _max = 1;
    [Tooltip("나올 확률 (0~1)")]
    [SerializeField, Range(0f, 1f)] private float _chance = 1f;

    public MazeLoot() { }

    public MazeLoot(MineOreDefinition ore, int min, int max, float chance = 1f)
    {
        _ore = ore;
        _min = min;
        _max = max;
        _chance = chance;
    }

    public MineOreDefinition Ore => _ore;
    public int Min => Mathf.Max(0, _min);
    public int Max => Mathf.Max(Min, _max);
    public float Chance => _chance;
}

/// <summary>
/// 광산 미로 한 층(테스트 씬: 지하 1층) 설정. 돌을 치는 규칙 · 광석 · 곡괭이는 광산 개편(MineRework)의 것을 그대로 쓴다.
/// 지도 글자 · 돌이 다시 생기는 시간 · 보물 상자 · 지도 해달이 오는 조건 · 갱도 그림.
/// Docs/광산_미로탐험과_왕도성장_기획.md 4.4~4.7 · 만들기: Tools/Mine Maze/Build Test Scene
/// </summary>
[CreateAssetMenu(menuName = "OtterKingdom/Mine Maze/Config", fileName = "MineMazeConfig")]
public class MineMazeConfig : ScriptableObject
{
    [Header("광산 개편 규칙")]
    [Tooltip("광석 · 곡괭이 · 손맛 수치 (콤보 · 치명타 · 반짝 광맥 · 광석 개수)")]
    [SerializeField] private MineReworkConfig _rework;
    [Tooltip("이 층의 광맥 · 희귀 · 화석 · 바위 체력 (MineRework 층)")]
    [SerializeField] private MineLayerDefinition _layer;

    [Header("층")]
    [Tooltip("보이는 이름 (예: 지하 1층)")]
    [SerializeField] private string _floorName = "지하 1층";
    [Tooltip("지도 ('#' 벽 · '.' 길 · 'S' 입구 · 'E' 계단 · 'R' 돌 · 'T' 보물 상자)")]
    [SerializeField] private List<string> _map = new List<string>();

    [Header("돌")]
    [Tooltip("부순 돌이 같은 자리에 다시 생기는 시간 (초). 한 번 뚫은 자리는 다시 생겨도 지나갈 수 있음")]
    [SerializeField] private float _rockRespawnSeconds = 60f;

    [Header("보물 상자")]
    [Tooltip("상자 하나에서 나오는 것 (한 번만 열림)")]
    [SerializeField] private List<MazeLoot> _chestLoot = new List<MazeLoot>();

    [Header("지도 해달")]
    [Tooltip("지도 없이 이만큼 나갔다 오면 지도 해달이 옴")]
    [SerializeField] private int _mapOtterRuns = 2;
    [Tooltip("첫 탐험에서 이만큼 칸을 돌아다녔으면 첫 번째로 나올 때 바로 옴")]
    [SerializeField] private int _mapOtterEarlyCells = 40;

    public MineReworkConfig Rework => _rework;
    [Tooltip("1층 설정에서만 지정: 지하 2층부터 순서대로")]
    [SerializeField] private List<MineMazeConfig> _additionalFloors = new List<MineMazeConfig>();
    public IReadOnlyList<MineMazeConfig> AdditionalFloors => _additionalFloors;
    [Header("성장 순환 테스트")]
    [SerializeField, Min(1)] private int _testKingdomLevel = 9;
    [SerializeField, Min(1)] private int _gateKingdomLevel = 9;
    [SerializeField, Min(0)] private int _gatePickaxeTier = 1;
    [SerializeField, Min(1)] private int _gateHits = 30;
    public int TestKingdomLevel => Mathf.Max(1, _testKingdomLevel);
    public int GateKingdomLevel => Mathf.Max(1, _gateKingdomLevel);
    public int GatePickaxeTier => Mathf.Clamp(_gatePickaxeTier, 0, Rework.Pickaxes.Count - 1);
    public int GateHits => Mathf.Max(1, _gateHits);
    public MineLayerDefinition Layer => _layer;
    public string FloorName => _floorName;
    public IReadOnlyList<string> MapRows => _map;
    public float RockRespawnSeconds => Mathf.Max(1f, _rockRespawnSeconds);
    public IReadOnlyList<MazeLoot> ChestLoot => _chestLoot;
    public int MapOtterRuns => Mathf.Max(1, _mapOtterRuns);
    public int MapOtterEarlyCells => Mathf.Max(1, _mapOtterEarlyCells);
}
