using System.Text;
Console.OutputEncoding = Encoding.UTF8;

// ───────── 데이터 (제안값) ─────────
var crops = new Dictionary<string, (double grow, int yield, int price)>
{
    ["당근"] = (30, 3, 5), ["오이"] = (45, 3, 8), ["감자"] = (60, 4, 9), ["고구마"] = (90, 4, 16), ["딸기"] = (120, 5, 21),
};
// 시나리오 (dotnet run -- B)
//   A: 모든 모종이 한 번 사면 계속 심기
//   B: 당근만 계속 심기, 나머지는 심을 때마다 모종 1개 (한 번 수확 가치의 SeedShare), 판매가 그대로
//   C: B와 같지만 판매가를 올려서 모종값을 뺀 순수입이 A와 같게
//   D: C + 새 작물을 처음 열 때 한 번 내는 해금 가격(앞 레벨 번 돈의 80%)
string scenario = args.Length > 0 ? args[0].ToUpperInvariant() : "A";
const double SeedShare = 0.25;
bool consumable = scenario != "A";
if (scenario == "C" || scenario == "D")
    foreach (var k in crops.Keys.ToList())
        if (k != "당근") { var c = crops[k]; crops[k] = (c.grow, c.yield, (int)Math.Round(c.price / (1 - SeedShare))); }
int SeedCost(string k) => !consumable || k == "당근" ? 0 : (int)Math.Round(crops[k].yield * crops[k].price * SeedShare);
double[] growMul = { 1.0, 0.9, 0.8, 0.7, 0.6 };
const double FishPerMin = 1.2; // 가정 (입질 10~30초, 물고기 50%) — 측정 필요
int fishPrice = 0;             // Lv.10에서 +20%가 되도록 계산
double[] targetAt = { 0, 3, 7, 12, 20, 35, 55, 90, 150, 240, 360, 540, 780 }; // [L] = Lv.L+1 도달 목표(분)

// ───────── 상태 ─────────
int furrows = 3, farm = 0; string crop = null; bool fishing = false;
double gold = 0, t = 0;
double HarvestsPerMin() => crop == null ? 0 : Farm.HarvestsPerMinute(furrows, crops[crop].grow * growMul[farm], 8);
// 순수입: 판매액 - 다시 심을 모종값
double FarmIncome() => crop == null ? 0 : HarvestsPerMin() * (crops[crop].yield * crops[crop].price - SeedCost(crop));
double SeedSpendPerMin() => crop == null ? 0 : HarvestsPerMin() * SeedCost(crop);
double Income() => FarmIncome() + (fishing ? FishPerMin * fishPrice : 0);
double ItemsPerMin() => Farm.HarvestsPerMinute(furrows, crops[crop].grow * growMul[farm], 8) * crops[crop].yield;
int Nice(double v) { if (v < 10) return (int)Math.Max(1, Math.Round(v)); double p = Math.Pow(10, Math.Floor(Math.Log10(v)) - 1); double s = p * (v / p >= 50 ? 5 : 1); return (int)(Math.Round(v / s) * s); }
string Fmt(double m) => m < 1 ? $"{m * 60:F0}초" : m < 90 ? $"{m:F1}분" : $"{m / 60:F1}시간";

var quests = new StringBuilder(); var summary = new StringBuilder(); var prices = new StringBuilder();
int level = 1; double lvStart = 0, prevIncome = 0;
double earnedThisLevel = 0, earnedPrevLevel = 0, seedThisLevel = 0, seedTotal = 0;
void Pass(double m) { gold += Income() * m; earnedThisLevel += Income() * m; seedThisLevel += SeedSpendPerMin() * m; t += m; }
// 새 작물: A는 한 번 사는 해금(앞 레벨 번 돈의 80%), B·C는 모종 20개 묶음으로 시작 (이후 모종값은 순수입에서 빠짐)
void CropBuy(string id, string name)
{
    if (scenario == "D") Buy(id, $"{name} 재배 시작 (첫 모종 포함)", -1, () => crop = name);
    else if (consumable) Buy(id, $"{name} 모종 20개 사기", 0, () => crop = name, 0, SeedCost(name) * 20);
    else Buy(id, $"{name} 모종 사기", -1, () => crop = name);
}
// 레벨의 대표 해금: 앞 레벨에 번 돈의 80%
int Unlock() => Nice(earnedPrevLevel * 0.8);
int[] levelExp = { 0, 100, 150, 220, 320, 450, 600, 800, 1050, 1350, 1700, 2100, 2600 };
var rows = new List<(string id, string title, double m, int reward, double income, double gold)>();
void Row(string id, string title, double m, int reward) => rows.Add((id, title, m, reward, Income(), gold));
void FlushRows()
{
    double total = rows.Sum(r => r.m); int exp = levelExp[level]; int given = 0;
    for (int i = 0; i < rows.Count; i++)
    {
        var r = rows[i];
        // 짧은 퀘스트도 최소 8%: 먼저 최소치를 떼어 두고 나머지를 시간 비례로
        double floorShare = 0.08; double rest = 1 - floorShare * rows.Count;
        int e = i == rows.Count - 1 ? exp - given : (int)(Math.Round(exp * (floorShare + rest * r.m / total) / 10.0) * 10);
        given += e;
        quests.AppendLine($"| {r.id} | {r.title} | {Fmt(r.m)} | {e} | {r.reward:N0} | {r.income:F0} | {r.gold:N0} |");
    }
    rows.Clear();
}

