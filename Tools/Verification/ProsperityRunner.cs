using System;
using System.Reflection;
using NUnit.Framework;

public static class ProsperityRunner
{
    public static int Main()
    {
        int passed = 0, failed = 0;
        foreach (var type in new[] { typeof(ProsperityTests), typeof(ProsperityProgressTests), typeof(ProsperityMarketTests), typeof(ProsperityOnboardingTests), typeof(SaveMigrationTests), typeof(ProsperityQuestPlanTests) })
        foreach (var method in type.GetMethods())
        {
            if (method.GetCustomAttribute<TestAttribute>() == null) continue;
            try { method.Invoke(Activator.CreateInstance(type), null); passed++; }
            catch (Exception ex) { failed++; Console.Error.WriteLine($"FAIL {type.Name}.{method.Name}: {ex.InnerException ?? ex}"); }
        }
        Console.WriteLine($"Prosperity/save checks: {passed} passed, {failed} failed");
        return failed == 0 ? 0 : 1;
    }
}
