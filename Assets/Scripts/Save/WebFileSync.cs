#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

/// <summary>
/// 웹 빌드에서 세이브 파일을 브라우저 저장소(IndexedDB)로 바로 옮긴다 (Assets/Plugins/WebGL/WebFileSync.jslib).
/// 웹에서는 파일을 써도 메모리에만 있고, 기본 웹 템플릿은 자동으로 옮기지 않아(autoSyncPersistentDataPath 꺼짐) 새로 고치면 사라졌다.
/// 편집기 · 안드로이드에서는 아무것도 안 함
/// </summary>
public static class WebFileSync
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void OtterSyncFiles();

    public static void Flush() => OtterSyncFiles();
#else
    public static void Flush() { }
#endif
}
