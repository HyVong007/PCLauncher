using Gma.System.MouseKeyHook;
using Microsoft.Win32;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;


namespace PCLauncher
{
	public partial class Alarm : Window
	{
		private readonly CancellationTokenSource cancelClosing = new();
		private const int DURATION_SECONDS = 60;
		public Alarm()
		{
			InitializeComponent();
			mouseKeyHook.KeyDown += MouseKeyEvent;
			mouseKeyHook.MouseDown += MouseKeyEvent;
			mouseKeyHook.MouseMove += MouseKeyEvent;
			DelayClosing();

			#region Phát video
			for (int i = 0; i < 100; ++i) Keyboard.Press(Key.VolumeDown);
			player.Source = new(App.AlarmPath, UriKind.Absolute);
			player.MediaEnded += (_, __) => player.Position = new TimeSpan(0, 0, 0);
			#endregion

			#region Đếm ngược
			int seconds = DURATION_SECONDS;
			var timer = new DispatcherTimer(new TimeSpan(0, 0, 1), DispatcherPriority.Normal, (_, __) =>
			{
				time.Text = $"{seconds--}";
			}, App.Current.Dispatcher)
			{ IsEnabled = false };
			timer.Start();
			#endregion

			IncreaseVolume();
			async void IncreaseVolume()
			{
				var token = cancelClosing.Token;
				for (int i = 0; i < App.AlarmVolume; i += 2)
				{
					Keyboard.Press(Key.VolumeUp);
					await Task.Delay(500);
					if (token.IsCancellationRequested) return;
				}
			}
		}


		private void Window_Closed(object sender, EventArgs e)
		{
			mouseKeyHook.KeyDown -= MouseKeyEvent;
			mouseKeyHook.MouseDown -= MouseKeyEvent;
			mouseKeyHook.MouseMove -= MouseKeyEvent;
			cancelClosing.Dispose();
		}


		private void MouseKeyEvent(object? sender, EventArgs e)
		{
			if (e is not KeyEventArgs k ||
				((k.KeyCode & Keys.VolumeUp) != Keys.VolumeUp && (k.KeyCode & Keys.VolumeDown) != Keys.VolumeDown)) cancelClosing.Cancel();
		}


		private async void DelayClosing()
		{
			try { await Task.Delay(DURATION_SECONDS * 1000, cancelClosing.Token); } catch (OperationCanceledException) { Close(); return; }
			Close();
			Util.SleepPC();
		}


		#region Static
		private static IKeyboardMouseEvents mouseKeyHook;
		public static void Init(IKeyboardMouseEvents mouseKeyHook)
		{
			if (string.IsNullOrEmpty(App.AlarmTime)) return;
			var s = App.AlarmTime.Split(':');
			if (s.Length != 2 || s[0].Length == 0 || s[1].Length == 0) return;
			var now = DateTime.Now;
			try { alarmTime = new DateTime(now.Year, now.Month, now.Day, Convert.ToByte(s[0]), Convert.ToByte(s[1]), 0); }
			catch { return; }

			if (alarmTime <= now) alarmTime = alarmTime.AddDays(1);
			Util.WakePC(alarmTime);
			SetAlarm();
			Alarm.mouseKeyHook = mouseKeyHook;
			SystemEvents.PowerModeChanged += PowerModeChanged;
			App.Current.Exit += (_, __) => SystemEvents.PowerModeChanged -= PowerModeChanged;
		}


		private static DateTime alarmTime;
		private static CancellationTokenSource cancelAlarm = new();
		private static async void SetAlarm()
		{
			cancelAlarm.Cancel();
			cancelAlarm.Dispose();
			cancelAlarm = new();
			try { await Task.Delay(alarmTime - DateTime.Now, cancelAlarm.Token); } catch (OperationCanceledException) { return; }

			alarmTime = alarmTime.AddDays(1);
			Util.WakePC(alarmTime);
			Task.Delay(1).ContinueWith(_ => SetAlarm());
			new Alarm().Show();
		}


		private static void PowerModeChanged(object sender, PowerModeChangedEventArgs e)
		{
			double m = (DateTime.Now - alarmTime).TotalMinutes;
			if (m >= 0) alarmTime = alarmTime.AddDays(1);
			Util.WakePC(alarmTime);
			SetAlarm();
			if (0 <= m && m < 3) new Alarm().Show();
		}
		#endregion
	}
}
