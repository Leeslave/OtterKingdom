using System;
using System.Reflection;
using NUnit.Framework;

public static class RuntimeModelRunner
{
    public static int Main()
    {
        int passed = 0, failed = 0;
        foreach (var method in typeof(ScoutingIntegrationTests).GetMethods())
        {
            if (method.GetCustomAttribute<TestAttribute>() == null) continue;
            try { method.Invoke(new ScoutingIntegrationTests(), null); passed++; }
            catch (Exception ex) { failed++; Console.Error.WriteLine($"FAIL {method.Name}: {(ex.InnerException ?? ex)}"); }
        }
        Console.WriteLine($"Settlement integration checks: {passed} passed, {failed} failed");
        return failed == 0 ? 0 : 1;
    }
}
