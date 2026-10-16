using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 재화마다 가격 버튼 스프라이트 (골드 = 겨자색, 조개 = 연보라). 목록에 없는 재화는 기본 스프라이트.
/// </summary>
[Serializable]
public class CurrencySpriteMap
{
    [Serializable]
    public class Entry
    {
        [SerializeField] private Currency _currency;
        [SerializeField] private Sprite _sprite;

        public Currency Currency => _currency;
        public Sprite Sprite => _sprite;
    }

    [SerializeField] private List<Entry> _entries = new List<Entry>();
    [Tooltip("목록에 없는 재화의 버튼")]
    [SerializeField] private Sprite _fallback;

    public Sprite Get(Currency currency)
    {
        foreach (var entry in _entries)
        {
            if (entry != null && entry.Currency == currency && entry.Sprite != null)
                return entry.Sprite;
        }
        return _fallback;
    }
}
