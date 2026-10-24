using System;
using System.Reflection;
using NUnit.Framework;

// 동일한 NUnit 테스트를 Unity 라이선스 없이 실행한다. Unity API를 호출하는 테스트는 이 실행기에 넣지 않는다.
public static class ScoutingRunner
{
    public static int Main()
    {
        int passed = 0, failed = 0;
        foreach (var method in typeof(ScoutingProgressTests).GetMethods())
        {
            if (method.GetCustomAttribute<TestAttribute>() == null) continue;
            try { method.Invoke(new ScoutingProgressTests(), null); passed++; }
            catch (Exception ex) { failed++; Console.Error.WriteLine($"FAIL {method.Name}: {(ex.InnerException ?? ex).Message}"); }
        }
        Console.WriteLine($"Scouting NUnit checks: {passed} passed, {failed} failed");
        return failed == 0 ? 0 : 1;
    }
}
