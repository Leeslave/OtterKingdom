using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.TestTools;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class QAUiRegressionTests
{
    [UnityTearDown]
    public IEnumerator LeavePlayModeAfterFailure()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
    private static T Field<T>(object target, string name) =>
        (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    [TestCase(775f, 2500f)]
    [TestCase(1080f, 1920f)]
    [TestCase(2500f, 775f)]
    public void ScrollSheet_StaysInsideParentAtNarrowPortraitAndLandscape(float width, float height)
    {
        var root = new GameObject("QA Screen", typeof(RectTransform));
        try
        {
            var screen = root.GetComponent<RectTransform>();
            screen.sizeDelta = new Vector2(width, height);
            var content = RuntimeUIScrollSheet.Create(screen, () => { });
            var fit = content.GetComponentInParent<RuntimeSheetLayout>();
            fit.Refresh();
            var frame = (RectTransform)fit.transform;
            var corners = new Vector3[4];
            frame.GetWorldCorners(corners);
            foreach (var corner in corners)
                Assert.IsTrue(screen.rect.Contains(screen.InverseTransformPoint(corner)), $"창 모서리가 {width}×{height} 화면을 벗어남");
            Assert.LessOrEqual(frame.rect.width, 1020f);
            Assert.IsNotNull(content.GetComponentInParent<ScrollRect>());
        }
        finally { Object.DestroyImmediate(root); }
    }

    [UnityTest]
    public IEnumerator ClosingOverlay_StopsRepeatedClicksAndPausesToastBehindPopup()
    {
        yield return new EnterPlayMode();
        GameNotices.Clear();
        var canvas = RuntimeUIKit.CreateCanvas("QA Underlying UI", 100, false);
        var button = RuntimeUIKit.CreateButton(canvas.transform, "뒤 버튼", () => { });
        RuntimeUIKit.Stretch(button.GetComponent<RectTransform>());
        Canvas.ForceUpdateCanvases();
        UITransitionShield.Block(.3f);
        yield return null;
        var data = new PointerEventData(EventSystem.current) { position = new Vector2(Screen.width * .5f, Screen.height * .5f) };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(data, hits);
        Assert.Greater(hits.Count, 0);
        Assert.IsNotNull(hits[0].gameObject.GetComponentInParent<UITransitionShield>(), "닫기 직후에는 뒤 재화 버튼 대신 투명 차단막이 입력을 받아야 함");
        yield return new WaitForSecondsRealtime(.35f);
        Assert.IsFalse(UITransitionShield.IsBlocking);
        Object.Destroy(canvas.gameObject);
        GameNotices.Post(new GameNotice("QA 알림"));
        yield return null;
        yield return null;
        var toast = Object.FindAnyObjectByType<GameNoticeToast>();
        Assert.IsTrue(GameNoticeToast.IsShowing);
        var panel = Field<RectTransform>(toast, "_panel");
        var modal = RuntimeUIKit.CreateCanvas("QA Quest Popup", 200, false);
        RuntimeUIKit.CreateDim(modal.transform);
        yield return null;
        Assert.IsFalse(panel.gameObject.activeSelf, "퀘스트·가방 창이 열리면 알림이 버튼을 가리지 않아야 함");
        float timer = Field<float>(toast, "_timer");
        yield return new WaitForSecondsRealtime(.2f);
        Assert.AreEqual(timer, Field<float>(toast, "_timer"), .01f, "가려진 알림의 읽기 시간은 줄지 않아야 함");
        Object.Destroy(modal.gameObject);
        yield return null;
        yield return null;
        Assert.IsTrue(panel.gameObject.activeSelf);
        GameNotices.Clear();
        yield return new ExitPlayMode();
    }
}
