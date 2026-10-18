using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 광산 개편(테스트 씬) 설정: 광석 · 층 · 곡괭이 목록과 손맛 수치 (바위 다시 생김, 반짝 광맥, 광맥 대박, 콤보, 치명타, 광부).
/// 값은 모두 임시 (Docs/광산_개편_기획.md). 만들기: Tools/Mine Rework/Build Test Scene
/// </summary>
[CreateAssetMenu(menuName = "OtterKingdom/Mine Rework/Config", fileName = "MineReworkConfig")]
public class MineReworkConfig : ScriptableObject
{
    [Header("목록")]
    [Tooltip("모든 광석 · 화석 (도감 순서)")]
    [SerializeField] private List<MineOreDefinition> _ores = new List<MineOreDefinition>();
    [Tooltip("층 (얕은 곳부터)")]
    [SerializeField] private List<MineLayerDefinition> _layers = new List<MineLayerDefinition>();
    [Tooltip("곡괭이 단계 (처음 곡괭이부터)")]
    [SerializeField] private List<MinePickaxeDefinition> _pickaxes = new List<MinePickaxeDefinition>();
    [Tooltip("평범한 바위에서 나오는 돌")]
    [SerializeField] private MineOreDefinition _stone;

    [Header("광맥 바위")]
    [Tooltip("한 번에 있을 수 있는 바위 수")]
    [SerializeField] private int _rockSlots = 5;
    [Tooltip("깬 바위가 다시 생기는 간격 (초, 하나씩)")]
    [SerializeField] private float _rockRespawnSeconds = 45f;
    [Tooltip("깨면 나오는 광맥 광석 개수 (최소 · 최대)")]
    [SerializeField] private Vector2Int _veinYield = new Vector2Int(2, 3);
    [Tooltip("평범한 바위의 돌 개수 (최소 · 최대)")]
    [SerializeField] private Vector2Int _stoneYield = new Vector2Int(2, 4);
    [Tooltip("새 바위가 반짝 광맥일 확률")]
    [SerializeField, Range(0f, 1f)] private float _shinyChance = 0.08f;
    [Tooltip("반짝 광맥 체력 배수")]
    [SerializeField] private float _shinyHpMultiplier = 1.5f;
    [Tooltip("막힌 길(다음 층 입구 바위)을 깨는 데 칠 횟수")]
    [SerializeField] private int _boulderHits = 20;

    [Header("손맛")]
    [Tooltip("이 시간(초) 안에 다시 치면 콤보가 이어짐")]
    [SerializeField] private float _comboWindow = 0.8f;
    [Tooltip("콤보 이만큼마다 깰 때 광석 하나 더 (최대 3)")]
    [SerializeField] private int _comboBonusEvery = 10;
    [Tooltip("기본 치명타 확률")]
    [SerializeField, Range(0f, 1f)] private float _baseCritChance = 0.1f;
    [Tooltip("날 갈기 한 번에 오르는 치명타 확률")]
    [SerializeField, Range(0f, 1f)] private float _critPerSharpen = 0.08f;
    [Tooltip("콤보 5마다 오르는 치명타 확률 (최대 10번)")]
    [SerializeField, Range(0f, 0.1f)] private float _critPerFiveCombo = 0.01f;

    [Header("광맥 대박")]
    [Tooltip("반짝 광맥을 깨면 대박이 터질 확률")]
    [SerializeField, Range(0f, 1f)] private float _jackpotChanceOnShiny = 0.3f;
    [Tooltip("아무 바위나 깨면 대박이 터질 확률")]
    [SerializeField, Range(0f, 1f)] private float _jackpotChancePerRock = 0.01f;
    [Tooltip("대박이 이어지는 시간 (초): 바위가 바로 다시 차오르고 광석 두 배")]
    [SerializeField] private float _jackpotSeconds = 15f;

    [Header("광부 (방치)")]
    [Tooltip("광부가 하나 캐는 간격 (초)")]
    [SerializeField] private float _minerSeconds = 10f;
    [Tooltip("광부를 응원하면 두 배로 캐는 시간 (초)")]
    [SerializeField] private float _cheerSeconds = 10f;
    [Tooltip("다시 응원할 수 있기까지 (초)")]
    [SerializeField] private float _cheerCooldown = 30f;
    [Tooltip("꺼 둔 동안은 이 배수만큼 느리게 캠")]
    [SerializeField] private float _offlineSlowdown = 3f;
    [Tooltip("꺼 둔 동안 캐는 최대 개수")]
    [SerializeField] private int _offlineMaxFinds = 120;

    [Header("테스트")]
    [Tooltip("처음 골드")]
    [SerializeField] private int _startGold = 200;

    public IReadOnlyList<MineOreDefinition> Ores => _ores;
    public IReadOnlyList<MineLayerDefinition> Layers => _layers;
    public IReadOnlyList<MinePickaxeDefinition> Pickaxes => _pickaxes;
    public MineOreDefinition Stone => _stone;
    public int RockSlots => Mathf.Max(1, _rockSlots);
    public float RockRespawnSeconds => Mathf.Max(1f, _rockRespawnSeconds);
    public Vector2Int VeinYield => _veinYield;
    public Vector2Int StoneYield => _stoneYield;
    public float ShinyChance => _shinyChance;
    public float ShinyHpMultiplier => Mathf.Max(1f, _shinyHpMultiplier);
    public int BoulderHits => Mathf.Max(1, _boulderHits);
    public float ComboWindow => _comboWindow;
    public int ComboBonusEvery => Mathf.Max(1, _comboBonusEvery);
    public float BaseCritChance => _baseCritChance;
    public float CritPerSharpen => _critPerSharpen;
    public float CritPerFiveCombo => _critPerFiveCombo;
    public float JackpotChanceOnShiny => _jackpotChanceOnShiny;
    public float JackpotChancePerRock => _jackpotChancePerRock;
    public float JackpotSeconds => _jackpotSeconds;
    public float MinerSeconds => Mathf.Max(1f, _minerSeconds);
    public float CheerSeconds => _cheerSeconds;
    public float CheerCooldown => _cheerCooldown;
    public float OfflineSlowdown => Mathf.Max(1f, _offlineSlowdown);
    public int OfflineMaxFinds => Mathf.Max(0, _offlineMaxFinds);
    public int StartGold => _startGold;

    public MineOreDefinition FindOre(string oreId)
    {
        foreach (var ore in _ores)
        {
            if (ore != null && ore.OreId == oreId)
                return ore;
        }
        return null;
    }
}
