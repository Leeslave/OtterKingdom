using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// 연구 트리 배치표: 모든 기술이 자기 분야에 한 번씩 · 선행은 같은 화면에 있음(다른 분야는 흐린 칸) · 선행은 위 줄(같은 줄이면 옆의 흐린 칸) ·
/// 한 칸에 하나 · 흐린 칸은 다른 분야이면서 그 화면 기술의 선행. 기술을 늘릴 때 표를 안 고치면 여기서 걸림.
/// </summary>
public class ResearchTreeLayoutTests
{
    [Test]
    public void EveryTech_PlacedOnceInItsBranch()
    {
        var seen = new Dictionary<string, TechBranch>();
        foreach (var branch in ResearchTreeLayout.Branches)
        {
            foreach (var id in ResearchTreeLayout.TechsOf(branch))
            {
                Assert.IsFalse(seen.ContainsKey(id), $"{id}가 두 번 놓임");
                seen[id] = branch;
                Assert.AreEqual(ResearchTreeLayout.BranchOf(id), branch, $"{id}는 {ResearchTreeLayout.Name(ResearchTreeLayout.BranchOf(id))} 분야");
                Assert.IsNotNull(ProsperityResearch.Find(id), $"{id}는 ProsperityResearch.Techs에 없음");
            }
        }
        foreach (var tech in ProsperityResearch.Techs)
            Assert.IsTrue(seen.ContainsKey(tech.Id), $"{tech.Id} {tech.Name}이 트리에 없음 — ResearchTreeLayout 표에 추가");
    }

    [Test]
    public void Prerequisites_AboveOrBesideAsGhost()
    {
        foreach (var branch in ResearchTreeLayout.Branches)
        {
            var row = new Dictionary<string, int>();
            var ghosts = new HashSet<string>();
            var cells = new HashSet<(int, TreeColumn)>();
            int index = 0;
            foreach (var band in ResearchTreeLayout.For(branch))
            {
                foreach (var slots in band.Rows)
                {
                    foreach (var slot in slots)
                    {
                        Assert.IsTrue(cells.Add((index, slot.Column)), $"{ResearchTreeLayout.Name(branch)} {index}번 줄 {slot.Column} 칸이 겹침");
                        string key = slot.Ghost ? "~" + slot.TechId : slot.TechId;
                        row[key] = index;
                        if (slot.Ghost)
                        {
                            ghosts.Add(slot.TechId);
                            Assert.AreNotEqual(branch, ResearchTreeLayout.BranchOf(slot.TechId), $"흐린 칸 {slot.TechId}는 다른 분야여야 함");
                        }
                    }
                    index++;
                }
            }

            foreach (var id in ResearchTreeLayout.TechsOf(branch))
            {
                var tech = ProsperityResearch.Find(id);
                foreach (var pre in tech.Prerequisites)
                {
                    bool real = row.TryGetValue(pre, out int preRow);
                    bool ghost = !real && row.TryGetValue("~" + pre, out preRow);
                    Assert.IsTrue(real || ghost, $"{tech.Name}의 선행 {pre}가 {ResearchTreeLayout.Name(branch)} 화면에 없음 (다른 분야면 흐린 칸으로)");
                    Assert.IsTrue(preRow < row[id] || ghost && preRow == row[id], $"{tech.Name}의 선행 {pre}는 위 줄에 (같은 줄이면 흐린 칸)");
                }
            }

            foreach (var ghost in ghosts)
            {
                bool used = false;
                foreach (var id in ResearchTreeLayout.TechsOf(branch))
                    used |= ((IList<string>)ProsperityResearch.Find(id).Prerequisites).Contains(ghost);
                Assert.IsTrue(used, $"흐린 칸 {ghost}를 선행으로 쓰는 기술이 {ResearchTreeLayout.Name(branch)}에 없음");
            }
        }
    }

    [Test]
    public void Bands_LevelRangeAndBranchNames()
    {
        var bands = ResearchTreeLayout.For(TechBranch.Industry);
        Assert.AreEqual((20, 23), ResearchTreeLayout.Levels(bands[0]), "제재 기술 Lv.20 · 석재 규격화 Lv.23");
        Assert.AreEqual(TechBranch.Industry, ResearchTreeLayout.BranchOf(TechIds.IndustrialLogistics), "융합(X)은 산업 화면");
        Assert.AreEqual(TechBranch.Logistics, ResearchTreeLayout.BranchOf(TechIds.Cart));
        Assert.AreEqual(TechBranch.City, ResearchTreeLayout.BranchOf(TechIds.Palace));
        Assert.AreEqual(TechBranch.Economy, ResearchTreeLayout.BranchOf(TechIds.Market));
        Assert.AreEqual("물류", ResearchTreeLayout.Name(TechBranch.Logistics));
    }
}
