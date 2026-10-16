using UnityEngine;

/// <summary>해금 조건을 검사할 때 필요한 게임 상태</summary>
public readonly struct UnlockContext
{
    public readonly int PlayerLevel;
    public readonly CurrencyManager Currency;

    public UnlockContext(int playerLevel, CurrencyManager currency)
    {
        PlayerLevel = playerLevel;
        Currency = currency;
    }
}

/// <summary>
/// 무언가(꾸미기 구역 등)를 여는 조건 하나. 레벨 조건, 재화 비용처럼 종류마다 상속해서 만든다.
/// 새 조건(퀘스트 완료, 도감 수 등)이 필요하면 이 클래스를 상속한 에셋을 추가하면 된다.
/// </summary>
public abstract class UnlockRequirement : ScriptableObject
{
    /// <summary>지금 조건을 만족하는지 (비용이면 낼 수 있는지)</summary>
    public abstract bool IsMet(UnlockContext context);

    /// <summary>조건을 채운다 (비용이면 지불). IsMet이 true일 때만 호출한다</summary>
    /// <returns>채웠으면 true</returns>
    public virtual bool TryFulfill(UnlockContext context) => IsMet(context);

    /// <summary>화면에 보일 조건 설명 (예: "Lv.5 이상", "조개 100")</summary>
    public abstract string Describe();
}
