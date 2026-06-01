using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace lightclip {
	internal class ExternMonitor {
		public static IntPtr GetCursorMonitor() {
			GetCursorPos(out POINT point);
			return MonitorFromPoint(point, MONITOR_DEFAULTTONEAREST);
		}

		[DllImport("user32.dll")]
		static extern bool GetCursorPos(out POINT lpPoint);

		[DllImport("user32.dll")]
		static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

		[DllImport("user32.dll")]
		static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

		[StructLayout(LayoutKind.Sequential)]
		struct POINT {
			public int X;
			public int Y;
		}

		const uint MONITOR_DEFAULTTONEAREST = 2;
		const int CCHDEVICENAME = 32;
		const uint MONITOR_DEFAULTTOPRIMARY = 0x00000001;

		[StructLayout(LayoutKind.Sequential)]
		class MONITORINFOEX {
			public int cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));
			public RECT rcMonitor = new RECT();
			public RECT rcWork = new RECT();
			public int dwFlags = 0;

			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHDEVICENAME)]
			public string szDevice = string.Empty;
		}

		[StructLayout(LayoutKind.Sequential)]
		struct RECT {
			public int Left;
			public int Top;
			public int Right;
			public int Bottom;
		}

		[DllImport("user32.dll")]
		static extern bool GetMonitorInfo(IntPtr hMonitor, [In, Out] MONITORINFOEX lpmi);

		public static string GetMonitorDeviceName(IntPtr monitor) {
			if (monitor == IntPtr.Zero) return null;

			MONITORINFOEX info = new MONITORINFOEX();
			if (GetMonitorInfo(monitor, info)) {
				return info.szDevice;
			}

			return null;
		}

		public static string GetCurMonitor() {
			return GetMonitorDeviceName(GetCursorMonitor());
		}

		public static Rect GetMonitorSize(IntPtr monitor) {
			if (monitor == IntPtr.Zero) return Rect.Empty;

			MONITORINFOEX info = new MONITORINFOEX();
			if (GetMonitorInfo(monitor, info)) {
				return new Rect(0, 0, info.rcMonitor.Right - info.rcMonitor.Left, info.rcMonitor.Bottom - info.rcMonitor.Top);
			}

			return Rect.Empty;
		}

		public static IntPtr GetMonitorFromWindow(Window window) {
			return MonitorFromWindow(new WindowInteropHelper(window).Handle, MONITOR_DEFAULTTONEAREST);
		}

		public static IntPtr GetMainMonitor() {
			return MonitorFromWindow(IntPtr.Zero, MONITOR_DEFAULTTOPRIMARY);
		}
	}
}
