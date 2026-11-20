using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace YunJianMemo;

public partial class MainWindow
{
    // ───────────────────────── 四象限 ─────────────────────────

    private void RefreshQuadrants()
    {
        var pending = _data.Tasks.Where(t => !t.IsDone).ToList();

        FillQuadrant(_q1, pending, QuadrantKind.ImportantUrgent);
        FillQuadrant(_q2, pending, QuadrantKind.ImportantNotUrgent);
        FillQuadrant(_q3, pending, QuadrantKind.UrgentNotImportant);
        FillQuadrant(_q4, pending, QuadrantKind.NotImportantNotUrgent);

        PendingCountText.Text = $"{pending.Count} 项待办";
        Q1Count.Text = _q1.Count.ToString();
        Q2Count.Text = _q2.Count.ToString();
        Q3Count.Text = _q3.Count.ToString();
        Q4Count.Text = _q4.Count.ToString();
        Q1Empty.Visibility = _q1.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Q2Empty.Visibility = _q2.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Q3Empty.Visibility = _q3.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Q4Empty.Visibility = _q4.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void FillQuadrant(System.Collections.ObjectModel.ObservableCollection<TaskItem> list,
        IEnumerable<TaskItem> pending, QuadrantKind kind)
    {
        var items = pending
            .Where(t => t.Quadrant == kind)
            .OrderBy(t => t.DueAt ?? DateTime.MaxValue)
            .ThenBy(t => t.CreatedAt)
            .ToList();
        list.Clear();
        foreach (var item in items) list.Add(item);
    }

    // ───────────────────────── 任务编辑器 ─────────────────────────

    private void AddTaskButton_Click(object sender, RoutedEventArgs e) => OpenTaskEditor(null);

    private void OpenTaskEditor(TaskItem? task, DateTime? defaultDate = null)
    {
        if (AllTasksOverlay.Visibility == Visibility.Visible) HideOverlay(AllTasksOverlay);

        _editingTask = task;
        _deleteArmed = false;
        _deleteResetTimer.Stop();
        TaskEditorDeleteButton.Content = "删除任务";
        TaskEditorDeleteButton.Visibility = task == null ? Visibility.Collapsed : Visibility.Visible;
        TaskEditorTitle.Text = task == null ? "新建任务" : "编辑任务";
        TaskEditorSaveButton.Content = task == null ? "添加" : "保存";
        TaskEditorError.Text = "";

        TaskTitleBox.Text = task?.Title ?? "";
        TaskNotesBox.Text = task?.Notes ?? "";

        var hasDue = task?.DueAt.HasValue == true || (task == null && defaultDate.HasValue);
        var due = task?.DueAt
                  ?? (defaultDate.HasValue
                      ? defaultDate.Value.Date.AddHours(9)
                      : DateTime.Today.AddHours(18));

        DueEnabledCheck.IsChecked = hasDue;
        DueDateBox.Text = due.ToString("yyyy-MM-dd");
        DueTimeBox.Text = due.ToString("HH:mm");
        ImportantCheck.IsChecked = task?.IsImportant ?? false;
        UrgentCheck.IsChecked = task?.IsUrgent ?? false;

        UpdateDueFieldState();
        UpdateQuadrantPreview();
        ShowOverlay(TaskEditor);

        Dispatcher.BeginInvoke(new Action(() =>
        {
            TaskTitleBox.Focus();
            TaskTitleBox.CaretIndex = TaskTitleBox.Text.Length;
        }), DispatcherPriority.Input);
    }

    private void CloseTaskEditor_Click(object sender, RoutedEventArgs e) => CloseTaskEditor();

    private void CloseTaskEditor()
    {
        HideOverlay(TaskEditor);
        _editingTask = null;
        _deleteArmed = false;
        _deleteResetTimer.Stop();
        TaskEditorDeleteButton.Content = "删除任务";
    }

    private void DueEnabled_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        UpdateDueFieldState();
    }

