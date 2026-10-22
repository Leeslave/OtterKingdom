using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 낚시 수치 맞추기: 강화 단계마다 봇이 실제 규칙(FishingReworkModel)으로 낚시해 보고 표로 남김.
/// - 단계 k = 줄 · 찌 · 릴을 모두 k단계로 맞춘 낚시 (0 = 처음)
/// - 봇 셋: 해달(방치 — 아무렇게나 누름), 가라앉히기(안 누름), 노리기(다음 강화 재료 물고기가 사는 깊이에 바늘을 맞춤)
/// - 잰 것: 한 번 던지기 시간 · 분당 낚은 수 · 분당 값 · 놓침 · 무거워 놓침 · 물고기별 분당 · 다음 강화 재료를 다 모으는 데 걸리는 분
/// 메뉴: Tools/Fishing Rework/Balance Sim · 배치: -executeMethod FishingBalanceSim.Run → Docs/낚시_밸런스_시뮬.md
/// </summary>
public static class FishingBalanceSim
{
    private const string ConfigPath = "Assets/Scriptable Obejects/FishingRework/FishingReworkConfig.asset";
    private const string ReportPath = "Docs/낚시_밸런스_시뮬.md";
    private const float Dt = 0.02f;
    private const float Minutes = 20f;
    private const float AimMinutes = 8f;

    private enum Bot { Otter, Sink, Target }

    private class Result
    {
        public int Casts, Catches, Missed, TooHeavy, Value;
        public float CastSeconds;
        public readonly Dictionary<FishDefinition, int> ByFish = new Dictionary<FishDefinition, int>();
    }

