using UnityEngine;

/// <summary>
/// 가공 레시피 (기획서 6.2): 이 시설이 이 원재료 N개로 가공품 1개를 만듦. 이 기술을 배워야 켤 수 있음.
/// 한 시설에 줄이 여럿이면 둘째 줄부터 가공 줄 ID를 따로 (예: 목공소 정밀 공방). 창고 재료 레시피는 만들 때마다 창고에서 꺼냄
/// </summary>
[CreateAssetMenu(fileName = "Recipe", menuName = "Game Data/Industry/Processing Recipe")]
public class ProcessingRecipeDefinition : ScriptableObject
{
    [Header("시설 · 기술")]
    [Tooltip("가공하는 시설 (목공소 · 채석 작업소 …)")]
    [SerializeField]
    private BuildingDefinition _building;

    [Tooltip("이 기술을 배워야 가공을 켤 수 있음 (TechIds, 예: I01)")]
    [SerializeField]
    private string _techId;

    [Tooltip("가공 줄 ID (비우면 시설 ID). 한 시설의 둘째 줄부터 따로 (세이브 · 수레가 이것으로 찾음)")]
    [SerializeField]
    private string _processorId;

    [Header("재료")]
    [Tooltip("넣는 원재료 (시설이 모은 것 + 가방에서 넣은 것)")]
    [SerializeField]
    private ItemDefinition _input;

    [Tooltip("가공품 1개에 드는 원재료 수")]
    [Min(1)]
    [SerializeField]
    private int _inputCount = 4;

    [Tooltip("켜면 재료를 넣어 두지 않고 만들 때마다 왕국 창고에서 꺼냄 (정밀 공방: 판재 + 주괴)")]
    [SerializeField]
    private bool _fromWarehouse;

    [Tooltip("창고 재료 레시피의 둘째 재료 (없으면 비움)")]
    [SerializeField]
    private ItemDefinition _extraInput;

    [Tooltip("둘째 재료 수")]
    [Min(0)]
    [SerializeField]
    private int _extraInputCount;

    [Tooltip("만드는 가공품 (왕국 창고에 들어감)")]
    [SerializeField]
    private ItemDefinition _output;

    [Tooltip("가공품 1개 만드는 시간 (초)")]
    [Min(1f)]
    [SerializeField]
    private float _cycleSeconds = 360f;

    [Header("기록")]
    [Tooltip("처음 만들면 열리는 발전 기록 (퀘스트가 셈, 예: first_plank)")]
    [SerializeField]
    private string _firstDevelopment;

    [Header("광장 소품 (IndustryPlazaView)")]
    [Tooltip("기술을 배우면 시설 앞에 놓이는 작업대 (제재 작업대 · 돌 재단대)")]
    [SerializeField]
    private Sprite _benchSprite;

    [Tooltip("시설에 쌓인 가공품 더미 (적음 · 보통 · 많음 순). 누르면 창고로")]
    [SerializeField]
    private Sprite[] _pileSprites = new Sprite[3];

    [Tooltip("이 가공품을 실은 수레 (오른쪽으로 감)")]
    [SerializeField]
    private Sprite _cartSprite;

    [Tooltip("소품 자리: 0 = 왼쪽 작업대 · 오른쪽 더미, 1 = 가운데 더미 (한 시설의 둘째 줄)")]
    [Range(0, 1)]
    [SerializeField]
    private int _propSlot;

    public BuildingDefinition Building => _building;
    public string TechId => _techId;
    public string ProcessorId => !string.IsNullOrEmpty(_processorId) ? _processorId : _building != null ? _building.BuildingId : null;
    public bool FromWarehouse => _fromWarehouse;
    public ItemDefinition ExtraInput => _extraInput;
    public int ExtraInputCount => _extraInput != null ? Mathf.Max(0, _extraInputCount) : 0;
    public int PropSlot => _propSlot;
    public ItemDefinition Input => _input;
    public int InputCount => _inputCount;
    public ItemDefinition Output => _output;
    public float CycleSeconds => _cycleSeconds;
    public string FirstDevelopment => _firstDevelopment;
    public Sprite BenchSprite => _benchSprite;
    public Sprite CartSprite => _cartSprite;

    /// <summary>쌓인 수에 맞는 더미 그림 (시설 보관함의 1/3까지 적음, 2/3까지 보통)</summary>
    public Sprite PileSprite(int tray, int capacity)
    {
        if (_pileSprites == null || _pileSprites.Length == 0 || tray <= 0)
            return null;
        int index = Mathf.Clamp(tray * 3 / Mathf.Max(1, capacity), 0, _pileSprites.Length - 1);
        return _pileSprites[index] != null ? _pileSprites[index] : _pileSprites[0];
    }

    public bool IsValid => _building != null && _input != null && _output != null;

    public ProcessingRecipe ToRecipe() => new ProcessingRecipe(ProcessorId, _input.ItemId, _inputCount, _output.ItemId, _cycleSeconds,
        _extraInput != null ? _extraInput.ItemId : null, ExtraInputCount, _fromWarehouse);
}
