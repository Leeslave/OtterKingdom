using System;
using UnityEngine;

/// <summary>
/// 작물 판매가 규칙 (Assets/Data/Crops/CropPricing.asset 하나).
/// 심을 때마다 모종을 쓰는 작물(감자·고구마·딸기)은 "한 번 수확해서 파는 값 = 모종 하나 값 × 배율"이 되도록
/// 수확물 아이템의 판매가를 정한다. 계속 심는 작물(당근·오이)은 모종값이 없어 그대로 둔다.
/// 인스펙터에서 배율을 바꾸면 바로 다시 계산된다 (CropPricingSetup). 요정 상점 모종값이나 수확량을 바꿨으면
/// 메뉴 Tools/Farm/Apply Crop Pricing으로 다시 맞춘다. 오프라인 수확(한 번에 offlineYieldPerHarvest개)은 따로다.
/// </summary>
[CreateAssetMenu(menuName = "OtterKingdom/Crop Pricing", fileName = "CropPricing")]
public class CropPricing : ScriptableObject
{
    // 2배: 한 번 거두면 모종값만큼 남음 → 사서 심는 작물이 공짜 당근 · 오이보다 칸당 더 벎 (흔함 무 1.6골드/초 > 당근 1.0)
    [Tooltip("한 번 수확해서 파는 값 = 모종 하나 값 × 이 배율 (2 = 모종값의 2배)")]
    [Min(0.1f)]
    [SerializeField] private float _harvestToSeedRatio = 2f;

    public float HarvestToSeedRatio => _harvestToSeedRatio;

    /// <summary>
    /// 수확물 한 개 판매가 = 모종 하나 값 × 배율 ÷ 한 번 수확량 (올림: 배율보다 덜 받지 않게)
    /// </summary>
    public static int SellPriceFor(double seedPrice, int yieldCount, double ratio)
    {
        if (yieldCount <= 0 || seedPrice <= 0 || ratio <= 0)
            return 0;
        return (int)Math.Ceiling(seedPrice * ratio / yieldCount - 1e-9);
    }

#if UNITY_EDITOR
    /// <summary>인스펙터에서 값이 바뀌었을 때 (에디터 도구 CropPricingSetup이 판매가를 다시 계산)</summary>
    public static event Action<CropPricing> Changed;

    private void OnValidate()
    {
        // OnValidate 안에서는 다른 에셋을 고치지 않음 → 한 프레임 뒤에
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null)
                Changed?.Invoke(this);
        };
    }
#endif
}
