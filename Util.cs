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
using System.Windows.Interop;
using System.Windows.Media;
using Windows.Foundation;


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
	}



	public static class Keyboard
	{
		public static void StartListening() =>
			ptrHook = SetWindowsHookEx(13, keyboardProcess = (nCode, wp, lp) =>
			{
				if (nCode < 0) return CallNextHookEx(ptrHook, nCode, wp, lp);

				var keyInfo = (KbDllHook)Marshal.PtrToStructure(lp, typeof(KbDllHook));
				return allowKey(keyInfo.key) ? IntPtr.Zero : (IntPtr)1;
			}, GetModuleHandle(Process.GetCurrentProcess().MainModule.ModuleName), 0);


		public static void StopListening()
		{
			if (ptrHook == IntPtr.Zero) return;
			UnhookWindowsHookEx(ptrHook);
			ptrHook = IntPtr.Zero;
		}


		public static void Press(params Key[] keys)
		{
			foreach (var key in keys) keybd_event((byte)key, 0, 0, 0);
			foreach (var key in keys) keybd_event((byte)key, 0, 2, 0);
		}


		/// <summary>
		/// Nhận vào 1 key và trả về kết quả cho phép hay chặn key đó ?<para/>
		/// Return: <see langword="true"/> là cho phép, <see langword="false"/> là chặn
		/// </summary>
		public static Func<Key, bool> allowKey = key => true;


		#region user32.dll
		/// <summary>
		/// Structure contain information about low-level keyboard input event
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		private struct KbDllHook
		{
			public Key key;
			public int scanCode;
			public int flags;
			public int time;
			public IntPtr extra;
		}

		[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern IntPtr SetWindowsHookEx(int id, LowLevelKeyboardProc callback, IntPtr hMod, uint dwThreadId);

		[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern bool UnhookWindowsHookEx(IntPtr hook);

		[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern IntPtr CallNextHookEx(IntPtr hook, int nCode, IntPtr wp, IntPtr lp);

		[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern IntPtr GetModuleHandle(string name);

		[DllImport("user32.dll", CharSet = CharSet.Auto)]
		private static extern short GetAsyncKeyState(Key key);

		[DllImport("user32.dll")]
		private static extern int keybd_event(byte bVk, byte bScan, long dwFlags, long dwExtraInfo);
		#endregion


		private static IntPtr ptrHook;
		private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

		/// <summary>
		/// Phải giữ trong field vì được sử dụng cho Unmannaged code
		/// </summary>
		[System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0052:Remove unread private members", Justification = "<Pending>")]
		private static LowLevelKeyboardProc keyboardProcess;
	}



	[Flags]
	public enum Key
	{
		//
		// Summary:
		//     The bitmask to extract a key code from a key value.
		KeyCode = 0xFFFF,
		//
		// Summary:
		//     The bitmask to extract modifiers from a key value.
		Modifiers = -65536,
		//
		// Summary:
		//     No key pressed.
		None = 0x0,
		//
		// Summary:
		//     The left mouse button.
		LButton = 0x1,
		//
		// Summary:
		//     The right mouse button.
		RButton = 0x2,
		//
		// Summary:
		//     The CANCEL key.
		Cancel = 0x3,
		//
		// Summary:
		//     The middle mouse button (three-button mouse).
		MButton = 0x4,
		//
		// Summary:
		//     The first x mouse button (five-button mouse).
		XButton1 = 0x5,
		//
		// Summary:
		//     The second x mouse button (five-button mouse).
		XButton2 = 0x6,
		//
		// Summary:
		//     The BACKSPACE key.
		Back = 0x8,
		//
		// Summary:
		//     The TAB key.
		Tab = 0x9,
		//
		// Summary:
		//     The LINEFEED key.
		LineFeed = 0xA,
		//
		// Summary:
		//     The CLEAR key.
		Clear = 0xC,
		//
		// Summary:
		//     The RETURN key.
		Return = 0xD,
		//
		// Summary:
		//     The ENTER key.
		Enter = 0xD,
		//
		// Summary:
		//     The SHIFT key.
		ShiftKey = 0x10,
		//
		// Summary:
		//     The CTRL key.
		ControlKey = 0x11,
		//
		// Summary:
		//     The ALT key.
		Menu = 0x12,
		//
		// Summary:
		//     The PAUSE key.
		Pause = 0x13,
		//
		// Summary:
		//     The CAPS LOCK key.
		Capital = 0x14,
		//
		// Summary:
		//     The CAPS LOCK key.
		CapsLock = 0x14,
		//
		// Summary:
		//     The IME Kana mode key.
		KanaMode = 0x15,
		//
		// Summary:
		//     The IME Hanguel mode key. (maintained for compatibility; use HangulMode)
		HanguelMode = 0x15,
		//
		// Summary:
		//     The IME Hangul mode key.
		HangulMode = 0x15,
		//
		// Summary:
		//     The IME Junja mode key.
		JunjaMode = 0x17,
		//
		// Summary:
		//     The IME final mode key.
		FinalMode = 0x18,
		//
		// Summary:
		//     The IME Hanja mode key.
		HanjaMode = 0x19,
		//
		// Summary:
		//     The IME Kanji mode key.
		KanjiMode = 0x19,
		//
		// Summary:
		//     The ESC key.
		Escape = 0x1B,
		//
		// Summary:
		//     The IME convert key.
		IMEConvert = 0x1C,
		//
		// Summary:
		//     The IME nonconvert key.
		IMENonconvert = 0x1D,
		//
		// Summary:
		//     The IME accept key, replaces System.Windows.Forms.Keys.IMEAceept.
		IMEAccept = 0x1E,
		//
		// Summary:
		//     The IME accept key. Obsolete, use System.Windows.Forms.Keys.IMEAccept instead.
		IMEAceept = 0x1E,
		//
		// Summary:
		//     The IME mode change key.
		IMEModeChange = 0x1F,
		//
		// Summary:
		//     The SPACEBAR key.
		Space = 0x20,
		//
		// Summary:
		//     The PAGE UP key.
		Prior = 0x21,
		//
		// Summary:
		//     The PAGE UP key.
		PageUp = 0x21,
		//
		// Summary:
		//     The PAGE DOWN key.
		Next = 0x22,
		//
		// Summary:
		//     The PAGE DOWN key.
		PageDown = 0x22,
		//
		// Summary:
		//     The END key.
		End = 0x23,
		//
		// Summary:
		//     The HOME key.
		Home = 0x24,
		//
		// Summary:
		//     The LEFT ARROW key.
		Left = 0x25,
		//
		// Summary:
		//     The UP ARROW key.
		Up = 0x26,
		//
		// Summary:
		//     The RIGHT ARROW key.
		Right = 0x27,
		//
		// Summary:
		//     The DOWN ARROW key.
		Down = 0x28,
		//
		// Summary:
		//     The SELECT key.
		Select = 0x29,
		//
		// Summary:
		//     The PRINT key.
		Print = 0x2A,
		//
		// Summary:
		//     The EXECUTE key.
		Execute = 0x2B,
		//
		// Summary:
		//     The PRINT SCREEN key.
		Snapshot = 0x2C,
		//
		// Summary:
		//     The PRINT SCREEN key.
		PrintScreen = 0x2C,
		//
		// Summary:
		//     The INS key.
		Insert = 0x2D,
		//
		// Summary:
		//     The DEL key.
		Delete = 0x2E,
		//
		// Summary:
		//     The HELP key.
		Help = 0x2F,
		//
		// Summary:
		//     The 0 key.
		D0 = 0x30,
		//
		// Summary:
		//     The 1 key.
		D1 = 0x31,
		//
		// Summary:
		//     The 2 key.
		D2 = 0x32,
		//
		// Summary:
		//     The 3 key.
		D3 = 0x33,
		//
		// Summary:
		//     The 4 key.
		D4 = 0x34,
		//
		// Summary:
		//     The 5 key.
		D5 = 0x35,
		//
		// Summary:
		//     The 6 key.
		D6 = 0x36,
		//
		// Summary:
		//     The 7 key.
		D7 = 0x37,
		//
		// Summary:
		//     The 8 key.
		D8 = 0x38,
		//
		// Summary:
		//     The 9 key.
		D9 = 0x39,
		//
		// Summary:
		//     The A key.
		A = 0x41,
		//
		// Summary:
		//     The B key.
		B = 0x42,
		//
		// Summary:
		//     The C key.
		C = 0x43,
		//
		// Summary:
		//     The D key.
		D = 0x44,
		//
		// Summary:
		//     The E key.
		E = 0x45,
		//
		// Summary:
		//     The F key.
		F = 0x46,
		//
		// Summary:
		//     The G key.
		G = 0x47,
		//
		// Summary:
		//     The H key.
		H = 0x48,
		//
		// Summary:
		//     The I key.
		I = 0x49,
		//
		// Summary:
		//     The J key.
		J = 0x4A,
		//
		// Summary:
		//     The K key.
		K = 0x4B,
		//
		// Summary:
		//     The L key.
		L = 0x4C,
		//
		// Summary:
		//     The M key.
		M = 0x4D,
		//
		// Summary:
		//     The N key.
		N = 0x4E,
		//
		// Summary:
		//     The O key.
		O = 0x4F,
		//
		// Summary:
		//     The P key.
		P = 0x50,
		//
		// Summary:
		//     The Q key.
		Q = 0x51,
		//
		// Summary:
		//     The R key.
		R = 0x52,
		//
		// Summary:
		//     The S key.
		S = 0x53,
		//
		// Summary:
		//     The T key.
		T = 0x54,
		//
		// Summary:
		//     The U key.
		U = 0x55,
		//
		// Summary:
		//     The V key.
		V = 0x56,
		//
		// Summary:
		//     The W key.
		W = 0x57,
		//
		// Summary:
		//     The X key.
		X = 0x58,
		//
		// Summary:
		//     The Y key.
		Y = 0x59,
		//
		// Summary:
		//     The Z key.
		Z = 0x5A,
		//
		// Summary:
		//     The left Windows logo key (Microsoft Natural Keyboard).
		LWin = 0x5B,
		//
		// Summary:
		//     The right Windows logo key (Microsoft Natural Keyboard).
		RWin = 0x5C,
		//
		// Summary:
		//     The application key (Microsoft Natural Keyboard).
		Apps = 0x5D,
		//
		// Summary:
		//     The computer sleep key.
		Sleep = 0x5F,
		//
		// Summary:
		//     The 0 key on the numeric keypad.
		NumPad0 = 0x60,
		//
		// Summary:
		//     The 1 key on the numeric keypad.
		NumPad1 = 0x61,
		//
		// Summary:
		//     The 2 key on the numeric keypad.
		NumPad2 = 0x62,
		//
		// Summary:
		//     The 3 key on the numeric keypad.
		NumPad3 = 0x63,
		//
		// Summary:
		//     The 4 key on the numeric keypad.
		NumPad4 = 0x64,
		//
		// Summary:
		//     The 5 key on the numeric keypad.
		NumPad5 = 0x65,
		//
		// Summary:
		//     The 6 key on the numeric keypad.
		NumPad6 = 0x66,
		//
		// Summary:
		//     The 7 key on the numeric keypad.
		NumPad7 = 0x67,
		//
		// Summary:
		//     The 8 key on the numeric keypad.
		NumPad8 = 0x68,
		//
		// Summary:
		//     The 9 key on the numeric keypad.
		NumPad9 = 0x69,
		//
		// Summary:
		//     The multiply key.
		Multiply = 0x6A,
		//
		// Summary:
		//     The add key.
		Add = 0x6B,
		//
		// Summary:
		//     The separator key.
		Separator = 0x6C,
		//
		// Summary:
		//     The subtract key.
		Subtract = 0x6D,
		//
		// Summary:
		//     The decimal key.
		Decimal = 0x6E,
		//
		// Summary:
		//     The divide key.
		Divide = 0x6F,
		//
		// Summary:
		//     The F1 key.
		F1 = 0x70,
		//
		// Summary:
		//     The F2 key.
		F2 = 0x71,
		//
		// Summary:
		//     The F3 key.
		F3 = 0x72,
		//
		// Summary:
		//     The F4 key.
		F4 = 0x73,
		//
		// Summary:
		//     The F5 key.
		F5 = 0x74,
		//
		// Summary:
		//     The F6 key.
		F6 = 0x75,
		//
		// Summary:
		//     The F7 key.
		F7 = 0x76,
		//
		// Summary:
		//     The F8 key.
		F8 = 0x77,
		//
		// Summary:
		//     The F9 key.
		F9 = 0x78,
		//
		// Summary:
		//     The F10 key.
		F10 = 0x79,
		//
		// Summary:
		//     The F11 key.
		F11 = 0x7A,
		//
		// Summary:
		//     The F12 key.
		F12 = 0x7B,
		//
		// Summary:
		//     The F13 key.
		F13 = 0x7C,
		//
		// Summary:
		//     The F14 key.
		F14 = 0x7D,
		//
		// Summary:
		//     The F15 key.
		F15 = 0x7E,
		//
		// Summary:
		//     The F16 key.
		F16 = 0x7F,
		//
		// Summary:
		//     The F17 key.
		F17 = 0x80,
		//
		// Summary:
		//     The F18 key.
		F18 = 0x81,
		//
		// Summary:
		//     The F19 key.
		F19 = 0x82,
		//
		// Summary:
		//     The F20 key.
		F20 = 0x83,
		//
		// Summary:
		//     The F21 key.
		F21 = 0x84,
		//
		// Summary:
		//     The F22 key.
		F22 = 0x85,
		//
		// Summary:
		//     The F23 key.
		F23 = 0x86,
		//
		// Summary:
		//     The F24 key.
		F24 = 0x87,
		//
		// Summary:
		//     The NUM LOCK key.
		NumLock = 0x90,
		//
		// Summary:
		//     The SCROLL LOCK key.
		Scroll = 0x91,
		//
		// Summary:
		//     The left SHIFT key.
		LShiftKey = 0xA0,
		//
		// Summary:
		//     The right SHIFT key.
		RShiftKey = 0xA1,
		//
		// Summary:
		//     The left CTRL key.
		LControlKey = 0xA2,
		//
		// Summary:
		//     The right CTRL key.
		RControlKey = 0xA3,
		//
		// Summary:
		//     The left ALT key.
		LMenu = 0xA4,
		//
		// Summary:
		//     The right ALT key.
		RMenu = 0xA5,
		//
		// Summary:
		//     The browser back key (Windows 2000 or later).
		BrowserBack = 0xA6,
		//
		// Summary:
		//     The browser forward key (Windows 2000 or later).
		BrowserForward = 0xA7,
		//
		// Summary:
		//     The browser refresh key (Windows 2000 or later).
		BrowserRefresh = 0xA8,
		//
		// Summary:
		//     The browser stop key (Windows 2000 or later).
		BrowserStop = 0xA9,
		//
		// Summary:
		//     The browser search key (Windows 2000 or later).
		BrowserSearch = 0xAA,
		//
		// Summary:
		//     The browser favorites key (Windows 2000 or later).
		BrowserFavorites = 0xAB,
		//
		// Summary:
		//     The browser home key (Windows 2000 or later).
		BrowserHome = 0xAC,
		//
		// Summary:
		//     The volume mute key (Windows 2000 or later).
		VolumeMute = 0xAD,
		//
		// Summary:
		//     The volume down key (Windows 2000 or later).
		VolumeDown = 0xAE,
		//
		// Summary:
		//     The volume up key (Windows 2000 or later).
		VolumeUp = 0xAF,
		//
		// Summary:
		//     The media next track key (Windows 2000 or later).
		MediaNextTrack = 0xB0,
		//
		// Summary:
		//     The media previous track key (Windows 2000 or later).
		MediaPreviousTrack = 0xB1,
		//
		// Summary:
		//     The media Stop key (Windows 2000 or later).
		MediaStop = 0xB2,
		//
		// Summary:
		//     The media play pause key (Windows 2000 or later).
		MediaPlayPause = 0xB3,
		//
		// Summary:
		//     The launch mail key (Windows 2000 or later).
		LaunchMail = 0xB4,
		//
		// Summary:
		//     The select media key (Windows 2000 or later).
		SelectMedia = 0xB5,
		//
		// Summary:
		//     The start application one key (Windows 2000 or later).
		LaunchApplication1 = 0xB6,
		//
		// Summary:
		//     The start application two key (Windows 2000 or later).
		LaunchApplication2 = 0xB7,
		//
		// Summary:
		//     The OEM Semicolon key on a US standard keyboard (Windows 2000 or later).
		OemSemicolon = 0xBA,
		//
		// Summary:
		//     The OEM 1 key.
		Oem1 = 0xBA,
		//
		// Summary:
		//     The OEM plus key on any country/region keyboard (Windows 2000 or later).
		Oemplus = 0xBB,
		//
		// Summary:
		//     The OEM comma key on any country/region keyboard (Windows 2000 or later).
		Oemcomma = 0xBC,
		//
		// Summary:
		//     The OEM minus key on any country/region keyboard (Windows 2000 or later).
		OemMinus = 0xBD,
		//
		// Summary:
		//     The OEM period key on any country/region keyboard (Windows 2000 or later).
		OemPeriod = 0xBE,
		//
		// Summary:
		//     The OEM question mark key on a US standard keyboard (Windows 2000 or later).
		OemQuestion = 0xBF,
		//
		// Summary:
		//     The OEM 2 key.
		Oem2 = 0xBF,
		//
		// Summary:
		//     The OEM tilde key on a US standard keyboard (Windows 2000 or later).
		Oemtilde = 0xC0,
		//
		// Summary:
		//     The OEM 3 key.
		Oem3 = 0xC0,
		//
		// Summary:
		//     The OEM open bracket key on a US standard keyboard (Windows 2000 or later).
		OemOpenBrackets = 0xDB,
		//
		// Summary:
		//     The OEM 4 key.
		Oem4 = 0xDB,
		//
		// Summary:
		//     The OEM pipe key on a US standard keyboard (Windows 2000 or later).
		OemPipe = 0xDC,
		//
		// Summary:
		//     The OEM 5 key.
		Oem5 = 0xDC,
		//
		// Summary:
		//     The OEM close bracket key on a US standard keyboard (Windows 2000 or later).
		OemCloseBrackets = 0xDD,
		//
		// Summary:
		//     The OEM 6 key.
		Oem6 = 0xDD,
		//
		// Summary:
		//     The OEM singled/double quote key on a US standard keyboard (Windows 2000 or later).
		OemQuotes = 0xDE,
		//
		// Summary:
		//     The OEM 7 key.
		Oem7 = 0xDE,
		//
		// Summary:
		//     The OEM 8 key.
		Oem8 = 0xDF,
		//
		// Summary:
		//     The OEM angle bracket or backslash key on the RT 102 key keyboard (Windows 2000
		//     or later).
		OemBackslash = 0xE2,
		//
		// Summary:
		//     The OEM 102 key.
		Oem102 = 0xE2,
		//
		// Summary:
		//     The PROCESS KEY key.
		ProcessKey = 0xE5,
		//
		// Summary:
		//     Used to pass Unicode characters as if they were keystrokes. The Packet key value
		//     is the low word of a 32-bit virtual-key value used for non-keyboard input methods.
		Packet = 0xE7,
		//
		// Summary:
		//     The ATTN key.
		Attn = 0xF6,
		//
		// Summary:
		//     The CRSEL key.
		Crsel = 0xF7,
		//
		// Summary:
		//     The EXSEL key.
		Exsel = 0xF8,
		//
		// Summary:
		//     The ERASE EOF key.
		EraseEof = 0xF9,
		//
		// Summary:
		//     The PLAY key.
		Play = 0xFA,
		//
		// Summary:
		//     The ZOOM key.
		Zoom = 0xFB,
		//
		// Summary:
		//     A constant reserved for future use.
		NoName = 0xFC,
		//
		// Summary:
		//     The PA1 key.
		Pa1 = 0xFD,
		//
		// Summary:
		//     The CLEAR key.
		OemClear = 0xFE,
		//
		// Summary:
		//     The SHIFT modifier key.
		Shift = 0x10000,
		//
		// Summary:
		//     The CTRL modifier key.
		Control = 0x20000,
		//
		// Summary:
		//     The ALT modifier key.
		Alt = 0x40000
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