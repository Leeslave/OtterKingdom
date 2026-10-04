using System;
using System.Collections.Generic;

/// <summary>
/// 밭 알림 문구 (모종 부족 · 가방 가득). 여러 칸이 한꺼번에 멈추면 한 줄로 묶고 작물 이름을 붙인다.
/// 요정 상점이 열려 있으면 [상점으로], 요정이 아직 오지 않았으면 "찾아오고 있어요" (없는 NPC로 보내지 않음).
/// </summary>
public static class FarmNoticeText
{
    public const string FairyComingText = "모종을 판매하는 요정이 찾아오고 있어요.";
    public const string BuyFromFairyText = "요정 상점에서 구매해 주세요.";
    public const string StorageFullText = "가방이 가득해서 밭 수확이 멈췄어요.\n가방의 수확물을 팔아 자리를 비워 주세요.";
    public const string ShopButton = "상점으로";

    /// <summary>모종을 어디서 사는지 (요정이 왔으면 상점, 아니면 오는 중)</summary>
    public static string WhereToBuySeeds => FairyAccess.IsShopOpen ? BuyFromFairyText : FairyComingText;

    public static GameNotice Build(FarmNotice notice, Func<string, string> cropName)
    {
        if (notice.Kind == FarmNoticeKind.StorageFull)
            return new GameNotice(StorageFullText);

        string message = notice.SlotCount <= 1 && notice.CropIds.Count > 0
            ? $"{cropName(notice.CropIds[0])} 모종이 없어 밭 한 곳의 재배가 멈췄어요."
            : $"모종이 없어 밭 {notice.SlotCount}곳의 재배가 멈췄어요. ({Join(notice.CropIds, cropName)})";
        message += "\n" + WhereToBuySeeds;
        return FairyAccess.IsShopOpen
            ? new GameNotice(message, ShopButton, FairyAccess.OpenShop)
            : new GameNotice(message);
    }

    private static string Join(IReadOnlyList<string> cropIds, Func<string, string> cropName)
    {
        var names = new List<string>(cropIds.Count);
        foreach (var id in cropIds)
            names.Add(cropName(id));
        return string.Join("·", names);
    }
}
