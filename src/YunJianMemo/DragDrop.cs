using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace YunJianMemo;

public partial class MainWindow
{
    private const string TaskDragFormat = "YunJianMemo.Task";
    private Point _taskDragStart;
    private TaskItem? _taskDragCandidate;
    private bool _suppressTaskClick;
    private Border? _highlightQuadrant;
    private Brush? _highlightQuadrantBorder;
    private Thickness _highlightQuadrantThickness;
    private Border? _highlightCalendarCell;
    private Brush? _highlightCalendarBackground;
    private Brush? _highlightCalendarBorder;
    private Thickness _highlightCalendarThickness;

    private void TaskDrag_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _suppressTaskClick = false;
        if (IsInteractiveSource(e.OriginalSource))
        {
            _taskDragCandidate = null;
            return;
        }
        if (sender is FrameworkElement { DataContext: TaskItem task })
        {
            _taskDragCandidate = task;
            _taskDragStart = e.GetPosition(this);
        }
    }

    private void TaskDrag_MouseMove(object sender, MouseEventArgs e)
    {
        if (_taskDragCandidate == null || e.LeftButton != MouseButtonState.Pressed) return;
        var position = e.GetPosition(this);
        if (Math.Abs(position.X - _taskDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(position.Y - _taskDragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        var task = _taskDragCandidate;
        _taskDragCandidate = null;
        _suppressTaskClick = true;
        var data = new DataObject();
        data.SetData(TaskDragFormat, task);
        try
        {
            DragDrop.DoDragDrop(sender as DependencyObject ?? this, data, DragDropEffects.Move);
        }
        catch (Exception ex)
        {
            Store.Log("拖动任务失败。", ex);
        }
    }

    private static TaskItem? GetDraggedTask(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(TaskDragFormat)) return null;
        return e.Data.GetData(TaskDragFormat) as TaskItem;
    }

    // ───────────────────────── 拖到四象限 ─────────────────────────

    private void Quadrant_DragEnter(object sender, DragEventArgs e)
    {
        if (GetDraggedTask(e) == null) return;
        if (sender is Border card) HighlightQuadrant(card);
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void Quadrant_DragOver(object sender, DragEventArgs e)
    {
        if (GetDraggedTask(e) == null)
        {
            e.Effects = DragDropEffects.None;
        }
        else
        {
            if (sender is Border card) HighlightQuadrant(card);
            e.Effects = DragDropEffects.Move;
        }
        e.Handled = true;
    }

    private void Quadrant_DragLeave(object sender, DragEventArgs e)
    {
        if (ReferenceEquals(sender, _highlightQuadrant)) ClearQuadrantHighlight();
    }

    private void Quadrant_Drop(object sender, DragEventArgs e)
    {
        ClearQuadrantHighlight();
        var task = GetDraggedTask(e);
        if (task == null || sender is not Border card) return;
        if (!int.TryParse(card.Tag?.ToString(), out var quadrant) || quadrant < 1 || quadrant > 4) return;

        var oldImportant = task.IsImportant;
        var oldUrgent = task.IsUrgent;
        var important = quadrant is 1 or 2;
        var urgent = quadrant is 1 or 3;
        if (oldImportant == important && oldUrgent == urgent)
        {
            e.Handled = true;
            return;
        }

        task.IsImportant = important;
        task.IsUrgent = urgent;
        task.RaiseComputed();
        RefreshAll();
        SaveNow();

        var target = task.Quadrant;
        ShowToast($"「{Shorten(task.Title, 12)}」移到 {QuadrantLabel(target)}", () =>
        {
            task.IsImportant = oldImportant;
            task.IsUrgent = oldUrgent;
            task.RaiseComputed();
            RefreshAll();
            SaveNow();
            ShowToast("已撤销象限变更", null, 1.8);
        }, 4.5);
        e.Handled = true;
    }

    private void HighlightQuadrant(Border card)
    {
        if (ReferenceEquals(_highlightQuadrant, card)) return;
        ClearQuadrantHighlight();
        _highlightQuadrant = card;
        _highlightQuadrantBorder = card.BorderBrush;
        _highlightQuadrantThickness = card.BorderThickness;
        card.BorderBrush = ThemeManager.Get("AccentBrush");
        card.BorderThickness = new Thickness(2);
    }

    private void ClearQuadrantHighlight()
    {
        if (_highlightQuadrant == null) return;
        _highlightQuadrant.BorderBrush = _highlightQuadrantBorder;
        _highlightQuadrant.BorderThickness = _highlightQuadrantThickness;
        _highlightQuadrant = null;
        _highlightQuadrantBorder = null;
    }

    // ───────────────────────── 拖到日历日期 ─────────────────────────

    private void CalendarCell_DragEnter(object sender, DragEventArgs e)
    {
        if (GetDraggedTask(e) == null) return;
        if (sender is Border cell) HighlightCalendarCell(cell);
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void CalendarCell_DragOver(object sender, DragEventArgs e)
    {
        if (GetDraggedTask(e) == null)
        {
            e.Effects = DragDropEffects.None;
        }
        else
        {
            if (sender is Border cell) HighlightCalendarCell(cell);
            e.Effects = DragDropEffects.Move;
        }
        e.Handled = true;
    }

    private void CalendarCell_DragLeave(object sender, DragEventArgs e)
    {
        if (ReferenceEquals(sender, _highlightCalendarCell)) ClearCalendarHighlight();
    }

    private void CalendarCell_Drop(object sender, DragEventArgs e)
    {
        ClearCalendarHighlight();
        var task = GetDraggedTask(e);
        if (task == null || sender is not Border { Tag: DateTime date }) return;

        var oldDue = task.DueAt;
        var time = oldDue?.TimeOfDay ?? TimeSpan.FromHours(9);
        var newDue = date.Date.Add(time);
        if (oldDue == newDue)
        {
            e.Handled = true;
            return;
        }

        task.DueAt = newDue;
        _selectedDate = date;
        _calendarMonth = new DateTime(date.Year, date.Month, 1);
        RefreshAll();
        SaveNow();

        ShowToast($"「{Shorten(task.Title, 12)}」改到 {newDue:M月d日 HH:mm}", () =>
        {
            task.DueAt = oldDue;
            RefreshAll();
            SaveNow();
            ShowToast("已撤销截止时间", null, 1.8);
        }, 4.5);
        e.Handled = true;
    }

    private void HighlightCalendarCell(Border cell)
    {
        if (ReferenceEquals(_highlightCalendarCell, cell)) return;
        ClearCalendarHighlight();
        _highlightCalendarCell = cell;
        _highlightCalendarBackground = cell.Background;
        _highlightCalendarBorder = cell.BorderBrush;
        _highlightCalendarThickness = cell.BorderThickness;
        cell.Background = ThemeManager.Get("AccentSoftBrush");
        cell.BorderBrush = ThemeManager.Get("AccentBrush");
        cell.BorderThickness = new Thickness(2);
    }

    private void ClearCalendarHighlight()
    {
        if (_highlightCalendarCell == null) return;
        _highlightCalendarCell.Background = _highlightCalendarBackground;
        _highlightCalendarCell.BorderBrush = _highlightCalendarBorder;
        _highlightCalendarCell.BorderThickness = _highlightCalendarThickness;
        _highlightCalendarCell = null;
        _highlightCalendarBackground = null;
        _highlightCalendarBorder = null;
    }
}
