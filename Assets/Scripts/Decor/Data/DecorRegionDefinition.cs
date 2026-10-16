using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 꾸미기 격자의 구역 하나 (예: 광장 가운데, 분수대 옆). 물건은 열린 구역의 칸에만 놓을 수 있다.
/// 처음부터 열려 있거나, 해금 조건(레벨, 재화 등)을 모두 채우면 열린다. 조건은 에셋으로 갈아 끼울 수 있다.
/// </summary>
[CreateAssetMenu(fileName = "DecorRegion", menuName = "Game Data/Decor/Decor Region")]
public class DecorRegionDefinition : ScriptableObject
{
    [Header("식별 및 기본정보")]
    [Tooltip("Key로 사용될 ID (세이브에 저장됨, 예: plaza_center). 저장 데이터가 생긴 뒤에는 바꾸지 않는다")]
    [SerializeField]
    private string _regionId;

    [Tooltip("잠긴 구역에 보일 이름")]
    [SerializeField]
    private string _displayName;

    [Header("칸")]
    [Tooltip("이 구역에 속하는 칸 (격자 좌표, 왼쪽 아래가 0,0). 여러 직사각형을 합쳐 모양을 만든다")]
    [SerializeField]
    private List<RectInt> _areas = new List<RectInt>();

    [Header("해금")]
    [Tooltip("처음부터 열려 있는지")]
    [SerializeField]
    private bool _unlockedByDefault;

    [Tooltip("열기 위한 조건 (모두 채워야 함). 비어 있고 기본으로 잠겨 있으면 게임 쪽이 직접 열어야 한다")]
    [SerializeField]
    private List<UnlockRequirement> _requirements = new List<UnlockRequirement>();

    public string RegionId => _regionId;
    public string DisplayName => _displayName;
    public IReadOnlyList<RectInt> Areas => _areas;
    public bool UnlockedByDefault => _unlockedByDefault;
    public IReadOnlyList<UnlockRequirement> Requirements => _requirements;

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(_regionId))
            Debug.LogWarning($"[{name}] RegionId가 비어 있습니다.", this);
    }
}