void Act(string id, string title, double m, int reward = 0) { Pass(m); gold += reward; Row(id, title, m, reward); }
// 저축형 가격: 지금 잔액 + 수입 × 분 → 사려면 정말 그만큼 모아야 함
void Buy(string id, string what, double saveMin, Action effect, int reward = 0, int fixedCost = 0)
{
    int cost = fixedCost > 0 ? fixedCost : saveMin < 0 ? Unlock() : Nice(Income() * saveMin);
    double m = Math.Max(0.5, (cost - gold) / Math.Max(1e-6, Income()) + 0.5);
    Pass(m); gold -= cost; effect(); gold += reward;
    prices.AppendLine($"| Lv.{level} | {what} | {cost:N0} |");
    Row(id, $"{what} ({cost:N0}골드)", m, reward);
}
void Harvest(string id, string cropName, double min, int reward = 0)
{ int n = Nice(ItemsPerMin() * min); double m = n / ItemsPerMin(); Pass(m); gold += reward; Row(id, $"{cropName} {n:N0}개 수확하기", m, reward); }
void Sell(string id, double min, int reward = 0)
{ int n = Nice(Income() * min); double m = n / Income(); Pass(m); gold += reward; Row(id, $"판매로 {n:N0}골드 벌기", m, reward); }
void Fish(string id, string what, double min, int reward = 0)
{ int n = Nice(FishPerMin * min); double m = n / FishPerMin; Pass(m); gold += reward; Row(id, $"{what} {n:N0}마리 낚기", m, reward); }
void Level(int l, string theme)
{
    if (l > 1) End();
    level = l; lvStart = t; prevIncome = Income(); earnedPrevLevel = earnedThisLevel; earnedThisLevel = 0;
    quests.AppendLine($"\n### Lv.{l} → {l + 1} · {theme}\n| # | 퀘스트 | 소요 | 경험치 | 골드 보상 | 분당 수입 | 잔액 |\n|---|---|---|---|---|---|---|");
}
double lastLevelIncome = 0;
void End()
{
    FlushRows();
    double inc = Income();
    string jump = lastLevelIncome > 0 ? $"{inc / lastLevelIncome - 1:+0%;-0%}" : "-";
    string mark = Math.Abs(t - targetAt[level]) / targetAt[level] > 0.15 ? " ⚠️" : "";
    summary.AppendLine($"| {level} → {level + 1} | {t - lvStart:F1}분 | {targetAt[level] - targetAt[level - 1]:F0}분 | {Fmt(t)} | {Fmt(targetAt[level])}{mark} | {inc:F0} | {jump} | {seedThisLevel:N0} | {gold:N0} |");
    lastLevelIncome = inc; seedTotal += seedThisLevel; seedThisLevel = 0;
}

// ───────── 레벨 계획 (한 레벨에 성장 하나: 고랑 ↔ 새 작물) ─────────
Level(1, "해달 왕국에 어서 와");
Act("1-1", "🖐 요정 상점 열기", 1.25, 30);
Buy("1-2", "🖐 당근 모종 사기 (한 번 사면 계속 심기)", 0, () => crop = "당근", 0, 20);
Act("1-3", "🖐 가방 열어 당근 모종 확인", 1.25);

Level(2, "밭의 친구");
Act("2-1", "🖐 도감에서 농부 해달 이야기 보기", 1.0, 20);
Harvest("2-2", "🖐 당근", 1.5, 20);
Sell("2-3", 1.5); // 🖐 가방 → 판매

