using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>회관의 섬 지도. 한 방향씩 넘겨 보고 정찰 주민을 고른다. 모든 결과는 기존 영토 개간으로 이어진다.</summary>
public class IslandMapSheet : MonoBehaviour
{
    // 지도 그림: Tools/StoryCuts/worldmap.py --island (바탕 IslandMap, 칸마다 Fog_<칸> · Pick_<칸>, 칸 위치 regions.json)
    private const string MapFolder = "UI/IslandMap";
    private const float MapHeight = 600f;
    // 발견했지만 아직 개간하지 않은 칸의 안개 진하기
    private const float DiscoveredFogAlpha = 0.4f;

    [Serializable] private class MapRegion { public string id; public string development; public float x, y, w, h; }
    [Serializable] private class MapRegions { public List<MapRegion> regions = new List<MapRegion>(); }
    private static MapRegions _mapRegions;

    private Canvas _canvas;
    private SettlementManager _manager;
    private readonly List<TerritoryDirection> _directions = new List<TerritoryDirection>();
    private readonly List<string> _selected = new List<string>();
    private int _direction;
    private int _workerPage;
    private bool _picking;
    private string _lastDestination;
    private TextMeshProUGUI _timer;
    public bool IsOpen => _canvas != null && _canvas.gameObject.activeSelf;

    public void Open(SettlementManager manager)
    {
        _manager = manager;
        _directions.Clear();
        foreach (var territory in manager.Config.Territories)
            if (territory != null && !_directions.Contains(territory.Direction)) _directions.Add(territory.Direction);
        _direction = 0;
        _picking = false;
        _selected.Clear();
        if (_canvas == null)
        {
            _canvas = RuntimeUIKit.CreateCanvas("섬 지도", 235, false);
            _canvas.transform.SetParent(transform, false);
        }
        _canvas.gameObject.SetActive(true);
        Draw();
    }

    public void Close() { if (_canvas != null) _canvas.gameObject.SetActive(false); }

    public void Focus(TerritoryDirection direction)
    {
        int index = _directions.IndexOf(direction);
        if (index < 0) return;
        _direction = index;
        _picking = false;
        Draw();
    }

    private void Update()
    {
        if (!IsOpen || _manager == null) return;
        var scout = _manager.Settlement.Scouting;
        if (scout.Destination != _lastDestination) { _picking = false; Draw(); return; }
        if (_timer != null && scout.Destination != null)
            _timer.text = $"정찰대 {scout.Workers.Count}명 · 귀환까지 {Math.Max(0, (scout.EndUtcTicks - SettlementManager.NowTicks) / TimeSpan.TicksPerSecond)}초";
    }