    [MenuItem("Tools/Fishing Rework/Balance Sim")]
    public static void Run()
    {
        var asset = AssetDatabase.LoadAssetAtPath<FishingReworkConfig>(ConfigPath);
        // 에셋은 그대로 두고 복사본에 시험할 수치를 넣어 봄 (명령줄 -simTune name=value ...)
        var config = Object.Instantiate(asset);
        var so = new SerializedObject(config);
        var args = System.Environment.GetCommandLineArgs();
        var tuned = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            int eq = args[i].IndexOf('=');
            if (!args[i].StartsWith("_") || eq < 0)
                continue;
            var prop = so.FindProperty(args[i].Substring(0, eq));
            if (prop == null)
                continue;
            float v = float.Parse(args[i].Substring(eq + 1), System.Globalization.CultureInfo.InvariantCulture);
            if (prop.propertyType == SerializedPropertyType.Float) prop.floatValue = v;
            else if (prop.propertyType == SerializedPropertyType.Integer) prop.intValue = (int)v;
            tuned.Add(args[i]);
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        config.SetFish(new List<FishDefinition>(asset.Fish));
        var sb = new StringBuilder();
        sb.AppendLine("# 낚시 밸런스 시뮬 (봇)");
        sb.AppendLine();
        sb.AppendLine($"`Tools/Fishing Rework/Balance Sim`으로 다시 만듦. 단계마다 {Minutes:0}분씩, 줄 · 찌 · 릴을 모두 같은 단계로 맞춤.");
        if (tuned.Count > 0)
            sb.AppendLine($"시험 수치: {string.Join(" ", tuned)}");
        sb.AppendLine("봇: **해달** = 방치(재료 물고기 먼저 아무 물고기 쪽으로 던지고 아무렇게나 누름) · **가라앉히기** = 늘 끝까지 던지고 안 누름 · **노리기** = 거리 줄자로 다음 강화 재료 물고기를 골라 던지고 그 깊이에 바늘을 맞춤 (잘하는 플레이어).");
        sb.AppendLine();
        sb.AppendLine("| 단계 | 거리 · 깊이 · 무게 | 봇 | 한 번 던지기(초) | 분당 낚음 | 분당 값 | 놓침/분 | 무거워 놓침/분 |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|");
        var details = new StringBuilder();
        float totalTargetMinutes = 0f;
        int maxLevel = config.Track(FishingUpgrade.Line).MaxLevel;
        for (int k = 0; k <= maxLevel; k++)
        {
            var results = new Dictionary<Bot, Result>();
            foreach (Bot bot in Enum.GetValues(typeof(Bot)))
            {
                var r = Simulate(config, k, bot, 1000 + k);
                results[bot] = r;
                sb.AppendLine($"| {k} | {config.Track(FishingUpgrade.Line).ValueAt(k):0}m · {config.Track(FishingUpgrade.Sinker).ValueAt(k):0}m · {config.Track(FishingUpgrade.Reel).ValueAt(k):0.#}kg | {Name(bot)} | {r.CastSeconds:0.0} | {r.Catches / Minutes:0.0} | {r.Value / Minutes:0} | {r.Missed / Minutes:0.0} | {r.TooHeavy / Minutes:0.0} |");
            }
            if (k >= maxLevel)
                continue;

            // 다음 단계 재료를 모으는 데 걸리는 분 (노리기 봇 · 해달 봇의 물고기별 속도로)
            details.AppendLine($"### 단계 {k} → {k + 1}");
            details.AppendLine();
            details.AppendLine("| 강화 | 재료 | 노리기(분) | 해달 방치(분) |");
            details.AppendLine("|---|---|---|---|");
            float stageMax = 0f;
            // 재료 물고기마다 그 물고기만 노려 본 속도 (분당)
            var aimed = new Dictionary<FishDefinition, float>();
            foreach (FishingUpgrade upgrade in Enum.GetValues(typeof(FishingUpgrade)))
            foreach (var cost in config.RecipeFor(upgrade, k + 1))
            {
                if (aimed.ContainsKey(cost.Fish))
                    continue;
                var only = Simulate(config, k, Bot.Target, 2000 + k * 37 + aimed.Count, cost.Fish, AimMinutes);
                only.ByFish.TryGetValue(cost.Fish, out int n);
                aimed[cost.Fish] = n / AimMinutes;
            }
            foreach (FishingUpgrade upgrade in Enum.GetValues(typeof(FishingUpgrade)))
            {
                var recipe = config.RecipeFor(upgrade, k + 1);
                float target = 0f, otter = 0f;
                var parts = new List<string>();
                foreach (var cost in recipe)
                {
                    parts.Add($"{cost.Fish.DisplayName} ×{cost.Amount} ({aimed[cost.Fish]:0.0}/분 · {config.PresenceAt(cost.Fish, k):0.0}마리)");
                    target += aimed[cost.Fish] > 0f ? cost.Amount / aimed[cost.Fish] : float.PositiveInfinity;
                    otter += MinutesFor(results[Bot.Otter], cost);
                }
                stageMax = Mathf.Max(stageMax, target);
                totalTargetMinutes += target;
                details.AppendLine($"| {Name(upgrade)} | {string.Join(" · ", parts)} | {Fmt(target)} | {Fmt(otter)} |");
            }
            details.AppendLine();
            var top = new List<KeyValuePair<FishDefinition, int>>(results[Bot.Target].ByFish);
            top.Sort((a, b) => b.Value.CompareTo(a.Value));
            var topText = new List<string>();
            for (int i = 0; i < top.Count && i < 8; i++)
                topText.Add($"{top[i].Key.DisplayName} {top[i].Value / Minutes:0.0}");
            details.AppendLine($"노리기 봇이 낚은 것 (분당): {string.Join(" · ", topText)}");
            details.AppendLine();
        }
        sb.AppendLine();
        sb.AppendLine($"## 강화 재료 모으기 (노리기 봇 합계 약 {totalTargetMinutes:0}분)");
        sb.AppendLine();
        sb.AppendLine($"노리기(분) = 재료 물고기를 하나씩 그것만 노려({AimMinutes:0}분씩 재 봄) 모으는 시간의 합. 같이 걸리는 다른 재료는 덤이라 실제로는 조금 덜 걸림.");
        sb.AppendLine();
        sb.Append(details);
        System.IO.File.WriteAllText(ReportPath, sb.ToString(), new UTF8Encoding(true));
        Debug.Log($"[FishingBalanceSim] {ReportPath}\n{sb}");
    }

    private static string Name(Bot bot) => bot == Bot.Otter ? "해달" : bot == Bot.Sink ? "가라앉히기" : "노리기";
    private static string Name(FishingUpgrade u) => u == FishingUpgrade.Line ? "줄" : u == FishingUpgrade.Sinker ? "찌" : "릴";
    private static string Fmt(float minutes) => float.IsInfinity(minutes) ? "못 낚음" : $"{minutes:0.0}";

    private static float MinutesFor(Result r, FishCost cost)
    {
        r.ByFish.TryGetValue(cost.Fish, out int n);
        return n <= 0 ? float.PositiveInfinity : cost.Amount / (n / Minutes);
    }

