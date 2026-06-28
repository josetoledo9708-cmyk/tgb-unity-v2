using System;
using System.Collections.Generic;

namespace Game.Core.Tests
{
    /// <summary>
    /// Mini-runner de tests sin dependencias externas (corre con `dotnet run`).
    /// Se reemplazará por NUnit EditMode al portar a Unity.
    /// </summary>
    public sealed class TestRunner
    {
        private int _passed;
        private int _failed;
        private readonly List<string> _failures = new();

        public void Case(string name, Action body)
        {
            try
            {
                body();
                _passed++;
                Console.WriteLine($"  PASS  {name}");
            }
            catch (Exception ex)
            {
                _failed++;
                _failures.Add($"{name}: {ex.Message}");
                Console.WriteLine($"  FAIL  {name}  -> {ex.Message}");
            }
        }

        public int Summarize()
        {
            Console.WriteLine();
            Console.WriteLine($"== {_passed} passed, {_failed} failed ==");
            return _failed == 0 ? 0 : 1;
        }

        // --- aserciones ---

        public static void AreEqual(object expected, object actual, string? msg = null)
        {
            if (!Equals(expected, actual))
                throw new Exception(msg ?? $"esperado <{expected}>, fue <{actual}>");
        }

        public static void IsTrue(bool cond, string msg)
        {
            if (!cond) throw new Exception(msg);
        }
    }
}
