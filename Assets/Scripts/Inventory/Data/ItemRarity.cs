using System;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemRarity", menuName = "Game Data/Inventory/Item Rarity")]
public class ItemRarity : ScriptableObject
{
    [Header("식별 및 기본정보")]
    [Tooltip("Key로 사용될 ID (예: Common)")]
    [SerializeField]
    private string _rarityId;

    [Tooltip("화면에 표시될 이름 (예: 흔함)")]
    [SerializeField]
    private string _displayName;

    [Header("등급")]
    [Tooltip("높을수록 희귀 (0 이상, 정렬/비교용)")]
    [SerializeField]
    private int _tier;

    [Header("시각 요소 (UI)")]
    [Tooltip("희귀도 대표 색 (슬롯 테두리 등)")]
    [SerializeField]
    private Color _color = Color.white;

    public string RarityId => _rarityId;
    public string DisplayName => _displayName;
    public int Tier => _tier;
    public Color Color => _color;

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(_rarityId))
            Debug.LogWarning($"[{name}] RarityID가 비어 있습니다.", this);

        _tier = Math.Max(0, _tier);
    }
}
