using UnityEngine;

/// <summary>
/// 장소 분위기 연출을 한곳에서: 소품·해달 발밑 그림자, 나무·덤불 살랑임, 날아다니는 나비(낮)·반딧불(밤), 지나가는 구름 그림자.
/// 시간대 빛은 같은 오브젝트의 TimeOfDayLighting이 맡는다.
/// 소품(PlazaProp)은 시작할 때 한 번, 돌아다니는 것(해달·광부·요정·장애물)은 1초마다 새로 생긴 것을 찾아 그림자를 붙인다.
/// 팀원 프리팹·씬 소품은 건드리지 않고 실행 중에만 붙인다.
/// </summary>
public class AmbienceDirector : MonoBehaviour
{
    private const float ScanInterval = 1f;

    [Header("발밑 그림자")]
    [Tooltip("흐린 타원 (흰색, 색은 아래에서)")]
    [SerializeField] private Sprite _shadowSprite;

    [Tooltip("그림자 그리는 순서 (바닥 위, 소품·해달 아래)")]
    [SerializeField] private int _shadowOrder = PlazaDepth.FlatPropOrder + 1;

    [SerializeField] private Color _shadowColor = new Color(0.29f, 0.18f, 0.13f, 0.4f);

    [Tooltip("이 이름의 소품은 그림자 없음 (바닥에 깔린 공사 터 등)")]
    [SerializeField] private string[] _noShadowNames = { "Scaffold" };

    [Header("살랑임")]
    [Tooltip("이 글자가 이름에 들어간 소품(또는 그 부모)이 바람에 살랑임")]
    [SerializeField] private string[] _swayNames = { "Tree", "Bush" };

    [Tooltip("바람 셰이더 재질 (OtterKingdom/Sprite Wind Sway). 밑동은 그대로, 위만 흔들림")]
    [SerializeField] private Material _windMaterial;

    [Header("나비")]
    [Tooltip("날개 편 것 / 접은 것")]
    [SerializeField] private Sprite[] _butterflyFrames;

    [SerializeField] private int _butterflyCount = 3;

    [SerializeField] private int _butterflyOrder = 31990;

    [SerializeField] private float _butterflySize = 0.6f;

    [SerializeField] private Color[] _butterflyTints =
    {
        new Color(1f, 0.84f, 0.9f, 1f),
        new Color(1f, 0.96f, 0.74f, 1f),
        new Color(0.84f, 0.92f, 1f, 1f),
    };

    [Header("반딧불 (밤)")]
    [SerializeField] private Sprite _fireflySprite;

    [Tooltip("조명을 받지 않는 재질 (어둠 속에서도 빛나게)")]
    [SerializeField] private Material _glowMaterial;

    [SerializeField] private int _fireflyCount = 6;

    [SerializeField] private int _fireflyOrder = 31995;

    [SerializeField] private float _fireflySize = 0.35f;

    [Header("구름 그림자")]
    [SerializeField] private Sprite _cloudSprite;

    [SerializeField] private int _cloudCount = 2;

    [SerializeField] private int _cloudOrder = 31980;

    [SerializeField] private Color _cloudColor = new Color(0.22f, 0.2f, 0.35f, 0.1f);

    [Tooltip("구름 그림자 폭 (월드)")]
    [SerializeField] private float _cloudWidth = 9f;

    [Tooltip("흘러가는 빠르기 (월드/초)")]
    [SerializeField] private float _cloudSpeed = 0.25f;

    private float _scanTimer;

    private void Start()
    {
        DecorateProps();
        ScanMovers();

        for (int i = 0; i < _butterflyCount && _butterflyFrames != null && _butterflyFrames.Length >= 2; i++)
        {
            var go = new GameObject("Butterfly");
            go.transform.SetParent(transform, false);
            go.AddComponent<ButterflyView>().Init(_butterflyFrames, _butterflyTints[i % _butterflyTints.Length], _butterflyOrder, _butterflySize);
        }
        for (int i = 0; i < _fireflyCount && _fireflySprite != null; i++)
        {
            var go = new GameObject("Firefly");
            go.transform.SetParent(transform, false);
            go.AddComponent<FireflyView>().Init(_fireflySprite, _glowMaterial, _fireflyOrder, _fireflySize * Random.Range(0.7f, 1.2f));
        }
        for (int i = 0; i < _cloudCount && _cloudSprite != null; i++)
        {
            var go = new GameObject("CloudShadow");
            go.transform.SetParent(transform, false);
            go.AddComponent<CloudShadowView>().Init(_cloudSprite, _cloudColor, _cloudOrder, _cloudWidth * Random.Range(0.8f, 1.2f),
                _cloudSpeed * Random.Range(0.8f, 1.25f), true);
        }
    }

