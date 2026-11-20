using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;

namespace YunJianMemo;

public partial class MainWindow
{
    private readonly DispatcherTimer _timerTicker = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private TimerMode _timerMode = TimerMode.Focus;
    private TimeSpan _timerDuration = TimeSpan.FromMinutes(25);
    private TimeSpan _timerRemaining = TimeSpan.FromMinutes(25);
    private bool _timerRunning;
    private DateTime _timerDeadline;
    private int _focusStreak;
    private Guid? _linkedTaskId;
    private int _lastDotCycle = -1;
    private bool _completingPhase;

    private void InitializePomodoro()
    {
        _timerTicker.Tick += TimerTicker_Tick;
        var snapshot = _data.Timer ?? new TimerSnapshot();
        _timerMode = snapshot.Mode;
        _focusStreak = Math.Max(0, snapshot.FocusStreak);
        _linkedTaskId = snapshot.LinkedTaskId;
        _timerDuration = DurationForMode(_timerMode);

        var remainingSeconds = snapshot.RemainingSeconds;
        _timerRunning = false;
        if (snapshot.IsRunning && snapshot.DeadlineUtc.HasValue)
        {
            var deadline = snapshot.DeadlineUtc.Value.ToLocalTime();
            var seconds = (deadline - DateTime.Now).TotalSeconds;
            if (seconds > 0.5)
            {
                remainingSeconds = seconds;
                _timerDeadline = deadline;
                _timerRunning = true;
            }
            else
            {
                remainingSeconds = 0;
            }
        }

        if (remainingSeconds <= 0.1)
        {
            remainingSeconds = _timerDuration.TotalSeconds;
            _timerRunning = false;
        }
        remainingSeconds = Math.Min(remainingSeconds, _timerDuration.TotalSeconds);
        _timerRemaining = TimeSpan.FromSeconds(remainingSeconds);

        SetModeTabChecks();
        UpdateTimerUi();
    }

    private void RestorePomodoroTimer()
    {
        if (_timerRunning)
        {
            _timerTicker.Start();
        }
        UpdateTimerUi();
    }

    private void SetModeTabChecks()
    {
        _updatingTimerModeTabs = true;
        FocusModeTab.IsChecked = _timerMode == TimerMode.Focus;
        ShortModeTab.IsChecked = _timerMode == TimerMode.ShortBreak;
        LongModeTab.IsChecked = _timerMode == TimerMode.LongBreak;
        _updatingTimerModeTabs = false;
    }

    private TimeSpan DurationForMode(TimerMode mode) => mode switch
    {
        TimerMode.Focus => TimeSpan.FromMinutes(Math.Clamp(_data.Settings.FocusMinutes, 1, 180)),
        TimerMode.ShortBreak => TimeSpan.FromMinutes(Math.Clamp(_data.Settings.ShortBreakMinutes, 1, 60)),
        _ => TimeSpan.FromMinutes(Math.Clamp(_data.Settings.LongBreakMinutes, 1, 120))
    };

    private void TimerMode_Checked(object sender, RoutedEventArgs e)
    {
        if (_initializing || _updatingTimerModeTabs || sender is not RadioButton { IsChecked: true, Tag: string tag })
            return;

        var mode = tag switch
        {
            "short" => TimerMode.ShortBreak,
            "long" => TimerMode.LongBreak,
            _ => TimerMode.Focus
        };
        if (mode == _timerMode) return;

        var wasRunning = _timerRunning;
        StopTimerTicker();
        _timerRunning = false;
        _timerMode = mode;
        _timerDuration = DurationForMode(mode);
        _timerRemaining = _timerDuration;
        UpdateTimerUi();
        RefreshPomodoroStats();
        QueueSave();
        if (wasRunning) ShowToast("已切换阶段，计时重新开始", null, 2.2);
    }

    private void ToggleTimer()
    {
        if (_timerRunning) PauseTimer();
        else StartTimer();
    }

    private void TimerToggle_Click(object sender, RoutedEventArgs e) => ToggleTimer();

    private void StartTimer()
    {
        if (_timerRemaining <= TimeSpan.FromMilliseconds(200))
            _timerRemaining = _timerDuration;
        _timerRunning = true;
        _timerDeadline = DateTime.Now + _timerRemaining;
        _timerTicker.Start();
        UpdateTimerUi();
        QueueSave();
        if (!IsVisible) ShowWidget(false);
    }

    private void PauseTimer()
    {
        if (!_timerRunning) return;
        _timerRemaining = _timerDeadline - DateTime.Now;
        if (_timerRemaining < TimeSpan.Zero) _timerRemaining = TimeSpan.Zero;
        _timerRunning = false;
        StopTimerTicker();
        UpdateTimerUi();
        QueueSave();
    }

    private void TimerReset_Click(object sender, RoutedEventArgs e) => ResetTimer(true);

