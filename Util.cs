using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Windows.Foundation;
using form = System.Windows.Forms;


namespace PCLauncher
{
	public static class Util
	{
		static Util() => appBarData.cbSize = (uint)Marshal.SizeOf(appBarData);


		#region Win32
		[DllImport("user32.dll")]
		private static extern bool ShowWindowAsync(int hWnd, int nCmdShow);

		[DllImport("user32.dll", SetLastError = true)]
		private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool SetForegroundWindow(IntPtr hWnd);

		[DllImport("user32.dll")]
		private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

		[DllImport("user32.dll")]
		private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

		[DllImport("user32.dll")]
		private static extern int ShowWindow(int hwnd, int command);

		[DllImport("user32.dll")]
		private static extern int FindWindowEx(int parentHandle, int childAfter, string className, int windowTitle);

		[DllImport("user32.dll")]
		private static extern int GetDesktopWindow();

		[DllImport("shell32.dll", SetLastError = true)]
		private static extern IntPtr SHAppBarMessage(AppBarMessage dwMessage, [In] ref AppBarData pData);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool IsWindowVisible(IntPtr hWnd);

		[DllImport("user32.dll")]
		private static extern int keybd_event(byte bVk, byte bScan, long dwFlags, long dwExtraInfo);
		#endregion


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Contains<T>(this IReadOnlyList<T> list, T item) => (list as List<T>).Contains(item);


		public enum SW : int
		{
			HIDE = 0,
			SHOWNORMAL = 1,
			SHOWMINIMIZED = 2,
			SHOWMAXIMIZED = 3,
			SHOWNOACTIVATE = 4,
			SHOW = 5,
			MINIMIZE = 6,
			SHOWMINNOACTIVE = 7,
			SHOWNA = 8,
			RESTORE = 9,
			SHOWDEFAULT = 10
		}
		public static void MaximizeMainWindow(this Process process)
		{
			var hWnd = process.MainWindowHandle;
			if (hWnd == IntPtr.Zero) return;
			ShowWindowAsync((int)hWnd, (int)(SW.SHOWMAXIMIZED));
			SetForegroundWindow(hWnd);
		}


		#region DisableMinimizeButton
		private const int GWL_STYLE = -16;
		private const int WS_MAXIMIZEBOX = 0x10000;
		private const int WS_MINIMIZEBOX = 0x20000;


		public static void DisableMinimizeButton(this Process process)
		{
			var hWnd = process.MainWindowHandle;
			if (hWnd == IntPtr.Zero) return;
			SetWindowLong(hWnd, GWL_STYLE, GetWindowLong(hWnd, GWL_STYLE) & ~WS_MINIMIZEBOX);
		}
		#endregion


		private const int SW_HIDE = 0, SW_SHOW = 1;
		private static readonly IntPtr taskBarHandle = FindWindow("Shell_TrayWnd", "");
		private static readonly int startButtonHandle = FindWindowEx(GetDesktopWindow(), 0, "button", 0);
		public static bool isTaskBarVisible
		{
			get => IsWindowVisible(taskBarHandle);

			set
			{
				ShowWindow((int)taskBarHandle, value ? SW_SHOW : SW_HIDE);
				ShowWindow(startButtonHandle, value ? SW_SHOW : SW_HIDE);
			}
		}


		#region taskBarState
		[Flags]
		public enum AppBarMessage
		{
			New = 0x00,
			Remove = 0x01,
			QueryPos = 0x02,
			SetPos = 0x03,
			GetState = 0x04,
			GetTaskBarPos = 0x05,
			Activate = 0x06,
			GetAutoHideBar = 0x07,
			SetAutoHideBar = 0x08,
			WindowPosChanged = 0x09,
			SetState = 0x0a
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct AppBarData
		{
			public uint cbSize;
			public IntPtr hWnd;
			public uint uCallbackMessage;
			public uint uEdge;
			public Rect rc;
			public int lParam;
		}

		[Flags]
		public enum AppBarState
		{
			AutoHide = 0x01,
			AlwaysOnTop = 0x02
		}


		private static AppBarData appBarData = new AppBarData()
		{
			hWnd = taskBarHandle
		};
		/// <summary>
		/// Set value thì sau ít nhất 300ms mới có tác dụng<para/>
		/// Muốn đợi có thể dùng <see cref="Thread.Sleep(int)"/> hoặc <see langword="await"/>
		/// </summary>
		public static AppBarState taskBarState
		{
			get => ((AppBarState)SHAppBarMessage(AppBarMessage.GetState, ref appBarData).ToInt32())
					.HasFlag(AppBarState.AutoHide) ? AppBarState.AutoHide : AppBarState.AlwaysOnTop;

			set
			{
				appBarData.lParam = (int)value;
				SHAppBarMessage(AppBarMessage.SetState, ref appBarData);
			}
		}
		#endregion


		/// <summary>
		/// Đọc file config có dạng:<para/>
		/// fieldName1=value1<br/>
		/// fieldName2 = value2<br/>
		/// ...<para/>
		/// <paramref name="configType"/> có chứa:<br/>
		/// public static PrimitiveType fieldName;<br/>
		/// ...
		/// </summary>
		public static void ReadConfig(this string filePath, Type configType)
		{
			var name_info = new Dictionary<string, FieldInfo>();
			foreach (FieldInfo info in configType.FindMembers(MemberTypes.Field, BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly, null, null))
				name_info[info.Name] = info;

			foreach (string line in File.ReadLines(filePath))
			{
				if (line.Length == 0 || line.Trim().Length == 0) continue; // line trống hoặc trắng

				var parts = line.Split('=');
				if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
					throw new Exception($"Phải có duy nhất 1 ký tự '='. FieldName và value không trống. Line: {line}");

				if (!name_info.TryGetValue(parts[0].Trim(), out FieldInfo info))
					throw new Exception($"Không tìm thấy FieldName trong {configType}. Line: {line}");

				try { info.SetValue(null, Convert.ChangeType(parts[1], info.FieldType)); }
				catch { throw new Exception($"Kiểu dữ liệu không hợp lệ. Line: {line}"); }
			}
		}


