using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>뽑기 연출 순서: 1회(조개 도착 → 톡톡 세 번 → 쩍 → 카드), 10회(뗏목 → 차례로 열림 → 결과), 건너뛰기</summary>
public partial class GachaRevealView
{
    private const int TapCount = 3;
    private static readonly string[] TapHints = { "톡톡! 조개를 두드려 보세요", "한 번 더 톡!", "마지막으로 톡!" };
    private const string RaftHint = "톡! 눌러서 조개를 한꺼번에 열어요";

    private readonly List<int> _order = new List<int>();
    private int _shownLight;

    #region 1회

    /// <param name="auto">두드리기를 저절로 (10회의 에픽 큰 연출)</param>
    /// <param name="showCard">끝나면 결과 카드 (10회 중에는 결과 10칸이 대신함)</param>
    private IEnumerator SingleRoutine(GachaPull pull, bool auto, bool showCard)
    {
        _single.gameObject.SetActive(true);
        ResetSingle();
        _shownLight = 0;
        var path = RevealPath(pull.Tier);

        yield return ShellArrives(auto);

        for (int tap = 0; tap < TapCount; tap++)
        {
            if (auto)
                yield return GachaTween.Wait(tap == 0 ? 0.2f : 0.38f);
            else
                yield return WaitTap(TapHints[tap]);
            // 빛은 마지막 두드림에 맞춰 오름 (길이 2면 세 번째에 승급)
            int light = path[Mathf.Max(0, path.Count - TapCount + tap)];
            yield return Strike(tap, light, pull);
        }

        yield return Open(pull);

        if (showCard)
        {
            yield return GachaTween.Wait(0.3f);
            ShowCard(pull, true);
        }
    }

    // 빛이 지나가는 등급 (승급): 에픽 = 하양→파랑→금 30% · 파랑→금 50% · 처음부터 금 20%, 레어 = 하양→파랑 35%
    private static List<int> RevealPath(int tier)
    {
        float r = Random.value;
        if (tier >= 2)
            return r < 0.3f ? new List<int> { 0, 1, 2 } : r < 0.8f ? new List<int> { 1, 2 } : new List<int> { 2 };
        if (tier == 1)
            return r < 0.35f ? new List<int> { 0, 1 } : new List<int> { 1 };
        return new List<int> { 0 };
    }

    // 빛 등급 → 조개 모습 (픽업 에픽은 벚꽃 조개)
    private static int LookFor(int light, GachaPull pull) =>
        light >= 2 ? (pull.Featured ? LookPickup : LookEpic) : light == 1 ? LookRare : LookCommon;

