using System;
using UnityEngine;

/// <summary>
/// 처음 넓힌 영토 쪽으로 옮겨지는 묶음 (새 이웃의 집 · 환영 소품 · 공사 현장). 서쪽을 먼저 넓히면 서쪽 땅, 북쪽이면 북쪽 땅에 놓인다.
/// 아직 넓히기 전에는 서쪽 자리에 있지만, 그때는 집이 지어지기 전이라 보이지 않는다 (새 이웃 맞이하기는 땅이 열린 뒤).
/// SettlementPlazaView가 발전 오브젝트를 켜고 끄기 전에 Apply를 부른다 (걷기 영역 계산 전에 자리가 정해지게).
/// </summary>
public class TerritoryAnchor : MonoBehaviour
{
    [Tooltip("서쪽을 먼저 넓혔을 때 (또는 아직 넓히기 전) 이 묶음의 자리 (월드)")]
    [SerializeField] private Vector3 _westPosition;

    [Tooltip("북쪽을 먼저 넓혔을 때 이 묶음의 자리 (월드)")]
    [SerializeField] private Vector3 _northPosition;

    [Tooltip("북쪽을 먼저 넓힌 발전 (territory_home_north)")]
    [SerializeField] private string _northDevelopment = "territory_home_north";

    /// <param name="has">발전이 열렸는지</param>
    /// <returns>자리가 바뀌었으면 true</returns>
    public bool Apply(Func<string, bool> has)
    {
        var target = has(_northDevelopment) ? _northPosition : _westPosition;
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
