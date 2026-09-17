using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace WheelFix
{
    internal sealed class NativeMouseHook : IDisposable
    {
        private const int WhMouseLl = 14;
        private const int WmMouseWheel = 0x020A;
        private const uint InjectedFlags = 0x00000003;
        internal static readonly UIntPtr InjectionMarker = new UIntPtr(0x57465831U);
        private readonly Func<int, bool> _sendWheel;
        private readonly Queue<string> _traces = new Queue<string>();
        private int _droppedTraces;

        private readonly WheelFilterCore _filter;
        private readonly NativeMethods.LowLevelMouseProc _callback;
        private IntPtr _hookHandle;

        public NativeMouseHook(WheelFilterCore filter) : this(filter, SendWheel) { }

        // The same routing used by the real callback is exercised by tests.
        internal NativeMouseHook(WheelFilterCore filter, Func<int, bool> sendWheel)
        {
            if (filter == null)
            {
                throw new ArgumentNullException("filter");
            }

            _filter = filter;
            _sendWheel = sendWheel;
            _callback = HookCallback;
            _hookHandle = IntPtr.Zero;
        }

        public void Start()
        {
            if (_hookHandle != IntPtr.Zero)
            {
                return;
            }

            IntPtr moduleHandle = NativeMethods.GetModuleHandle(null);
            _hookHandle = NativeMethods.SetWindowsHookEx(
                WhMouseLl, _callback, moduleHandle, 0U);

            if (_hookHandle == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(),
                    L.Text(
                        "Windows rejected the global mouse hook.",
                        "Windows non ha accettato l'hook globale del mouse."));
            }
        }

        public void Dispose()
        {
            if (_hookHandle != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hookHandle);
                _hookHandle = IntPtr.Zero;
            }

            GC.SuppressFinalize(this);
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam.ToInt32() == WmMouseWheel)
            {
                NativeMethods.MsLlHookStruct data =
                    (NativeMethods.MsLlHookStruct)Marshal.PtrToStructure(
                        lParam, typeof(NativeMethods.MsLlHookStruct));

                int delta = unchecked((short)(data.MouseData >> 16));
                if (HandleWheel(delta, data.Time, data.Flags, data.ExtraInfo))
                    return new IntPtr(1);
            }

            return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        // True means swallow the original; false means pass it exactly once.
        internal bool HandleWheel(int delta, uint timestamp, uint flags, UIntPtr extraInfo)
        {
            // Check before touching filter state/time. A reentrant SendInput
            // callback must pass through and must never create another input.
            bool own = extraInfo == InjectionMarker;
            if (own || (flags & InjectedFlags) != 0U)
            {
                Trace("t=" + timestamp + " raw=" + delta + " state=" + _filter.State +
                    " gap_ms=NA output=" + delta + " reason=" +
                    (own ? "own_synthetic_bypass" : "external_synthetic_bypass"));
                return false;
            }

            WheelFilterResult result = _filter.Process(delta, timestamp);
            string gap = result.GapMs.HasValue ? result.GapMs.Value.ToString() : "NA";
            if (result.ResetToIdle)
                Trace("t=" + timestamp + " state=IDLE gap_ms=" + gap +
                    " reason=idle_timeout reset_at=" +
                    unchecked(timestamp - result.GapMs.Value + (uint)_filter.WindowMs));

            bool corrected = result.OutputDelta != delta;
            bool sent = !corrected || _sendWheel(result.OutputDelta);
            int error = sent ? 0 : Marshal.GetLastWin32Error();
            Trace("t=" + timestamp + " raw=" + delta + " raw_dir=" +
                (delta > 0 ? "UP" : delta < 0 ? "DOWN" : "ZERO") +
                " state=" + result.State + " gap_ms=" + gap +
                " output=" + (sent ? result.OutputDelta : 0) +
                " requested=" + result.OutputDelta + " reason=" +
                (sent ? result.Reason : "sendinput_failed") + " win32_error=" + error);
            // Even if injection fails, never leak the contrary original and
            // never retry/queue it later. The failure remains visible in logs.
            return corrected;
        }

        private static bool SendWheel(int delta)
        {
            NativeMethods.Input input = new NativeMethods.Input();
            input.Type = 0U; // INPUT_MOUSE
            input.Mouse.MouseData = unchecked((uint)delta);
            input.Mouse.Flags = 0x0800U; // MOUSEEVENTF_WHEEL
            input.Mouse.ExtraInfo = InjectionMarker;
            return NativeMethods.SendInput(1U, new NativeMethods.Input[] { input },
                Marshal.SizeOf(typeof(NativeMethods.Input))) == 1U;
        }

        private void Trace(string message)
        {
            // Bound diagnostics only; wheel events are never buffered.
            if (_traces.Count < 2048) _traces.Enqueue(message);
            else _droppedTraces++;
        }

        internal void FlushTrace()
        {
            while (_traces.Count != 0) DiagnosticLog.Write(_traces.Dequeue());
            if (_droppedTraces != 0)
            {
                DiagnosticLog.Write("Trace overflow: dropped=" + _droppedTraces);
                _droppedTraces = 0;
            }
        }

    }

    internal static class NativeMethods
    {
        internal delegate IntPtr LowLevelMouseProc(
            int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        internal struct Point
        {
            internal int X;
            internal int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MsLlHookStruct
        {
            internal Point Pt;
            internal uint MouseData;
            internal uint Flags;
            internal uint Time;
            internal UIntPtr ExtraInfo;
        }

        // MOUSEINPUT is the largest INPUT union member on both x86 and x64.
        // Sequential alignment yields cbSize 28 / 40 respectively.
        [StructLayout(LayoutKind.Sequential)]
        internal struct MouseInput
        {
            internal int Dx;
            internal int Dy;
            internal uint MouseData;
            internal uint Flags;
            internal uint Time;
            internal UIntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Input
        {
            internal uint Type;
            internal MouseInput Mouse;
        }

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern uint SendInput(uint count, Input[] inputs, int size);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr SetWindowsHookEx(
            int idHook,
            LowLevelMouseProc callback,
            IntPtr moduleHandle,
            uint threadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnhookWindowsHookEx(IntPtr hookHandle);

        [DllImport("user32.dll")]
        internal static extern IntPtr CallNextHookEx(
            IntPtr hookHandle,
            int nCode,
            IntPtr wParam,
            IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr GetModuleHandle(string moduleName);

    }
}