    private void ResetTimer(bool notify)
    {
        StopTimerTicker();
        _timerRunning = false;
        _timerDuration = DurationForMode(_timerMode);
        _timerRemaining = _timerDuration;
        UpdateTimerUi();
        QueueSave();
        if (notify) ShowToast("本阶段计时已重置", null, 1.8);
    }

    private void TimerSkip_Click(object sender, RoutedEventArgs e)
    {
        StopTimerTicker();
        _timerRunning = false;
        if (_timerMode == TimerMode.Focus)
        {
            SetTimerModeInternal(TimerMode.ShortBreak);
            ShowToast("已跳过专注，进入短休息", null, 2);
        }
        else
        {
            SetTimerModeInternal(TimerMode.Focus);
            ShowToast("已跳过休息，回到专注", null, 2);
        }
        QueueSave();
    }

    private void TimerTicker_Tick(object? sender, EventArgs e)
    {
        if (!_timerRunning) return;
        var remaining = _timerDeadline - DateTime.Now;
        if (remaining <= TimeSpan.Zero)
        {
            _timerRemaining = TimeSpan.Zero;
            _timerRunning = false;
            StopTimerTicker();
            UpdateTimerUi();
            CompletePhase();
        }
        else
        {
            _timerRemaining = remaining;
            UpdateTimerUi();
        }
    }

    private void CompletePhase()
    {
        if (_completingPhase) return;
        _completingPhase = true;
        try
        {
            var finished = _timerMode;
            if (finished == TimerMode.Focus)
            {
                _focusStreak++;
                RecordFocusSession();
                var longAfter = Math.Clamp(_data.Settings.LongBreakAfter, 2, 8);
                var next = _focusStreak % longAfter == 0 ? TimerMode.LongBreak : TimerMode.ShortBreak;
                SetTimerModeInternal(next);
                var message = next == TimerMode.LongBreak
                    ? $"完成一轮专注 · 长休息 {_data.Settings.LongBreakMinutes} 分钟"
                    : $"专注完成 · 短休息 {_data.Settings.ShortBreakMinutes} 分钟";
                ShowToast(message, null, 5);
                PlayChime();
                ShowTrayBalloon("专注完成", message);
            }
            else
            {
                SetTimerModeInternal(TimerMode.Focus);
                ShowToast("休息结束 · 回来继续专注吧", null, 4);
                PlayChime();
                ShowTrayBalloon("休息结束", "下一轮专注已准备好。");
            }

            RefreshAll();
            SaveNow();
            if (_data.Settings.AutoStartNext) StartTimer();
        }
        finally
        {
            _completingPhase = false;
        }
    }

    private void SetTimerModeInternal(TimerMode mode)
    {
        _timerMode = mode;
        _timerDuration = DurationForMode(mode);
        _timerRemaining = _timerDuration;
        _timerRunning = false;
        SetModeTabChecks();
        UpdateTimerUi();
        RefreshPomodoroStats();
    }

    private void RecordFocusSession()
    {
        var task = _linkedTaskId.HasValue
            ? _data.Tasks.FirstOrDefault(t => t.Id == _linkedTaskId.Value)
            : null;
        if (task != null) task.PomodoroCount++;

        _data.PomodoroRecords.Insert(0, new PomodoroRecord
        {
            CompletedAt = DateTime.Now,
            Minutes = Math.Max(1, (int)Math.Round(_timerDuration.TotalMinutes)),
            TaskId = task?.Id,
            TaskTitle = task?.Title ?? ""
        });
        if (_data.PomodoroRecords.Count > 500)
            _data.PomodoroRecords.RemoveRange(500, _data.PomodoroRecords.Count - 500);
    }

    private void UpdateTimerUi()
    {
        var accent = _timerMode switch
        {
            TimerMode.Focus => Palette.Frozen(Palette.AccentHex),
            TimerMode.ShortBreak => Palette.Frozen(Palette.AmberHex),
            _ => Palette.Frozen(Palette.VioletHex)
        };
        TimerRing.AccentBrush = accent;
        TimerRing.Progress = _timerDuration.TotalSeconds > 0
            ? 1 - Math.Clamp(_timerRemaining.TotalSeconds / _timerDuration.TotalSeconds, 0, 1)
            : 0;

        TimerModeLabel.Text = _timerMode switch
        {
            TimerMode.Focus => "专  注",
            TimerMode.ShortBreak => "短 休 息",
            _ => "长 休 息"
        };
        TimerModeLabel.Foreground = accent;
        TimerText.Text = FormatRemaining(_timerRemaining);

        if (_timerRunning) TimerToggleButton.Content = "暂停";
        else if (_timerRemaining < _timerDuration - TimeSpan.FromMilliseconds(500))
            TimerToggleButton.Content = "继续";
        else TimerToggleButton.Content = _timerMode == TimerMode.Focus ? "开始专注" : "开始休息";

        UpdateSessionDots();
        RefreshPomodoroStats();
    }