    private void UpdateDueFieldState()
    {
        var enabled = DueEnabledCheck.IsChecked == true;
        DueFieldsGrid.IsEnabled = enabled;
        DueFieldsGrid.Opacity = enabled ? 1 : 0.42;
        if (enabled)
        {
            if (string.IsNullOrWhiteSpace(DueDateBox.Text))
                DueDateBox.Text = DateTime.Today.ToString("yyyy-MM-dd");
            if (string.IsNullOrWhiteSpace(DueTimeBox.Text))
                DueTimeBox.Text = "18:00";
        }
    }

    private void DueQuickToday_Click(object sender, RoutedEventArgs e) => SetDueDate(DateTime.Today);
    private void DueQuickTomorrow_Click(object sender, RoutedEventArgs e) => SetDueDate(DateTime.Today.AddDays(1));
    private void DueQuickDayAfter_Click(object sender, RoutedEventArgs e) => SetDueDate(DateTime.Today.AddDays(2));

    private void SetDueDate(DateTime date)
    {
        DueEnabledCheck.IsChecked = true;
        DueDateBox.Text = date.ToString("yyyy-MM-dd");
        if (string.IsNullOrWhiteSpace(DueTimeBox.Text)) DueTimeBox.Text = "18:00";
        UpdateDueFieldState();
    }

    private void DueQuickEvening_Click(object sender, RoutedEventArgs e) => SetDueTime("18:00");
    private void DueQuickMorning_Click(object sender, RoutedEventArgs e) => SetDueTime("09:00");

    private void SetDueTime(string time)
    {
        DueEnabledCheck.IsChecked = true;
        DueTimeBox.Text = time;
        if (string.IsNullOrWhiteSpace(DueDateBox.Text)) DueDateBox.Text = DateTime.Today.ToString("yyyy-MM-dd");
        UpdateDueFieldState();
    }