    // 파도를 타고 오른쪽에서 떠내려와 해달 배 위로 퐁
    private IEnumerator ShellArrives(bool quick)
    {
        _otter.sprite = _otterRest;
        _shellRoot.gameObject.SetActive(true);
        // 물결 윗선에 아랫부분만 잠겨 떠서 옴
        var onWater = _shellRest + new Vector2(300f, -60f);
        var start = _shellRest + new Vector2(950f, -60f);
        _shellRoot.localScale = Vector3.one * 0.8f;
        yield return GachaTween.Run(quick ? 0.45f : 1f, t =>
        {
            var p = Vector2.Lerp(start, onWater, GachaTween.OutCubic(t));
            p.y += Mathf.Sin(t * Mathf.PI * 3f) * 10f;
            _shellRoot.anchoredPosition = p;
            _shellRoot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 4f) * 8f);
        });

        AudioManager.Play(SfxKind.Click);
        yield return GachaTween.Run(quick ? 0.3f : 0.42f, t =>
        {
            var p = Vector2.Lerp(onWater, _shellRest, t);
            p.y += GachaTween.Bump(t) * 180f;
            _shellRoot.anchoredPosition = p;
            _shellRoot.localScale = Vector3.one * Mathf.Lerp(0.8f, 1f, t);
            _shellRoot.localRotation = Quaternion.Euler(0f, 0f, (1f - t) * 25f);
        });
        _shellRoot.anchoredPosition = _shellRest;
        _shellRoot.localRotation = Quaternion.identity;
        _otter.sprite = _otterReady;

        // 배 위에 얹힐 때 통
        yield return GachaTween.Run(0.18f, t =>
        {
            float b = GachaTween.Bump(t);
            _shell.rectTransform.localScale = new Vector3(1f + 0.12f * b, 1f - 0.12f * b, 1f);
        });
    }

    // 톡: 금이 한 단계 길어지고 그 사이로 빛이 샘. 빛 등급이 오르면 승급
    private IEnumerator Strike(int tap, int light, GachaPull pull)
    {
        AudioManager.Play(SfxKind.ShellTap);
        Shake(8f + tap * 5f, 0.2f);
        StartCoroutine(Punch());

        var color = LightColor(light, pull.Featured);
        _crackCore.sprite = _crackCores[tap];
        _crackGlow.sprite = _crackGlows[tap];
        _crackCore.gameObject.SetActive(true);
        _crackGlow.gameObject.SetActive(true);
        _crackGlow.color = color;
        _shellGlow.gameObject.SetActive(true);
        _shellGlow.color = GachaTween.WithAlpha(color, 0.4f + 0.2f * tap);
        _shellGlowScale = 1f + 0.2f * tap;
        Burst(_shell.rectTransform.position, color, 3 + tap * 2, 130f + 40f * tap, 44f);

        if (light > _shownLight)
        {
            _shownLight = light;
            yield return Upgrade(light, pull);
        }
        yield return GachaTween.Wait(0.12f);
    }

    // 돌로 톡: 해달이 살짝 내려갔다 올라오고 조개는 눌렸다 펴짐
    private IEnumerator Punch()
    {
        yield return GachaTween.Run(0.22f, t =>
        {
            float b = GachaTween.Bump(t);
            _otterPunch = new Vector2(0f, -14f * b);
            _shell.rectTransform.localScale = new Vector3(1f + 0.14f * b, 1f - 0.14f * b, 1f);
        });
        _otterPunch = Vector2.zero;
    }

    // 승급: 번쩍 + 조개가 그 등급의 조개로 바뀌며 통. 에픽이면 밤바다로 (픽업 에픽은 벚꽃잎도)
    private IEnumerator Upgrade(int light, GachaPull pull)
    {
        AudioManager.Play(SfxKind.RevealUpgrade);
        StartCoroutine(Flash(0.6f, 0.3f));
        var color = LightColor(light, pull.Featured);
        Burst(_shell.rectTransform.position, color, 10, 260f, 60f);
        _shell.sprite = _shellSprites[LookFor(light, pull)];
        if (light >= 2)
            StartCoroutine(GoNight(pull.Featured, 0.9f));
        yield return GachaTween.Run(0.3f, t => _shellRoot.localScale = Vector3.one * Mathf.LerpUnclamped(1.35f, 1f, GachaTween.OutBack(t)));
    }

    // 쩍: 떨림 → 번쩍 · 빛 고리 → 반쪽이 양옆으로 → 장난감이 빛살과 함께 튀어 오름
    private IEnumerator Open(GachaPull pull)
    {
        int light = Mathf.Clamp(pull.Tier, 0, 2);
        var color = LightColor(light, pull.Featured);
        int look = LookFor(light, pull);

        _shellGlow.color = GachaTween.WithAlpha(color, 0.95f);
        yield return GachaTween.Run(0.5f, t =>
        {
            _shellRoot.anchoredPosition = _shellRest + Random.insideUnitCircle * (3f + 7f * t);
            _shellGlowScale = Mathf.Lerp(1.4f, 2.1f, t);
        });
        _shellRoot.anchoredPosition = _shellRest;

        AudioManager.Play(SfxKind.ShellCrack);
        StartCoroutine(Flash(0.85f, 0.45f));
        Shake(24f, 0.35f);
        var center = _shell.rectTransform.position;
        StartCoroutine(RingRoutine(center, color, 3.2f, 0.65f));
        Burst(center, color, 14, 420f, 70f);
        _shell.gameObject.SetActive(false);
        _crackCore.gameObject.SetActive(false);
        _crackGlow.gameObject.SetActive(false);
        _shellGlow.gameObject.SetActive(false);
        _shellLeft.sprite = _shellLeftSprites[look];
        _shellRight.sprite = _shellRightSprites[look];
        _shellLeft.gameObject.SetActive(true);
        _shellRight.gameObject.SetActive(true);
        StartCoroutine(SplitRoutine());
        _otter.sprite = _otterSurprise;

        AudioManager.Play(pull.IsEpic ? SfxKind.RevealEpic : pull.Tier == 1 ? SfxKind.RevealRare : SfxKind.RevealCommon);
        _toy.sprite = pull.Item.Icon;
        _toyRoot.gameObject.SetActive(true);
        _toyRoot.position = center;
        var from = _toyRoot.anchoredPosition;
        _rays.gameObject.SetActive(true);
        _rays.color = GachaTween.WithAlpha(color, 0f);
        _toyGlow.color = GachaTween.WithAlpha(color, 0f);
        float raysTarget = pull.IsEpic ? 0.95f : pull.Tier == 1 ? 0.7f : 0.45f;
        yield return GachaTween.Run(0.65f, t =>
        {
            _toyRoot.anchoredPosition = Vector2.Lerp(from, _toyRest, GachaTween.OutCubic(t));
            _toy.rectTransform.localScale = Vector3.one * Mathf.LerpUnclamped(0.15f, 1f, GachaTween.OutBack(t, 2.2f));
            _rays.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.4f, 1f, GachaTween.OutCubic(t));
            _raysAlpha = raysTarget * t;
            GachaTween.SetAlpha(_toyGlow, 0.85f * t);
        });
        _toyFloating = true;
        Burst(_toy.rectTransform.position, color, pull.IsEpic ? 12 : 7, 330f, 60f);
    }

    private IEnumerator SplitRoutine()
    {
        var left = _shellLeft.rectTransform;
        var right = _shellRight.rectTransform;
        yield return GachaTween.Run(0.7f, t =>
        {
            float k = GachaTween.OutCubic(t);
            float y = 40f * GachaTween.Bump(t) - 90f * GachaTween.InCubic(t);
            left.anchoredPosition = new Vector2(-190f * k, y);
            right.anchoredPosition = new Vector2(190f * k, y);
            left.localRotation = Quaternion.Euler(0f, 0f, 32f * k);
            right.localRotation = Quaternion.Euler(0f, 0f, -32f * k);
            float alpha = 1f - GachaTween.InCubic(t);
            GachaTween.SetAlpha(_shellLeft, alpha);
            GachaTween.SetAlpha(_shellRight, alpha);
        });
        _shellLeft.gameObject.SetActive(false);
        _shellRight.gameObject.SetActive(false);
    }

    private void ShowCard(GachaPull pull, bool animate)
    {
        var (hint, portrait) = _describe != null ? _describe(pull) : (string.Empty, null);
        _card.Show(pull, hint, portrait, _again, animate);
        _skipButton.gameObject.SetActive(false);
        _tapHintRoot.gameObject.SetActive(false);
        _finished = true;
    }

    // 건너뛴 1회: 마지막 모습 (튀어나온 장난감 + 빛살, 에픽이면 밤바다)
    private void ShowSingleFinal(GachaPull pull)
    {
        _single.gameObject.SetActive(true);
        int light = Mathf.Clamp(pull.Tier, 0, 2);
        var color = LightColor(light, pull.Featured);
        if (pull.IsEpic)
            SetNightNow(pull.Featured);
        _otter.sprite = _otterSurprise;
        _otterPunch = Vector2.zero;
        _shellRoot.gameObject.SetActive(false);
        _toyRoot.gameObject.SetActive(true);
        _toyRoot.anchoredPosition = _toyRest;
        _toy.sprite = pull.Item.Icon;
        _toy.rectTransform.localScale = Vector3.one;
        _rays.gameObject.SetActive(true);
        _rays.rectTransform.localScale = Vector3.one;
        _rays.color = color;
        _raysAlpha = pull.IsEpic ? 0.95f : pull.Tier == 1 ? 0.7f : 0.45f;
        _toyGlow.color = GachaTween.WithAlpha(color, 0.85f);
        _toyFloating = true;
    }

    #endregion

    #region 10회 (뗏목)

    private IEnumerator RaftRoutine()
    {
        _raft.gameObject.SetActive(true);
        var pulls = _report.Pulls;
        int best = _report.BestIndex;

        // 가장 좋은 것은 마지막에
        _order.Clear();
        for (int i = 0; i < pulls.Count; i++)
        {
            if (i != best)
                _order.Add(i);
        }
        _order.Add(best);

        for (int k = 0; k < _raftSlots.Length; k++)
        {
            var slot = _raftSlots[k];
            bool has = k < _order.Count;
            slot.gameObject.SetActive(has);
            if (!has)
                continue;
            slot.Phase = k * 0.9f;
            slot.Otter.sprite = _otterRest;
            slot.Shell.sprite = _shellSprites[LookCommon];
            slot.Shell.gameObject.SetActive(true);
            slot.Shell.rectTransform.localScale = Vector3.one;
            GachaTween.SetAlpha(slot.Shell, 1f);
            slot.Glow.gameObject.SetActive(false);
            slot.Toy.gameObject.SetActive(false);
            slot.NewBadge.SetActive(false);
            slot.transform.localScale = Vector3.zero;
        }

        // 뗏목이 한 마리씩 둥실 떠오름
        int count = _order.Count;
        yield return GachaTween.Run(0.8f, t =>
        {
            for (int k = 0; k < count; k++)
                _raftSlots[k].transform.localScale = Vector3.one * GachaTween.OutBack(Mathf.Clamp01(t * 2f - k * 0.1f));
        });

        yield return WaitTap(RaftHint);

        for (int k = 0; k < count; k++)
        {
            bool last = k == count - 1;
            if (last)
                yield return GachaTween.Wait(0.35f);
            yield return OpenSlot(_raftSlots[k], pulls[_order[k]], last);
            if (!last)
                yield return GachaTween.Wait(0.1f);
        }
        yield return GachaTween.Wait(0.6f);

        // 에픽이 나왔으면 그 조개를 크게 한 번 더 (두드리기는 저절로)
        var bestPull = pulls[best];
        if (bestPull.IsEpic)
        {
            yield return GachaTween.Run(0.25f, t => _raft.localScale = Vector3.one * (1f - 0.1f * t));
            _raft.gameObject.SetActive(false);
            _raft.localScale = Vector3.one;
            yield return SingleRoutine(bestPull, true, false);
            yield return GachaTween.Wait(1.1f);
        }
        // 결과 10칸만 보이게 뗏목 · 큰 연출을 살짝 걷어 냄 (결과 뒤 막이 같이 어두워짐)
        yield return FadeStageOut(0.25f);
        ShowResults(true);
    }

    private IEnumerator FadeStageOut(float seconds)
    {
        var groups = new List<CanvasGroup>();
        foreach (var stage in new[] { _raft, _single })
        {
            if (!stage.gameObject.activeSelf)
                continue;
            var group = stage.GetComponent<CanvasGroup>();
            if (group == null)
                group = stage.gameObject.AddComponent<CanvasGroup>();
            groups.Add(group);
        }
        yield return GachaTween.Run(seconds, t =>
        {
            foreach (var group in groups)
                group.alpha = 1f - t;
        });
        _raft.gameObject.SetActive(false);
        _single.gameObject.SetActive(false);
        foreach (var group in groups)
            group.alpha = 1f;
    }

    private IEnumerator OpenSlot(GachaRaftSlotView slot, GachaPull pull, bool last)
    {
        int light = Mathf.Clamp(pull.Tier, 0, 2);
        var color = LightColor(light, pull.Featured);
        AudioManager.Play(pull.IsEpic ? SfxKind.RevealUpgrade : pull.Tier == 1 ? SfxKind.RevealRare : SfxKind.ShellTap);
        slot.Glow.gameObject.SetActive(true);
        slot.Glow.color = GachaTween.WithAlpha(color, pull.Tier > 0 ? 0.95f : 0.55f);
        slot.Shell.sprite = _shellSprites[LookFor(light, pull)];
        if (pull.Tier > 0)
            Burst(slot.Shell.rectTransform.position, color, pull.IsEpic ? 10 : 5, pull.IsEpic ? 220f : 140f, 40f);
        if (last || pull.IsEpic)
            Shake(pull.IsEpic ? 14f : 8f, 0.25f);

        var shell = slot.Shell.rectTransform;
        yield return GachaTween.Run(0.16f, t =>
        {
            float b = GachaTween.Bump(t);
            shell.localScale = new Vector3(1f + 0.25f * b, 1f - 0.15f * b, 1f);
        });

        slot.Otter.sprite = _otterSurprise;
        slot.Toy.sprite = pull.Item.Icon;
        slot.Toy.gameObject.SetActive(true);
        slot.NewBadge.SetActive(pull.IsNew);
        var toy = slot.Toy.rectTransform;
        yield return GachaTween.Run(0.28f, t =>
        {
            shell.localScale = Vector3.one * Mathf.Lerp(1f, 1.5f, t);
            GachaTween.SetAlpha(slot.Shell, 1f - t);
            toy.localScale = Vector3.one * GachaTween.OutBack(t, 2f);
        });
        slot.Shell.gameObject.SetActive(false);
    }

    private void ShowResults(bool animate)
    {
        _results.Show(_report, _again, animate);
        _skipButton.gameObject.SetActive(false);
        _tapHintRoot.gameObject.SetActive(false);
        _finished = true;
    }

    #endregion

    #region 건너뛰기

    private void Skip()
    {
        if (_finished)
            return;

        StopAllCoroutines();
        _waitingTap = false;
        _tapHintRoot.gameObject.SetActive(false);
        GachaTween.SetAlpha(_flash, 0f);
        _shakeStrength = 0f;
        _shakeRoot.anchoredPosition = Vector2.zero;
        _otterPunch = Vector2.zero;
        ClearChildren(_sparkleLayer);
        _ring.gameObject.SetActive(false);

        if (_report.Pulls.Count == 1)
        {
            var pull = _report.Pulls[0];
            ShowSingleFinal(pull);
            ShowCard(pull, true);
            return;
        }

        var best = _report.Pulls[_report.BestIndex];
        if (best.IsEpic)
            SetNightNow(best.Featured);
        _raft.gameObject.SetActive(false);
        _raft.localScale = Vector3.one;
        _single.gameObject.SetActive(false);
        ShowResults(true);
    }

    #endregion
}