    private void Update()
    {
        _scanTimer -= Time.deltaTime;
        if (_scanTimer > 0f)
            return;
        _scanTimer = ScanInterval;
        ScanMovers();
    }

    // 서 있는 소품: 그림자 + (나무·덤불이면) 살랑임
    private void DecorateProps()
    {
        foreach (var prop in FindObjectsByType<PlazaProp>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (prop.IsFlat || System.Array.IndexOf(_noShadowNames, prop.name) >= 0)
                continue;
            // 집처럼 피벗이 앞 모서리인 소품은 바닥 가운데(깊이 보정의 절반쯤)로
            GroundShadow.Attach(prop.gameObject, _shadowSprite, _shadowOrder, _shadowColor, 0.7f, prop.DepthOffset * 0.5f);

            if (_windMaterial != null && IsSwayProp(prop))
                prop.gameObject.AddComponent<IdleSway>().Configure(_windMaterial, 0.025f, Random.Range(1.6f, 2.2f));
        }
    }

    // 이름으로 고름 (흔드는 사과나무도: 셰이더 살랑임과 흔들기 기울임은 함께 써도 됨)
    private bool IsSwayProp(PlazaProp prop)
    {
        if (prop.GetComponent<IdleSway>() != null)
            return false;
        foreach (var key in _swayNames)
        {
            if (prop.name.Contains(key) || (prop.transform.parent != null && prop.transform.parent.name.Contains(key)))
                return true;
        }
        return false;
    }

    // 돌아다니거나 나중에 생기는 것들
    private void ScanMovers()
    {
        foreach (var otter in FindObjectsByType<OtterWanderAgent>(FindObjectsSortMode.None))
            GroundShadow.Attach(otter.gameObject, _shadowSprite, _shadowOrder, _shadowColor, 0.55f, 0f);
        foreach (var builder in FindObjectsByType<BuilderOtterController>(FindObjectsSortMode.None))
            GroundShadow.Attach(builder.gameObject, _shadowSprite, _shadowOrder, _shadowColor, 0.55f, 0f);
        foreach (var miner in FindObjectsByType<MinerOtterController>(FindObjectsSortMode.None))
            GroundShadow.Attach(miner.gameObject, _shadowSprite, _shadowOrder, _shadowColor, 0.55f, 0f);
        foreach (var farmer in FindObjectsByType<FarmerOtterController>(FindObjectsSortMode.None))
            GroundShadow.Attach(farmer.gameObject, _shadowSprite, _shadowOrder, _shadowColor, 0.55f, 0f);
        foreach (var fisher in FindObjectsByType<FishingOtterController>(FindObjectsSortMode.None))
            GroundShadow.Attach(fisher.gameObject, _shadowSprite, _shadowOrder, _shadowColor, 0.55f, 0f);
        foreach (var npc in FindObjectsByType<OfflineFarmNpcView>(FindObjectsSortMode.None))
            GroundShadow.Attach(npc.gameObject, _shadowSprite, _shadowOrder, _shadowColor, 0.55f, 0f);
        foreach (var fairy in FindObjectsByType<FairyNpcView>(FindObjectsSortMode.None))
            GroundShadow.Attach(fairy.gameObject, _shadowSprite, _shadowOrder, _shadowColor, 0.4f, 0f);
        foreach (var obstacle in FindObjectsByType<ClearingObstacleView>(FindObjectsSortMode.None))
            GroundShadow.Attach(obstacle.gameObject, _shadowSprite, _shadowOrder, _shadowColor, 0.75f, 0f);
    }
}
