using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Utils
{
    public struct SW : IDisposable
    {
        private string outputText;
        private bool useTicks;
        private Stopwatch sw;

        public SW(string outputText, bool useTicks = false)
        {
            sw = Stopwatch.StartNew();
            this.outputText = outputText;
            this.useTicks = useTicks;
        }

        public void Dispose()
        {
            sw.Stop();
            UnityEngine.Debug.Log($"{outputText} {(useTicks ? sw.ElapsedTicks : sw.ElapsedMilliseconds)} {(useTicks ? "Ticks" : "ms")}");
        }
    }

    public static class Perf
    {
        public static Stack<long> stack;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Init()
        {
            stack = new Stack<long>();
        }

            
        public static void S() => stack.Push(Stopwatch.GetTimestamp());
        public static void E() => Debug.Log($"Elapsed: {Stopwatch.GetTimestamp() - stack.Pop()} ms");

        public static void Profile()
        {
            stack.Push(Stopwatch.GetTimestamp());
        }

        public static void PopLog()
        {
            Debug.Log($"Elapsed: {Stopwatch.GetTimestamp() - stack.Pop()} ms");
        }
    }
}