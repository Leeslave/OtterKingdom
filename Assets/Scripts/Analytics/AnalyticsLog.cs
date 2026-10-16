using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>이벤트를 어디에 쓸지 (지금은 기기 파일. 원격 수집이 정해지면 이것만 바꿈)</summary>
public interface IAnalyticsSink
{
    void Write(string jsonLine);
}

/// <summary>
/// 테스트용 이벤트 기록 (상세기획서 12장). M1은 로컬 로그: 기기의 analytics/events.jsonl에 한 줄에 하나씩 JSON.
/// 공통 필드: 이벤트 ID, UTC 시각, 익명 설치 ID, 세션 ID, 빌드 버전, 개발 빌드 여부(dev — 지표에서 뺄 것).
/// 개인 정보·자유 입력 원문은 넣지 않는다. 기록 실패는 게임을 막지 않는다.
/// </summary>
public static class AnalyticsLog
{
    private const string InstallIdKey = "analytics_install_id";

    public static IAnalyticsSink Sink { get; set; } = new LocalFileAnalyticsSink();

    public static string SessionId { get; } = Guid.NewGuid().ToString("N");

    private static string _installId;

    public static string InstallId
    {
        get
        {
            if (_installId != null)
                return _installId;
            _installId = PlayerPrefs.GetString(InstallIdKey, null);
            if (string.IsNullOrEmpty(_installId))
            {
                _installId = Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString(InstallIdKey, _installId);
                PlayerPrefs.Save();
            }
            return _installId;
        }
    }

    /// <summary>이벤트 하나. fields는 (이름, 값) 쌍 — 값은 문자열·숫자·bool</summary>
    public static void Track(string eventName, params (string key, object value)[] fields)
    {
        if (string.IsNullOrEmpty(eventName))
            throw new ArgumentException("이벤트 이름이 없습니다.", nameof(eventName));

        try
        {
            Sink?.Write(Format(eventName, fields));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[AnalyticsLog] {eventName} 기록 실패: {e.Message}");
        }
    }

    /// <summary>한 줄 JSON (테스트에서 직접 확인할 수 있게 공개)</summary>
    public static string Format(string eventName, IReadOnlyList<(string key, object value)> fields)
    {
        var sb = new StringBuilder(256);
        sb.Append('{');
        AppendField(sb, "event", eventName, true);
        AppendField(sb, "eventId", Guid.NewGuid().ToString("N"));
        AppendField(sb, "utc", GameClock.UtcNow.ToString("o", CultureInfo.InvariantCulture));
        AppendField(sb, "installId", InstallId);
        AppendField(sb, "sessionId", SessionId);
        AppendField(sb, "build", Application.version);
        AppendField(sb, "dev", Application.isEditor || Debug.isDebugBuild);
        if (fields != null)
        {
            foreach (var (key, value) in fields)
                AppendField(sb, key, value);
        }
        sb.Append('}');
        return sb.ToString();
    }

    private static void AppendField(StringBuilder sb, string key, object value, bool first = false)
    {
        if (!first)
            sb.Append(',');
        AppendString(sb, key);
        sb.Append(':');
        switch (value)
        {
            case null:
                sb.Append("null");
                break;
            case bool b:
                sb.Append(b ? "true" : "false");
                break;
            case int or long or short or byte:
                sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                break;
            case float f:
                sb.Append(float.IsFinite(f) ? f.ToString("0.###", CultureInfo.InvariantCulture) : "null");
                break;
            case double d:
                sb.Append(double.IsFinite(d) ? d.ToString("0.###", CultureInfo.InvariantCulture) : "null");
                break;
            default:
                AppendString(sb, Convert.ToString(value, CultureInfo.InvariantCulture));
                break;
        }
    }

    private static void AppendString(StringBuilder sb, string text)
    {
        sb.Append('"');
        foreach (char c in text)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < ' ')
                        sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else
                        sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
    }
}

/// <summary>기기 파일에 한 줄씩 덧붙임. 2MB가 넘으면 이전 파일(events.old.jsonl) 하나만 남기고 새로 시작</summary>
public class LocalFileAnalyticsSink : IAnalyticsSink
{
    private const long MaxBytes = 2 * 1024 * 1024;

    public static string Folder => Path.Combine(Application.persistentDataPath, "analytics");
    public static string FilePath => Path.Combine(Folder, "events.jsonl");

    public void Write(string jsonLine)
    {
        Directory.CreateDirectory(Folder);
        var info = new FileInfo(FilePath);
        if (info.Exists && info.Length > MaxBytes)
        {
            string old = Path.Combine(Folder, "events.old.jsonl");
            if (File.Exists(old))
                File.Delete(old);
            File.Move(FilePath, old);
        }
        File.AppendAllText(FilePath, jsonLine + "\n");
    }
}