Level(3, "고랑 넓히기");
Buy("3-1", "고랑 한 칸 열기 (4칸째)", -1, () => furrows = 4);
Sell("3-2", 4.5);

Level(4, "새 작물, 오이");
CropBuy("4-1", "오이");
Harvest("4-2", "오이", 5.5, 50);
Act("4-3", "도감 3칸 채우고 이야기 보기", 2.0);

Level(5, "고랑 다섯 칸");
Buy("5-1", "고랑 한 칸 열기 (5칸째)", -1, () => furrows = 5);
Harvest("5-2", "오이", 8.0);
Sell("5-3", 6.5);

Level(6, "감자와 첫 손님");
CropBuy("6-1", "감자");
Buy("6-2", "요정 상점에서 축구공 사기", 6.0, () => { });
Act("6-3", "🖐 광장에 장난감 놓기", 1.0, 100);
Harvest("6-4", "감자", 11.0);
Act("6-5", "방문 해달 1마리 만나기", 7.0, 200);

Level(7, "밭 가득");
Buy("7-1", "고랑 한 칸 열기 (6칸째)", -1, () => furrows = 6);
Harvest("7-2", "감자", 18.0, 300);
Sell("7-3", 16.5);

Level(8, "달콤한 고구마와 조개");
CropBuy("8-1", "고구마");
Act("8-2", "🖐 조개로 가방 칸 늘리기", 1.0);
Act("8-3", "퍼즐(조개) 사서 광장에 놓기", 2.0, 300);
Harvest("8-4", "고구마", 36.5);
Act("8-5", "방문 해달 2마리 만나기", 20.0, 500);

Level(9, "대풍년");
Buy("9-1", "고랑 한 칸 열기 (7칸째)", -1, () => furrows = 7);
Buy("9-2", "🖐 밭 강화 (Lv.2)", 20.0, () => farm = 1);
Act("9-3", "일일 퀘스트 1개 끝내기", 10.0);
Harvest("9-4", "고구마", 40.0);
Sell("9-5", 39.0);

// 낚시: 농사 수입의 +20%가 되도록 물고기 값
fishPrice = Nice(FarmIncome() * 0.20 / FishPerMin);
Level(10, "바다가 열렸다");
Act("10-1", "낚시터 가 보기", 1.0, 200);
fishing = true;
Act("10-2", "도감에서 낚시꾼 해달 이야기 보기", 2.0, 200);
Fish("10-3", "고등어", 30.0, 500);
Fish("10-4", "물고기", 87.0, 800);

Level(11, "새빨간 딸기");
CropBuy("11-1", "딸기");
Harvest("11-2", "딸기", 90.0, 800);
Fish("11-3", "물고기", 89.5);

Level(12, "해달 왕국");
Buy("12-1", "고랑 한 칸 열기 (8칸째)", -1, () => furrows = 8);
Buy("12-2", "밭 강화 (Lv.3)", 60.0, () => farm = 2);
Act("12-3", "방문 해달 3마리 만나기", 60.0, 1000);
Harvest("12-4", "딸기", 90.0);
Sell("12-5", 89.0);
End();

Console.WriteLine($"# 시나리오 {scenario}\n\n## 작물 (제안)\n| 작물 | 성장 | 수확 | 판매가 | 한 번 수확 가치 | 모종 (1번 심기) | 고랑 하나 분당 순수입 |\n|---|---|---|---|---|---|---|");
foreach (var (n, c) in crops) Console.WriteLine($"| {n} | {c.grow}초 | {c.yield}개 | {c.price} | {c.yield * c.price} | {SeedCost(n)} | {Farm.HarvestsPerMinute(1, c.grow, 8) * (c.yield * c.price - SeedCost(n)):F0} |");
Console.WriteLine($"\n물고기 판매가: {fishPrice} (분당 {FishPerMin}마리 가정)");
Console.WriteLine("\n## 레벨 요약\n| Lv | 걸린 시간 | 예산 | 누적 | 목표 | 분당 순수입 | 앞 레벨 대비 | 모종 지출 | 끝날 때 잔액 |\n|---|---|---|---|---|---|---|---|---|");
Console.Write(summary);
Console.WriteLine("\n## 가격\n| 레벨 | 항목 | 가격 |\n|---|---|---|"); Console.Write(prices);
Console.WriteLine("\n## 퀘스트"); Console.Write(quests);