		/// <summary>
		/// Đọc file config có dạng:<para/>
		/// fieldName1=value1<br/>
		/// fieldName2 = value2<br/>
		/// ...<para/>
		/// <typeparamref name="T"/> có chứa:<br/>
		/// public static PrimitiveType fieldName;<br/>
		/// ...
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void ReadConfig<T>(this string filePath) => ReadConfig(filePath, typeof(T));


		public static T GetChild<T>(this Visual element) where T : Visual
		{
			var type = typeof(T);
			if (element.GetType() == type) return element as T;
			Visual foundElement = null;
			if (element is FrameworkElement) (element as FrameworkElement).ApplyTemplate();
			for (int i = VisualTreeHelper.GetChildrenCount(element) - 1; i >= 0; --i)
			{
				var visual = VisualTreeHelper.GetChild(element, i) as Visual;
				foundElement = GetChild<T>(visual);
				if (foundElement != null) break;
			}
			return foundElement as T;
		}


		#region Sleep and Awake
		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern IntPtr CreateWaitableTimer(IntPtr lpTimerAttributes, bool bManualReset, string lpTimerName);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern bool SetWaitableTimer(IntPtr hTimer, [In] ref long pDueTime, int lPeriod, TimerCompleteDelegate pfnCompletionRoutine, IntPtr pArgToCompletionRoutine, bool fResume);

		[DllImport("powrprof.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

		private delegate void TimerCompleteDelegate();

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern bool CancelWaitableTimer(IntPtr hTimer);


		public static IntPtr WakePC(in DateTime dt)
		{
			TimerCompleteDelegate timerComplete = null;
			long interval = dt.ToFileTime();
			IntPtr handle = CreateWaitableTimer(IntPtr.Zero, true, "WaitableTimer");
			SetWaitableTimer(handle, ref interval, 0, timerComplete, IntPtr.Zero, true);
			return handle;
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool SleepPC() => SetSuspendState(false, false, false);
		#endregion


		public static TaskAwaiter GetAwaiter(this IAsyncAction op)
		{
			var source = new TaskCompletionSource();
			if (op.Status == AsyncStatus.Completed) source.SetResult();
			else op.Completed += (info, status) => source.SetResult();
			return source.Task.GetAwaiter();
		}


		public static TaskAwaiter<T> GetAwaiter<T>(this IAsyncOperation<T> op)
		{
			var source = new TaskCompletionSource<T>();
			if (op.Status == AsyncStatus.Completed) source.TrySetResult(op.GetResults());
			else op.Completed += (info, status) => source.TrySetResult(op.GetResults());
			return source.Task.GetAwaiter();
		}


		#region Sound Volume
		private const int APPCOMMAND_VOLUME_MUTE = 0x80000;
		private const int APPCOMMAND_VOLUME_UP = 0xA0000;
		private const int APPCOMMAND_VOLUME_DOWN = 0x90000;
		private const int WM_APPCOMMAND = 0x319;


		[DllImport("user32.dll")]
		private static extern IntPtr SendMessageW(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);


		public static void VolumeMute(IntPtr handle) => SendMessageW(handle, WM_APPCOMMAND, handle, (IntPtr)APPCOMMAND_VOLUME_MUTE);
		public static void VolumeDown(IntPtr handle) => SendMessageW(handle, WM_APPCOMMAND, handle, (IntPtr)APPCOMMAND_VOLUME_DOWN);
		public static void VolumeUp(IntPtr handle) => SendMessageW(handle, WM_APPCOMMAND, handle, (IntPtr)APPCOMMAND_VOLUME_UP);
		#endregion


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong ConvertMACAddress(string mac) => Convert.ToUInt64(mac.Replace(":", ""), 16);


		#region Bật/Tắt màn hình
		private enum MonitorState
		{
			MonitorStateOn = -1,
			MonitorStateOff = 2,
			MonitorStateStandBy = 1
		}


		[DllImport("user32.dll")]
		private static extern int SendMessage(int hWnd, int hMsg, int wParam, int lParam);


		public static void TurnOffMonitor() => SendMessage(0xFFFF, 0x112, 0xF170, 2);
		#endregion


		public static void Press(params form.Keys[] keys)
		{
			foreach (var key in keys) keybd_event((byte)key, 0, 0, 0);
			foreach (var key in keys) keybd_event((byte)key, 0, 2, 0);
		}

	}


	[Serializable]
	[StructLayout(LayoutKind.Sequential)]
	public struct Rect
	{
		public int left, top, right, bottom;

		public Rect(int left, int top, int right, int bottom)
		{
			this.left = left; this.top = top; this.right = right; this.bottom = bottom;
		}


		public int x
		{
			get => left;
			set
			{
				right -= (left - value); left = value;
			}
		}


		public int y
		{
			get => top;
			set
			{
				bottom -= (top - value); top = value;
			}
		}


		public int width
		{
			get => right - left;
			set => right = value + left;
		}


		public int height
		{
			get => bottom - top;
			set => bottom = value + top;
		}
	}
}