    // 던질 곳 (거리 줄자): 노리기 = 그 물고기 중 가까운 것을 노리는 자리, 해달 = 재료 물고기 먼저 아무거나(조금 빗나감),
    // 가라앉히기 = 늘 끝까지
    private static float CastDistanceFor(FishingReworkModel model, Bot bot, FishDefinition aim, System.Random rng, List<FishDefinition> wanted)
    {
        if (bot == Bot.Sink)
            return model.CastDistance;
        var picks = new List<(FishingFish fish, float weight)>();
        foreach (var fish in model.Fish)
        {
            if (fish.Hooked || fish.Weight > model.ReelCapacity || fish.Position.x > model.CastDistance || -fish.Position.y > model.MaxDepth + 0.5f)
                continue;
            if (bot == Bot.Target)
            {
                if (fish.Def == aim)
                    picks.Add((fish, 1f));
            }
            else
                picks.Add((fish, wanted.Contains(fish.Def) ? 5f : 1f));
        }
        if (picks.Count == 0)
            return model.CastDistance * (bot == Bot.Target ? 1f : (float)(0.5 + rng.NextDouble() * 0.5));
        if (bot == Bot.Target)
        {
            // 가장 가까운 것 (짧게 던져 빨리 돌아옴)
            picks.Sort((a, b) => a.fish.Position.x.CompareTo(b.fish.Position.x));
            return model.AimFor(picks[0].fish);
        }
        float total = 0f;
        foreach (var p in picks)
            total += p.weight;
        double roll = rng.NextDouble() * total;
        foreach (var p in picks)
        {
            roll -= p.weight;
            if (roll <= 0)
                return model.AimFor(p.fish) + (float)(rng.NextDouble() * 3.0 - 1.5);
        }
        return model.CastDistance;
    }

    private static Result Simulate(FishingReworkConfig config, int level, Bot bot, int seed, FishDefinition only = null, float minutes = Minutes)
    {
        var save = new FishingReworkSave { started = true, lineLevel = level, sinkerLevel = level, reelLevel = level };
        var model = new FishingReworkModel(config, save, new System.Random(seed));
        var rng = new System.Random(seed * 7);
        var r = new Result();
        model.Caught += c =>
        {
            r.Catches++;
            r.Value += c.Price;
            r.ByFish.TryGetValue(c.Def, out int n);
            r.ByFish[c.Def] = n + 1;
        };
        model.Missed += () => r.Missed++;
        model.TooHeavy += _ => r.TooHeavy++;

        // 노리기: 다음 강화 재료 중 하나를 골라 그 물고기가 사는 깊이에 바늘을 둠 (던질 때마다 돌아가며)
        var targets = new List<FishDefinition>();
        if (only != null)
            targets.Add(only);
        else if (level < config.Track(FishingUpgrade.Line).MaxLevel)
        {
            foreach (FishingUpgrade u in Enum.GetValues(typeof(FishingUpgrade)))
            foreach (var cost in config.RecipeFor(u, level + 1))
                if (!targets.Contains(cost.Fish))
                    targets.Add(cost.Fish);
        }
        FishDefinition aim = null;
        int aimIndex = 0;
        bool autoHold = false;
        float autoTimer = 0f, castStart = 0f, castTotal = 0f;
        float t = 0f, end = minutes * 60f;
        while (t < end)
        {
            bool hold = false;
            if (model.Phase == FishingPhase.Ready)
            {
                if (targets.Count > 0)
                    aim = targets[aimIndex++ % targets.Count];
                model.TryCast(CastDistanceFor(model, bot, aim, rng, targets));
                r.Casts++;
                castStart = t;
            }
            else if (model.Phase == FishingPhase.InWater)
            {
                switch (bot)
                {
                    case Bot.Otter:
                        autoTimer -= Dt;
                        if (autoTimer <= 0f)
                        {
                            autoHold = !autoHold;
                            autoTimer = autoHold ? (float)(0.3 + rng.NextDouble() * 0.6) : (float)(0.8 + rng.NextDouble() * 1.0);
                        }
                        hold = autoHold;
                        break;
                    case Bot.Target:
                        if (aim != null)
                        {
                            // 잘하는 플레이어: 화면에 보이는 그 물고기 중 바늘 앞(선착장 쪽)에서 가장 가까운 것에 깊이를 맞춤.
                            // 안 보이면 그 물고기가 사는 깊이로
                            float depth = -model.Hook.y;
                            float want = aim.NearBottom ? config.SeabedDepth(model.Hook.x) - 0.6f : (aim.MinDepth + Mathf.Min(aim.MaxDepth, model.MaxDepth)) * 0.5f;
                            float best = float.MaxValue;
                            foreach (var fish in model.Fish)
                            {
                                if (fish.Def != aim || fish.Hooked || fish.Position.x > model.Hook.x + 0.5f)
                                    continue;
                                float dx = model.Hook.x - fish.Position.x;
                                if (dx < best && dx < 12f)
                                {
                                    best = dx;
                                    want = -fish.Position.y;
                                }
                            }
                            hold = depth > want + 0.25f;
                        }
                        break;
                }
            }
            var before = model.Phase;
            model.Tick(Dt, hold);
            t += Dt;
            if (before != FishingPhase.Ready && model.Phase == FishingPhase.Ready)
                castTotal += t - castStart;
        }
        r.CastSeconds = r.Casts > 0 ? castTotal / r.Casts : 0f;
        return r;
    }
}
