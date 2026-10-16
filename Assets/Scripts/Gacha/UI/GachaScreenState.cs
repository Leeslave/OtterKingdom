using System.Collections.Generic;
using UnityEngine;

/// <summary>뽑기 버튼 하나의 모양 (값 치르는 방법에 따라 색 · 아이콘 · 값)</summary>
public readonly struct GachaPriceLook
{
    public readonly Sprite Button;
    public readonly Sprite Icon;
    public readonly string Cost;

    public GachaPriceLook(Sprite button, Sprite icon, string cost)
    {
        Button = button;
        Icon = icon;
        Cost = cost;
    }
}

/// <summary>뽑기 화면에 그릴 값 (GachaPresenter가 만들고 GachaScreenView가 그림)</summary>
public sealed class GachaScreenState
{
    public readonly List<GachaBannerDefinition> Banners = new List<GachaBannerDefinition>();
    public readonly HashSet<GachaBannerDefinition> NewBanners = new HashSet<GachaBannerDefinition>();
    public GachaBannerDefinition Selected;

    /// <summary>"13일 남음" · "상시"</summary>
    public string Period;
    public bool Limited;

    /// <summary>배너 아래 눈여겨볼 장난감 (픽업 = 확률 UP, 상시 = 에픽)</summary>
    public string HighlightLabel;
    public readonly List<(ItemDefinition item, int tier, bool featured)> Highlights = new List<(ItemDefinition, int, bool)>();

    /// <summary>"에픽 확정까지" · "레어 확정까지" (골드 배너)</summary>
    public string PityLabel = "에픽 확정까지";
    public int PityLeft;
    public int PityTotal;
    public bool Guaranteed;

    public bool ShowPoints;
    public int Points;
    public int PointsGoal;
    public ItemDefinition PointsReward;

    public bool ShowGold;
    public bool GoldAvailable;
    public bool GoldAffordable;
    public int GoldCost;

    /// <summary>천장 아래 안내 줄 (포인트 · 골드 뽑기 줄이 없는 배너, 비우면 감춤)</summary>
    public string Note;
    public Sprite NoteIcon;

    public int Shards;
    public Sprite TicketIcon;
    public int Tickets;

    public GachaPriceLook Single;
    public GachaPriceLook Ten;
}
