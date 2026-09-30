// 농부 해달 수확 시뮬레이션 (FarmerOtterController 동작 그대로): 가장 가까운 수확 대기 고랑 → L자 이동(초당 1칸) → 수확 애니메이션 → 같은 작물 다시 심기
public static class Farm
{
    static readonly double[] Xs = { -1.84, 0.27, 2.38 };
    static readonly double[] Ys = { 1.93, -0.20, -2.55 };
    public static readonly List<(double x, double y)> Order = Build();
    static List<(double, double)> Build()
    {
        var o = new List<(double, double)>();
        foreach (var y in Ys) { o.Add((Xs[1], y)); o.Add((Xs[0], y)); o.Add((Xs[2], y)); }
        return o;
    }

    /// <returns>분당 수확 횟수</returns>
    public static double HarvestsPerMinute(int furrows, double growSec, double harvestSec, double walkSpeed = 1.0)
    {
        const double Sim = 3600;
        var slots = Order.Take(furrows).ToList();
        var readyAt = slots.Select((s, i) => growSec * (0.3 + 0.7 * i / Math.Max(1, furrows))).ToArray();
        double t = 0, fx = 0.27, fy = 0.8; int n = 0;
        while (t < Sim)
        {
            int best = -1; double bestD = double.MaxValue;
            for (int i = 0; i < slots.Count; i++)
                if (readyAt[i] <= t)
                {
                    double d = Math.Sqrt(Math.Pow(slots[i].x - fx, 2) + Math.Pow(slots[i].y - fy, 2));
                    if (d < bestD) { bestD = d; best = i; }
                }
            if (best < 0) { t = Math.Min(readyAt.Min(), Sim); continue; }
            t += (Math.Abs(slots[best].y - fy) + Math.Abs(slots[best].x - fx)) / walkSpeed + harvestSec;
            fx = slots[best].x; fy = slots[best].y;
            readyAt[best] = t + growSec; n++;
        }
        return n / (Sim / 60.0);
    }
}