    private void DueTimeBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded || _initializing) return;
        if (DueTimeBox.Text.Length > 5 && !DueTimeBox.Text.Contains(':'))
            DueTimeBox.Text = DueTimeBox.Text[..2] + ":" + DueTimeBox.Text[2..];
    }

    private void Priority_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        UpdateQuadrantPreview();
    }

    private void UpdateQuadrantPreview()
    {
        var important = ImportantCheck.IsChecked == true;
        var urgent = UrgentCheck.IsChecked == true;
        var kind = important
            ? (urgent ? QuadrantKind.ImportantUrgent : QuadrantKind.ImportantNotUrgent)
            : (urgent ? QuadrantKind.UrgentNotImportant : QuadrantKind.NotImportantNotUrgent);
        QuadrantPreviewDot.Fill = Palette.QuadrantBrush(kind);
        var hint = kind switch
        {
            QuadrantKind.ImportantUrgent => "先处理，别让火继续烧",
            QuadrantKind.ImportantNotUrgent => "安排固定时间，持续投入",
            QuadrantKind.UrgentNotImportant => "能委托就委托，能简化就简化",
            _ => "有空再做，或者干脆不做"
        };
        QuadrantPreviewText.Text = $"将归入：{QuadrantLabel(kind)} · {hint}";
    }

    private static string QuadrantLabel(QuadrantKind kind) => kind switch
    {
        QuadrantKind.ImportantUrgent => "重要 · 紧急",
        QuadrantKind.ImportantNotUrgent => "重要 · 不紧急",
        QuadrantKind.UrgentNotImportant => "紧急 · 不重要",
        _ => "不紧急 · 不重要"
    };

    private void SaveTask_Click(object sender, RoutedEventArgs e) => SaveTask();

    private void SaveTask()
    {
        var title = TaskTitleBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            TaskEditorError.Text = "给这件事起一个名字吧。";
            TaskTitleBox.Focus();
            return;
        }

        DateTime? dueAt;
        try
        {
            dueAt = ReadDueAt();
        }
        catch (FormatException ex)
        {
            TaskEditorError.Text = ex.Message;
            return;
        }

        TaskItem target;
        if (_editingTask == null)
        {
            target = new TaskItem { CreatedAt = DateTime.Now };
            _data.Tasks.Add(target);
        }
        else
        {
            target = _editingTask;
        }

        target.Title = title;
        target.Notes = TaskNotesBox.Text.Trim();
        target.DueAt = dueAt;
        target.IsImportant = ImportantCheck.IsChecked == true;
        target.IsUrgent = UrgentCheck.IsChecked == true;

        if (dueAt.HasValue)
        {
            _selectedDate = dueAt.Value.Date;
            _calendarMonth = new DateTime(_selectedDate.Year, _selectedDate.Month, 1);
        }

        var wasNew = _editingTask == null;
        CloseTaskEditor();
        RefreshAll();
        SaveNow();
        ShowToast(wasNew ? $"已添加「{Shorten(target.Title, 14)}」" : "任务已更新", null, 2.4);
    }

    private DateTime? ReadDueAt()
    {
        if (DueEnabledCheck.IsChecked != true) return null;

        var dateText = DueDateBox.Text.Trim();
        var timeText = DueTimeBox.Text.Trim().Replace('：', ':');
        if (string.IsNullOrWhiteSpace(dateText))
            throw new FormatException("请填写截止日期，例如 2026-11-20。");
        if (string.IsNullOrWhiteSpace(timeText))
            throw new FormatException("请填写截止时间，精确到分钟，例如 18:30。");

        string[] formats = { "yyyy-MM-dd", "yyyy-M-d", "yyyy/MM/dd", "yyyy/M/d", "MM-dd", "M-d", "MM/dd", "M/d" };
        if (!DateTime.TryParseExact(dateText, formats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
        {
            throw new FormatException("日期格式不正确，请使用 yyyy-MM-dd，例如 2026-11-20。");
        }
        if (date.Year < 2000) date = new DateTime(DateTime.Today.Year, date.Month, date.Day);

        int hour;
        int minute;
        if (timeText.Contains(':'))
        {
            var parts = timeText.Split(':', StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !int.TryParse(parts[0], out hour) || !int.TryParse(parts[1], out minute))
                throw new FormatException("时间格式不正确，请使用 24 小时制 HH:mm，例如 18:30。");
        }
        else if ((timeText.Length == 3 || timeText.Length == 4) && int.TryParse(timeText, out var compact))
        {
            hour = compact / 100;
            minute = compact % 100;
        }
        else
        {
            throw new FormatException("时间格式不正确，请使用 24 小时制 HH:mm，例如 18:30。");
        }

        if (hour is < 0 or > 23 || minute is < 0 or > 59)
            throw new FormatException("时间超出范围，小时应为 00–23，分钟应为 00–59。");

        return date.Date.AddHours(hour).AddMinutes(minute);
    }

    private void TaskEditor_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CloseTaskEditor();
            e.Handled = true;
            return;
        }

        var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        if (e.Key == Key.Enter && (ctrl || sender == TaskTitleBox || sender == DueDateBox || sender == DueTimeBox))
        {
            SaveTask();
            e.Handled = true;
        }
    }

    private void DeleteTask_Click(object sender, RoutedEventArgs e)
    {
        if (_editingTask == null) return;
        if (!_deleteArmed)
        {
            _deleteArmed = true;
            TaskEditorDeleteButton.Content = "再点一次确认删除";
            _deleteResetTimer.Stop();
            _deleteResetTimer.Start();
            return;
        }

        var task = _editingTask;
        var index = _data.Tasks.IndexOf(task);
        var wasLinked = _linkedTaskId == task.Id;
        _deleteResetTimer.Stop();
        _data.Tasks.Remove(task);
        if (wasLinked) _linkedTaskId = null;
        _data.DeletedTasks.Insert(0, task);
        if (_data.DeletedTasks.Count > 50)
            _data.DeletedTasks.RemoveRange(50, _data.DeletedTasks.Count - 50);

        CloseTaskEditor();
        RefreshAll();
        SaveNow();
        ShowToast($"已删除「{Shorten(task.Title, 14)}」", () =>
        {
            if (_data.Tasks.Contains(task)) return;
            _data.Tasks.Insert(Math.Clamp(index, 0, _data.Tasks.Count), task);
            if (wasLinked) _linkedTaskId = task.Id;
            _data.DeletedTasks.Remove(task);
            RefreshAll();
            SaveNow();
            ShowToast("任务已恢复", null, 2);
        }, 5);
    }

    private void ResetDeleteConfirmation()
    {
        _deleteResetTimer.Stop();
        _deleteArmed = false;
        TaskEditorDeleteButton.Content = "删除任务";
    }

    private void TaskCheck_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox box || box.DataContext is not TaskItem task) return;
        e.Handled = true;
        var markDone = box.IsChecked == true;
        if (task.IsDone == markDone && (markDone ? task.CompletedAt.HasValue : task.CompletedAt == null))
        {
            // 仍然刷新一次，确保界面与模型一致。
        }

        var previousCompletedAt = task.CompletedAt;
        task.IsDone = markDone;
        task.CompletedAt = markDone ? DateTime.Now : null;

        var title = task.Title;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            RefreshAll();
            QueueSave();
            ShowToast(markDone ? $"已完成「{Shorten(title, 14)}」" : "已恢复为待办", () =>
            {
                task.IsDone = !markDone;
                task.CompletedAt = markDone ? previousCompletedAt : DateTime.Now;
                RefreshAll();
                SaveNow();
                ShowToast("已撤销", null, 1.8);
            }, 4);
        }), DispatcherPriority.Background);
    }

    private void TaskRow_Click(object sender, MouseButtonEventArgs e)
    {
        if (IsInteractiveSource(e.OriginalSource)) return;
        if (sender is not FrameworkElement { DataContext: TaskItem task }) return;
        e.Handled = true;
        OpenTaskEditor(task);
    }

    // ───────────────────────── 全部任务 ─────────────────────────

    private void AllTasksButton_Click(object sender, RoutedEventArgs e) => ShowAllTasks();

    private void ShowAllTasks()
    {
        RefreshAllTasks();
        ShowOverlay(AllTasksOverlay);
    }

    private void CloseAllTasks_Click(object sender, RoutedEventArgs e) => HideOverlay(AllTasksOverlay);

    private void AllFilter_Checked(object sender, RoutedEventArgs e)
    {
        if (_initializing || sender is not RadioButton { IsChecked: true, Tag: string filter }) return;
        _allFilter = filter;
        RefreshAllTasks();
    }

    private void RefreshAllTasks()
    {
        IEnumerable<TaskItem> query;
        switch (_allFilter)
        {
            case "done":
                query = _data.Tasks.Where(t => t.IsDone)
                    .OrderByDescending(t => t.CompletedAt ?? DateTime.MinValue);
                break;
            case "all":
                query = _data.Tasks.OrderBy(t => t.IsDone)
                    .ThenBy(t => t.DueAt ?? DateTime.MaxValue)
                    .ThenByDescending(t => t.CreatedAt);
                break;
            default:
                query = _data.Tasks.Where(t => !t.IsDone)
                    .OrderBy(t => t.DueAt ?? DateTime.MaxValue)
                    .ThenBy(t => t.CreatedAt);
                break;
        }

        var items = query.ToList();
        _allTasks.Clear();
        foreach (var item in items) _allTasks.Add(item);

        var pending = _data.Tasks.Count(t => !t.IsDone);
        var done = _data.Tasks.Count(t => t.IsDone);
        AllTasksCountText.Text = _allFilter switch
        {
            "done" => $"{done} 项已完成",
            "all" => $"{pending} 项未完成 · {done} 项已完成",
            _ => $"{pending} 项未完成"
        };
        AllTasksEmpty.Text = _allFilter switch
        {
            "done" => "还没有完成的任务",
            _ => "这里还没有任务"
        };
        AllTasksEmpty.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AddFromAllTasks_Click(object sender, RoutedEventArgs e)
    {
        HideOverlay(AllTasksOverlay);
        OpenTaskEditor(null);
    }
}
