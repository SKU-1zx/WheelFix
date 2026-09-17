using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace WheelFix
{
    internal static class WheelFilterCoreTests
    {
        private static int _assertions;

        private static void Main()
        {
            Sequence("A", -1, new int[] { -1, -1, 1, -1, 1, 1, -1, -1 });
            Sequence("B", 1, new int[] { 1, -1, 1, -1, -1, 1, 1 });
            RandomGestures();
            ContinuousHalfAndHalf();
            TimeoutBoundaries();
            SyntheticRouting();
            RegressionCases();
            Console.WriteLine("PASS: A-F and regressions; " + _assertions + " assertions.");
        }

        private static void Sequence(string name, int direction, int[] pulses)
        {
            WheelFilterCore filter = NewFilter();
            for (int i = 0; i < pulses.Length; i++)
                Equal(direction * 120, filter.Process(pulses[i] * 120, (uint)(i * 100)).OutputDelta);
            Console.WriteLine("PASS " + name);
        }

        private static void RandomGestures()
        {
            WheelFilterCore filter = NewFilter();
            Random random = new Random(7351);
            uint time = 0;
            Equal(-120, filter.Process(-120, time).OutputDelta);
            for (int i = 0; i < 500; i++)
            {
                time += (uint)random.Next(1, 100);
                Equal(-120, filter.Process(random.Next(2) == 0 ? -120 : 120, time).OutputDelta);
            }
            time += 501;
            WheelFilterResult next = filter.Process(120, time);
            Equal(120, next.OutputDelta);
            Check(next.ResetToIdle);
            for (int i = 0; i < 500; i++)
            {
                time += (uint)random.Next(1, 100);
                Equal(120, filter.Process(random.Next(2) == 0 ? -120 : 120, time).OutputDelta);
            }
            Console.WriteLine("PASS C: random gestures separated by 501ms");
        }

        private static void ContinuousHalfAndHalf()
        {
            WheelFilterCore filter = NewFilter();
            Equal(-120, filter.Process(-120, 0).OutputDelta);
            for (uint i = 1; i <= 10000; i++)
                Equal(-120, filter.Process(i % 2 == 0 ? -120 : 120, i).OutputDelta);
            // Long all-contrary run, well beyond total timeout duration.
            for (uint i = 10001; i <= 11000; i++)
                Equal(-120, filter.Process(120, i).OutputDelta);
            Equal(6000, filter.BlockedCount);
            Console.WriteLine("PASS D: 10,000 pulses at 1ms, then 1,000 contrary pulses");
        }

        private static void TimeoutBoundaries()
        {
            foreach (int timeout in new int[] { 400, 450, 500 })
            {
                foreach (int gap in new int[] { timeout - 1, timeout, timeout + 1 })
                {
                    WheelFilterCore filter = new WheelFilterCore(true, timeout);
                    filter.Process(-120, 10);
                    WheelFilterResult result = filter.Process(120, (uint)(10 + gap));
                    Equal(gap < timeout ? -120 : 120, result.OutputDelta);
                    Check(result.ResetToIdle == (gap >= timeout));
                    Equal(gap, result.GapMs.Value);
                }
            }
            // The last contrary raw pulse, not the last accepted pulse, is the anchor.
            WheelFilterCore active = NewFilter();
            active.Process(-120, 0);
            Equal(-120, active.Process(120, 449).OutputDelta);
            Equal(-120, active.Process(120, 898).OutputDelta);
            Equal(120, active.Process(120, 1348).OutputDelta);
            Console.WriteLine("PASS E: immediate switch at exact configured timeout");
        }

        private static void SyntheticRouting()
        {
            WheelFilterCore filter = NewFilter();
            NativeMouseHook hook = null;
            List<int> delivered = new List<int>();
            int injections = 0;
            hook = new NativeMouseHook(filter, delegate(int output)
            {
                injections++;
                if (injections > 50) throw new Exception("Synthetic recursion");
                // Reenter the actual hook routing with a deliberately distant
                // timestamp: neither the state nor raw deadline may change.
                Check(!hook.HandleWheel(output, 100000, 1, NativeMouseHook.InjectionMarker));
                delivered.Add(output);
                return true;
            });
            int[] raw = { -120, -120, 120, -120, 120, 120, -120, -120 };
            for (int i = 0; i < raw.Length; i++)
                if (!hook.HandleWheel(raw[i], (uint)(i * 10), 0, UIntPtr.Zero))
                    delivered.Add(raw[i]);
            Equal(raw.Length, delivered.Count);
            foreach (int output in delivered) Equal(-120, output);
            Equal(3, injections);
            // Test marker without flags, and each injected flag without marker.
            Check(!hook.HandleWheel(120, 200000, 0, NativeMouseHook.InjectionMarker));
            Check(!hook.HandleWheel(120, 200000, 1, UIntPtr.Zero));
            Check(!hook.HandleWheel(120, 200000, 2, UIntPtr.Zero));
            Equal((int)WheelFilterState.LOCK_DOWN, (int)filter.State);
            Check(hook.HandleWheel(120, 519, 0, UIntPtr.Zero)); // 449ms since raw
            Check(!hook.HandleWheel(120, 969, 0, UIntPtr.Zero)); // exact 450ms
            Equal((int)WheelFilterState.LOCK_UP, (int)filter.State);
            Equal(4, injections);

            WheelFilterCore failing = NewFilter();
            NativeMouseHook failedHook = new NativeMouseHook(failing, delegate(int delta) { return false; });
            Check(!failedHook.HandleWheel(-120, 0, 0, UIntPtr.Zero));
            Check(failedHook.HandleWheel(120, 1, 0, UIntPtr.Zero)); // never leak wrong original
            Equal((int)WheelFilterState.LOCK_DOWN, (int)failing.State);
            Equal(IntPtr.Size == 8 ? 40 : 28, Marshal.SizeOf(typeof(NativeMethods.Input)));
            Console.WriteLine("PASS F: real hook routing, reentrant synthetic bypass, no duplicate output, injection failure");
        }

        private static void RegressionCases()
        {
            WheelFilterCore filter = NewFilter();
            filter.Process(-120, uint.MaxValue - 10);
            Equal(-240, filter.Process(240, 15).OutputDelta);
            Equal(-15, filter.Process(15, 20).OutputDelta);
            Equal(0, filter.Process(0, 460).OutputDelta);
            Equal(120, filter.Process(120, 470).OutputDelta); // zero did not extend deadline
            filter.Enabled = false;
            Equal((int)WheelFilterState.IDLE, (int)filter.State);
            Equal(-120, filter.Process(-120, 471).OutputDelta);
            filter.Enabled = true;
            Equal(120, filter.Process(120, 472).OutputDelta);
            filter.WindowMs = 500;
            Equal((int)WheelFilterState.IDLE, (int)filter.State);
            Equal(-120, filter.Process(-120, 473).OutputDelta);
            filter.ResetBlockedCount();
            Equal(0, filter.BlockedCount);
            filter.WindowMs = 1;
            Equal(400, filter.WindowMs);
            filter.WindowMs = 1500;
            Equal(500, filter.WindowMs);
            // Original reported 406/437ms gaps must no longer unlock at 450ms.
            filter = NewFilter();
            filter.Process(-120, 0);
            Equal(-120, filter.Process(120, 406).OutputDelta);
            Equal(-120, filter.Process(120, 843).OutputDelta);
            Console.WriteLine("PASS regressions: wrap, magnitude, zero, settings, old-log gaps");
        }

        private static WheelFilterCore NewFilter() { return new WheelFilterCore(true, 450); }
        private static void Equal(long expected, long actual)
        {
            _assertions++;
            if (expected != actual) throw new Exception("Expected " + expected + ", got " + actual);
        }
        private static void Check(bool condition)
        {
            _assertions++;
            if (!condition) throw new Exception("Assertion failed");
        }
    }
}
