using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 장소의 전체 빛(Global Light 2D)을 실제 시각에 맞춘다 (TimeOfDay). 씬에 전체 빛이 없으면 만든다.
/// 화면 UI는 영향을 받지 않는다. Night는 밤 불빛(NightGlow)·반딧불이 함께 본다.
/// 옛 기본 재질(Sprites-Default, 조명을 안 받음)을 쓰는 그림은 URP 조명 재질로 바꿔 함께 어두워지게 한다 (밭 배경·고랑·작물).
/// </summary>
public class TimeOfDayLighting : MonoBehaviour
{
    private const string LegacySpriteMaterial = "Sprites-Default";
    private const float RelightInterval = 2f;

    [Tooltip("조명을 받는 스프라이트 재질 (URP Sprite-Lit-Default)")]
    [SerializeField] private Material _litMaterial;

    private float _relightTimer;

    /// <summary>지금 밤 불빛을 켜는 정도 (0~1). 장소마다 같은 시각이라 하나만 둠</summary>
    public static float Night { get; private set; }

    private Light2D _light;

    private void Awake()
    {
        _light = FindObjectsByType<Light2D>(FindObjectsSortMode.None).FirstOrDefault(l => l.lightType == Light2D.LightType.Global);
        if (_light == null)
        {
            var go = new GameObject("TimeOfDayGlobalLight");
            go.transform.SetParent(transform, false);
            _light = go.AddComponent<Light2D>();
            _light.lightType = Light2D.LightType.Global;
            _light.targetSortingLayers = SortingLayer.layers.Select(l => l.id).ToArray();
        }
        Apply();
    }

    private void Start()
    {
        Relight();
    }

    private void Update()
    {
        Apply();
        // 작물처럼 나중에 생기는 그림도 바꿈
        _relightTimer -= Time.deltaTime;
        if (_relightTimer <= 0f)
        {
            _relightTimer = RelightInterval;
            Relight();
        }
    }

    private void Relight()
    {
        if (_litMaterial == null)
            return;
        foreach (var renderer in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
        {
            var material = renderer.sharedMaterial;
            if (material != null && material.name == LegacySpriteMaterial)
                renderer.sharedMaterial = _litMaterial;
        }
    }

    private void Apply()
    {
        float hour = TimeOfDay.CurrentHour;
        var (color, intensity) = TimeOfDay.Light(hour);
        _light.color = color;
        _light.intensity = intensity;
        Night = TimeOfDay.Night(hour);
    }
}
