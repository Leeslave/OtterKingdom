using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 처음 넓힌 영토 쪽으로 옮겨지는 묶음 (새 이웃의 집 · 환영 소품 · 공사 현장). 처음 넓힌 방향의 첫 땅(그 방향 구역 블록 1단계)에 놓인다.
/// 아직 넓히기 전에는 기본 자리에 있지만, 그때는 집이 지어지기 전이라 보이지 않는다 (새 이웃 맞이하기는 땅이 열린 뒤).
/// SettlementPlazaView가 발전 오브젝트를 켜고 끄기 전에 Apply를 부른다 (걷기 영역 계산 전에 자리가 정해지게).
/// </summary>
public class TerritoryAnchor : MonoBehaviour
{
    /// <summary>방향 하나의 자리: 이 발전(처음 넓힌 방향)이 열려 있으면 여기</summary>
    [Serializable]
    public class Spot
    {
        [Tooltip("처음 넓힌 방향의 발전 (territory_home_ne 등)")]
        public string development;

        [Tooltip("이 묶음의 자리 (월드)")]
        public Vector3 position;
    }

    [Tooltip("아직 넓히기 전의 자리 (월드)")]
    [SerializeField] private Vector3 _defaultPosition;

    [Tooltip("방향마다의 자리 (앞의 것부터 봄)")]
    [SerializeField] private List<Spot> _spots = new List<Spot>();

    public Vector3 DefaultPosition => _defaultPosition;
    public IReadOnlyList<Spot> Spots => _spots;

    /// <summary>열린 발전에 맞는 자리</summary>
    public Vector3 TargetFor(Func<string, bool> has)
    {
        foreach (var spot in _spots)
        {
            if (spot != null && !string.IsNullOrEmpty(spot.development) && has(spot.development))
                return spot.position;
        }
        return _defaultPosition;
    }

    /// <param name="has">발전이 열렸는지</param>
    /// <returns>자리가 바뀌었으면 true</returns>
    public bool Apply(Func<string, bool> has)
    {
        var target = TargetFor(has);
        if (transform.position == target)
            return false;
        transform.position = target;
        foreach (var site in GetComponentsInChildren<ConstructionSiteView>(true))
            site.RefreshDepth();
        foreach (var prop in GetComponentsInChildren<PlazaProp>(true))
            prop.SetDepthOffset(prop.DepthOffset);
        return true;
    }
}
