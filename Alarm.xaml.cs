using Gma.System.MouseKeyHook;
using Microsoft.Win32;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using form = System.Windows.Forms;


namespace PCLauncher
{
	public partial class Alarm : Window
	{
		private const int DURATION_SECONDS = 60;
		private readonly CancellationTokenSource cancelClosing = new();
		private static Alarm instance;
		public Alarm(bool sleepWhenTimeout)
		{
			instance = this;
			InitializeComponent();
			mouseKeyHook.KeyDown += MouseKeyEvent;
			mouseKeyHook.MouseDown += MouseKeyEvent;
			mouseKeyHook.MouseMove += MouseKeyEvent;
			DelayClosing();

			#region Phát video
			for (int i = 0; i < 100; ++i) Util.Press(form.Keys.VolumeDown);
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
			Util.MuteApp(true, "msedge", "chrome", "mpc-be64");
			async void IncreaseVolume()
			{
				var token = cancelClosing.Token;
				for (int i = 0; i < App.AlarmVolume; i += 2)
				{
					Util.Press(form.Keys.VolumeUp);
					await Task.Delay(500);
					if (token.IsCancellationRequested) return;
				}
			}

			async void DelayClosing()
			{
				try { await Task.Delay(DURATION_SECONDS * 1000, cancelClosing.Token); }
				catch (OperationCanceledException) { Close(); return; }
				Close();
				if (sleepWhenTimeout) Util.SleepPC();
			}
		}


		private void Window_Closed(object sender, EventArgs e)
		{
			instance = null;
			mouseKeyHook.KeyDown -= MouseKeyEvent;
			mouseKeyHook.MouseDown -= MouseKeyEvent;
			mouseKeyHook.MouseMove -= MouseKeyEvent;
			cancelClosing.Dispose();
			Util.MuteApp(false, "msedge", "chrome", "mpc-be64");
		}


		private void MouseKeyEvent(object? sender, EventArgs e)
		{
			if (e is not form.KeyEventArgs k ||
			(
				(k.KeyCode & form.Keys.VolumeUp) != form.Keys.VolumeUp && (k.KeyCode & form.Keys.VolumeDown) != form.Keys.VolumeDown
			)) cancelClosing.Cancel();
		}


		#region Static
		private static IKeyboardMouseEvents mouseKeyHook;
		public static async void Init(IKeyboardMouseEvents mouseKeyHook)
		{
			var now = DateTime.Now;
			if (string.IsNullOrEmpty(App.AlarmTime)) goto AUTO_SLEEP;
			var s = App.AlarmTime.Split(':');
			if (s.Length != 2 || s[0].Length == 0 || s[1].Length == 0) goto AUTO_SLEEP;
			try { alarmTime = new DateTime(now.Year, now.Month, now.Day, Convert.ToByte(s[0]), Convert.ToByte(s[1]), 0); }
			catch { goto AUTO_SLEEP; }

			if (alarmTime <= now) UpdateAlarmTime();
			Util.WakePC(alarmTime);
			SetAlarm();
			Alarm.mouseKeyHook = mouseKeyHook;
			SystemEvents.PowerModeChanged += PowerModeChanged;
			App.Current.Exit += (_, __) => SystemEvents.PowerModeChanged -= PowerModeChanged;

		AUTO_SLEEP:
			if (alarmTime != default && (alarmTime - now).TotalMinutes < 6) return;

			using var cts = new CancellationTokenSource();
			SystemEvents.PowerModeChanged += e;
			mouseKeyHook.KeyDown += e;
			mouseKeyHook.MouseDown += e;
			mouseKeyHook.MouseMove += e;
			try { await Task.Delay(3 * 60_000, cts.Token); }
			catch (OperationCanceledException) { return; }
			finally
			{
				SystemEvents.PowerModeChanged -= e;
				mouseKeyHook.KeyDown -= e;
				mouseKeyHook.MouseDown -= e;
				mouseKeyHook.MouseMove -= e;
			}


			Util.SleepPC();
			void e(object? _, EventArgs __) => cts.Cancel();
		}


		private static DateTime alarmTime;
		private static CancellationTokenSource cancelAlarm = new();
		private static async void SetAlarm()
		{
			cancelAlarm.Cancel();
			cancelAlarm.Dispose();
			cancelAlarm = new();
			try { await Task.Delay(alarmTime - DateTime.Now, cancelAlarm.Token); } catch (OperationCanceledException) { return; }

			UpdateAlarmTime();
			Util.WakePC(alarmTime);
			Task.Delay(1).ContinueWith(_ => SetAlarm());
			new Alarm(false).Show();
		}


		private static void PowerModeChanged(object sender, PowerModeChangedEventArgs e)
		{
			if (e.Mode == PowerModes.Suspend)
			{
				instance?.cancelClosing.Cancel();
				return;
			}

			double m = (DateTime.Now - alarmTime).TotalMinutes;
			if (m >= 0) UpdateAlarmTime();
			Util.WakePC(alarmTime);
			SetAlarm();
			if (0 <= m && m < 3) new Alarm(true).Show();
		}


		private static void UpdateAlarmTime()
		{
			var now = DateTime.Now.AddDays(1);
			alarmTime = new(now.Year, now.Month, now.Day, alarmTime.Hour, alarmTime.Minute, 0);
		}
		#endregion
	}
}