    private void Draw(string hint = "")
    {
        for (int i = _canvas.transform.childCount - 1; i >= 0; i--)
        {
            var child = _canvas.transform.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
        RuntimeUIKit.PrepareGlyphs("섬 지도 정찰 발견 보고 주민 귀환 회수 출발 재료 비용 없음 바닷가 낡은 선착장 농경지 생산 일터 다음 이전 방향 선택 한가한 작업 중 초 조건을 다시 확인해 주세요 " + hint);
        var panel = RuntimeUIScrollSheet.Create(_canvas.transform, Close);
        RuntimeUIKit.CreateLabel(panel, "섬 지도 · 정찰", 42, true);
        var scout = _manager.Settlement.Scouting;
        DrawMap(panel, scout);
        _lastDestination = scout.Destination;
        _timer = null;
        if (!_manager.HasIslandMap)
            RuntimeUIKit.CreateLabel(panel, "오래된 갱도를 탐험해 지도 해달을 만나고, 마을회관을 마련하면 섬 정찰을 시작할 수 있어요. 첫 개간은 먼저 할 수 있어요.", 28);
        else if (scout.Destination != null)
        {
            RuntimeUIKit.CreateLabel(panel, $"{_manager.ScoutDestination(scout.Destination)?.DisplayName} 정찰 중", 32, true);
            _timer = RuntimeUIKit.CreateLabel(panel, "귀환을 기다리고 있어요.", 28);
            RuntimeUIKit.CreateLabel(panel, "정찰 중인 주민은 다른 작업을 쉬어요. 돌아오면 보고서를 읽기 전에도 다시 일을 할 수 있어요.", 26);
            RuntimeUIKit.CreateButton(panel, "정찰대 회수하기", () =>
            {
                _manager.CancelScouting();
                Draw("주민들이 돌아왔어요. 정찰을 마치지 않아 새 발견은 없어요.");
            }, RuntimeUIKit.ButtonKind.Secondary);
        }
        else if (_directions.Count > 0)
        {
            var nav = RuntimeUIKit.CreateRow(panel, 85);
            RuntimeUIKit.CreateButton(nav, "이전 방향", () => ChangeDirection(-1), flexible: true);
            RuntimeUIKit.CreateButton(nav, "다음 방향", () => ChangeDirection(1), flexible: true);
            var territory = _manager.CurrentTerritory(_directions[_direction]);
            if (territory == null) RuntimeUIKit.CreateLabel(panel, "이 방향은 모두 넓혔어요!", 32);
            else
            {
                bool discovered = scout.HasDiscovered(territory.ExpansionId);
                bool tutorial = TerritoryRules.IsTutorial(territory, _manager.Settlement);
                RuntimeUIKit.CreateLabel(panel, territory.DisplayName, 34, true);
                RuntimeUIKit.CreateLabel(panel, discovered ? "발견한 땅 · 길을 개간하면 건물과 밭을 놓을 수 있어요." : ScoutingRules.Hint(territory), 27);
                if (tutorial)
                    RuntimeUIKit.CreateLabel(panel, "첫 개간은 정찰 없이 시작해요. 공터 오른쪽 숲의 점선 원을 눌러요.", 28);
                else if (discovered)
                {
                    RuntimeUIKit.CreateLabel(panel, $"개간 {_manager.TerritoryClearingsDone(territory)}/{territory.Clearings.Count}", 28);
                    RuntimeUIKit.CreateButton(panel, "발견한 땅으로 길 내기", () =>
                    {
                        var task = _manager.NextTerritoryClearing(territory);
                        Close();
                        if (_manager.GetTerritoryStage(territory) == TerritoryStage.Cleared && territory.Mission != null)
                            _manager.RequestProject(territory.Mission);
                        else if (task != null) _manager.RequestTask(task);
                    });
                }
                else if (!_picking)
                    RuntimeUIKit.CreateButton(panel, "정찰대 편성", () => { _picking = true; _selected.Clear(); _workerPage = 0; Draw(); });
                else DrawWorkers(panel, territory);
            }
        }
        if (scout.Reports.Count > 0)
        {
            string id = scout.Reports[0];
            var reported = _manager.ScoutDestination(id);
            RuntimeUIKit.CreateLabel(panel, $"새 발견 보고: {reported?.DisplayName ?? id}\n{(reported != null ? ScoutingRules.Hint(reported) : "정찰대가 돌아왔어요.")}", 26);
            RuntimeUIKit.CreateButton(panel, "보고 확인 · 해당 방향 보기", () =>
            {
                scout.ReadReport(id);
                if (reported != null) _direction = Math.Max(0, _directions.IndexOf(reported.Direction));
                SettlementManager.RequestSave();
                Draw();
            });
        }
        if (!string.IsNullOrEmpty(hint)) RuntimeUIKit.CreateLabel(panel, hint, 26);
    }

    private void DrawWorkers(Transform panel, TerritoryExpansionDefinition territory)
    {
        var candidates = new List<SettlementOtterDefinition>();
        _manager.CollectResidents(candidates);
        _selected.RemoveAll(id => _manager.Config.FindOtter(id) == null || !_manager.CanAssign(_manager.Config.FindOtter(id)));
        var available = candidates.FindAll(o => _manager.CanAssign(o));
        int pages = Math.Max(1, (candidates.Count + 3) / 4);
        _workerPage = Math.Min(_workerPage, pages - 1);
        RuntimeUIKit.CreateLabel(panel, $"주민 1~2명 · 예상 {(int)ScoutingRules.Duration(territory).TotalSeconds}초\n남는 일반 작업 주민 {available.Count - _selected.Count}명", 27);
        for (int i = _workerPage * 4; i < Math.Min(candidates.Count, _workerPage * 4 + 4); i++)
        {
            var otter = candidates[i];
            bool canWork = _manager.CanAssign(otter);
            bool chosen = _selected.Contains(otter.OtterId);
            var button = RuntimeUIKit.CreateButton(panel, $"{(chosen ? "선택 · " : "")}{otter.DisplayName}{(canWork ? "" : " · 다른 작업 중")}", () =>
            {
                if (chosen) _selected.Remove(otter.OtterId);
                else if (_selected.Count < 2) _selected.Add(otter.OtterId);
                Draw();
            }, chosen ? RuntimeUIKit.ButtonKind.Primary : RuntimeUIKit.ButtonKind.Option);
            button.interactable = canWork;
        }
        if (pages > 1) RuntimeUIKit.CreateButton(panel, $"다른 주민 보기 ({_workerPage + 1}/{pages})", () => { _workerPage = (_workerPage + 1) % pages; Draw(); });
        var start = RuntimeUIKit.CreateButton(panel, "정찰 출발 · 재료 비용 없음", () =>
        {
            bool ok = _manager.TryStartScouting(territory, _selected);
            _picking = !ok;
            Draw(ok ? "정찰대가 출발했어요!" : "주민과 정찰 조건을 다시 확인해 주세요.");
        });
        start.interactable = _selected.Count > 0;
    }

    // 우리 땅 전체 지도: 못 가 본 칸은 안개(발견한 칸은 옅게, 개간을 시작했거나 연 칸은 걷힘),
    // 지금 보는 방향(정찰 중이면 정찰 가는 곳)의 칸은 빨간 점선. 영토 칸을 누르면 그 방향으로 넘김
    private void DrawMap(Transform panel, ScoutingProgress scout)
    {
        var map = Resources.Load<Sprite>(MapFolder + "/IslandMap");
        if (map == null)
            return;
        if (_mapRegions == null)
        {
            var json = Resources.Load<TextAsset>(MapFolder + "/regions");
            _mapRegions = json != null ? JsonUtility.FromJson<MapRegions>(json.text) : new MapRegions();
        }

        var holder = new GameObject("Map", typeof(RectTransform), typeof(LayoutElement));
        holder.transform.SetParent(panel, false);
        var element = holder.GetComponent<LayoutElement>();
        element.preferredHeight = MapHeight;
        element.minHeight = MapHeight;

        var areaObject = new GameObject("Area", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
        areaObject.transform.SetParent(holder.transform, false);
        var area = (RectTransform)areaObject.transform;
        var fitter = areaObject.GetComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = map.rect.width / map.rect.height;
        var image = areaObject.GetComponent<Image>();
        image.sprite = map;
        image.raycastTarget = false;

        var viewed = scout.Destination == null && _directions.Count > 0 ? _manager.CurrentTerritory(_directions[_direction]) : null;
        foreach (var region in _mapRegions.regions)
        {
            var territory = _manager.ScoutDestination(region.development);
            bool open, discovered = false, picked = false;
            if (territory != null)
            {
                open = _manager.GetTerritoryStage(territory) == TerritoryStage.Expanded || _manager.TerritoryClearingsDone(territory) > 0;
                discovered = scout.HasDiscovered(territory.ExpansionId) || TerritoryRules.IsTutorial(territory, _manager.Settlement);
                picked = scout.Destination != null ? scout.Destination == territory.ExpansionId : territory == viewed;
            }
            else
                open = _manager.Settlement.HasDevelopment(region.development);

            if (!open)
                AddRegionImage(area, region, "Fog_", discovered ? DiscoveredFogAlpha : 1f);
            if (picked)
                AddRegionImage(area, region, "Pick_", 1f);

            // 영토 칸을 누르면 그 방향 (정찰 중에는 바꾸지 않음)
            int direction = territory != null ? _directions.IndexOf(territory.Direction) : -1;
            if (direction >= 0 && scout.Destination == null && _manager.HasIslandMap)
            {
                var tap = AddRegionImage(area, region, null, 0f);
                tap.raycastTarget = true;
                tap.gameObject.AddComponent<Button>().onClick.AddListener(() =>
                {
                    _direction = direction;
                    _picking = false;
                    Draw();
                });
            }
        }
    }

    private static Image AddRegionImage(RectTransform area, MapRegion region, string prefix, float alpha)
    {
        var go = new GameObject(prefix == null ? "Tap_" + region.id : prefix + region.id, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(area, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(region.x, 1f - region.y - region.h);
        rect.anchorMax = new Vector2(region.x + region.w, 1f - region.y);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = go.GetComponent<Image>();
        image.sprite = prefix != null ? Resources.Load<Sprite>($"{MapFolder}/{prefix}{region.id}") : null;
        image.color = new Color(1f, 1f, 1f, alpha);
        image.raycastTarget = false;
        return image;
    }

    private void ChangeDirection(int delta)
    {
        _direction = (_direction + delta + _directions.Count) % _directions.Count;
        _picking = false;
        Draw();
    }
}