    private static string FormatRemaining(TimeSpan time)
    {
        var total = (int)Math.Ceiling(Math.Max(0, time.TotalSeconds));
        return total >= 3600
            ? $"{total / 3600}:{total % 3600 / 60:00}:{total % 60:00}"
            : $"{total / 60:00}:{total % 60:00}";
    }

    private void UpdateSessionDots()
    {
        var longAfter = Math.Clamp(_data.Settings.LongBreakAfter, 2, 8);
        var cycle = _focusStreak % longAfter;
        if (cycle == _lastDotCycle && SessionDotsPanel.Children.Count == longAfter) return;
        _lastDotCycle = cycle;

        var accent = _timerMode switch
        {
            TimerMode.Focus => Palette.Frozen(Palette.AccentHex),
            TimerMode.ShortBreak => Palette.Frozen(Palette.AmberHex),
            _ => Palette.Frozen(Palette.VioletHex)
        };
        SessionDotsPanel.Children.Clear();
        for (var i = 0; i < longAfter; i++)
        {
            SessionDotsPanel.Children.Add(new System.Windows.Shapes.Ellipse
            {
                Width = 5,
                Height = 5,
                Margin = new Thickness(2, 0, 2, 0),
                Fill = i < cycle ? accent : new SolidColorBrush(Color.FromArgb(38, 255, 255, 255))
            });
        }
    }

    private void RefreshPomodoroStats()
    {
        GetTodayFocusStats(out var count, out var minutes);
        FocusStatsText.Text = $"今日专注 {count} 次 · {minutes} 分钟";
        var longAfter = Math.Clamp(_data.Settings.LongBreakAfter, 2, 8);
        var cycle = _focusStreak % longAfter;
        CycleText.Text = cycle == 0 && _focusStreak > 0
            ? "完成一轮 · 休息一下"
            : $"第 {cycle + 1} / {longAfter} 轮";
    }

    private void GetTodayFocusStats(out int count, out int minutes)
    {
        var today = DateTime.Today;
        var records = _data.PomodoroRecords.Where(r => r.CompletedAt.Date == today).ToList();
        count = records.Count;
        minutes = records.Sum(r => r.Minutes);
    }

    private int GetTodayFocusMinutes()
    {
        GetTodayFocusStats(out _, out var minutes);
        return minutes;
    }

    private void RefreshFocusChoices()
    {
        var choices = new List<FocusTaskChoice> { new() { Id = null, Title = "不关联任务" } };
        var pending = _data.Tasks.Where(t => !t.IsDone)
            .OrderBy(t => t.DueAt ?? DateTime.MaxValue)
            .ThenBy(t => t.CreatedAt)
            .ToList();
        foreach (var task in pending)
            choices.Add(new FocusTaskChoice { Id = task.Id, Title = task.Title });

        if (_linkedTaskId.HasValue)
        {
            var linked = _data.Tasks.FirstOrDefault(t => t.Id == _linkedTaskId.Value);
            if (linked == null)
            {
                _linkedTaskId = null;
            }
            else if (linked.IsDone && choices.All(c => c.Id != linked.Id))
            {
                choices.Add(new FocusTaskChoice { Id = linked.Id, Title = linked.Title + "（已完成）" });
            }
        }

        _updatingFocusChoices = true;
        FocusTaskCombo.ItemsSource = choices;
        FocusTaskCombo.SelectedItem = choices.FirstOrDefault(c => c.Id == _linkedTaskId) ?? choices[0];
        _linkedTaskId = (FocusTaskCombo.SelectedItem as FocusTaskChoice)?.Id;
        _updatingFocusChoices = false;
    }

    private void FocusTaskCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingFocusChoices || _initializing) return;
        _linkedTaskId = (FocusTaskCombo.SelectedItem as FocusTaskChoice)?.Id;
        QueueSave();
    }

    private void ApplyDurationChanges()
    {
        if (_timerRunning) return;
        _timerDuration = DurationForMode(_timerMode);
        _timerRemaining = _timerDuration;
        UpdateTimerUi();
    }

    private void CaptureTimerSnapshot()
    {
        _data.Timer = new TimerSnapshot
        {
            Mode = _timerMode,
            RemainingSeconds = Math.Max(0, _timerRemaining.TotalSeconds),
            IsRunning = _timerRunning,
            DeadlineUtc = _timerRunning ? _timerDeadline.ToUniversalTime() : null,
            LinkedTaskId = _linkedTaskId,
            FocusStreak = _focusStreak
        };
    }

    private void StopTimerTicker()
    {
        _timerTicker.Stop();
    }

    private void PlayChime()
    {
        if (!_data.Settings.SoundEnabled) return;
        try
        {
            System.Media.SystemSounds.Asterisk.Play();
        }
        catch { /* 系统声音不可用时不打断流程 */ }
    }

    private void ShowTrayBalloon(string title, string message)
    {
        try
        {
            _tray?.ShowBalloonTip(4000, title, message, WinForms.ToolTipIcon.None);
        }
        catch { /* ignore */ }
    }
}
