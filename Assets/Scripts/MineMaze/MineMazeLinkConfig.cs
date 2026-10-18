using UnityEngine;

/// <summary>
/// 본 게임과 광산 미로를 잇는 설정 (Resources/MineMazeLink). 미로 장소 · 광산 장소(돌아갈 곳), 광산 씬 입구 표시 그림.
/// 미로 장소는 이동 팝업에 카드를 만들지 않도록 전역 UI 장소 목록에 넣지 않는다 (광산 카트로만 들어감).
/// 만들기: Tools/Mine Maze/Connect To Main Game
/// </summary>
[CreateAssetMenu(menuName = "OtterKingdom/Mine Maze/Link", fileName = "MineMazeLink")]
public class MineMazeLinkConfig : ScriptableObject
{
    public const string ResourcePath = "MineMazeLink";

    [Tooltip("미로 장소 (씬 MineMaze, 갱도를 찾아야 열림)")]
    [SerializeField] private ZoneDefinition _mazeZone;
    [Tooltip("미로에서 나가면 돌아갈 광산 장소")]
    [SerializeField] private ZoneDefinition _mineZone;

    [Header("광산 씬 입구 (광산 카트)")]
    [Tooltip("입구 가운데 (광산 배경 그림 픽셀, 왼쪽 위 원점)")]
    [SerializeField] private Vector2 _entrancePixel = new Vector2(756f, 480f);
    [Tooltip("누를 수 있는 크기 (광산 배경 그림 픽셀)")]
    [SerializeField] private Vector2 _entranceSizePixel = new Vector2(190f, 140f);
    [Tooltip("광산 배경 그림 크기 · 1유닛 픽셀 (MineSceneSetup과 같음)")]
    [SerializeField] private Vector2 _backgroundPixels = new Vector2(1024f, 1536f);
    [SerializeField] private float _backgroundPixelsPerUnit = 96f;
    [Tooltip("입구 뒤에서 숨 쉬는 빛")]
    [SerializeField] private Sprite _glow;

    private static MineMazeLinkConfig _loaded;
    private static bool _tried;

    /// <summary>Resources의 연결 설정 (없으면 null = 미로가 본 게임에 아직 연결되지 않음)</summary>
    public static MineMazeLinkConfig Instance
    {
        get
        {
            if (!_tried)
            {
                _tried = true;
                _loaded = Resources.Load<MineMazeLinkConfig>(ResourcePath);
            }
            return _loaded;
        }
    }

    public ZoneDefinition MazeZone => _mazeZone;
    public ZoneDefinition MineZone => _mineZone;
    public Sprite Glow => _glow;

    /// <summary>광산 씬 월드 위치 (배경이 원점 가운데)</summary>
    public Vector2 EntranceWorld => new Vector2(
        (_entrancePixel.x - _backgroundPixels.x * 0.5f) / _backgroundPixelsPerUnit,
        (_backgroundPixels.y * 0.5f - _entrancePixel.y) / _backgroundPixelsPerUnit);

    public Vector2 EntranceWorldSize => _entranceSizePixel / _backgroundPixelsPerUnit;
}
