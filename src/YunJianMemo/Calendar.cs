using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace YunJianMemo;

public partial class MainWindow
{
    private void PrevMonth_Click(object sender, RoutedEventArgs e) => ChangeMonth(-1);
    private void NextMonth_Click(object sender, RoutedEventArgs e) => ChangeMonth(1);

    private void ChangeMonth(int delta)
    {
        var month = _calendarMonth.AddMonths(delta);
        var day = Math.Min(_selectedDate.Day, DateTime.DaysInMonth(month.Year, month.Month));
        _calendarMonth = new DateTime(month.Year, month.Month, 1);
        _selectedDate = new DateTime(month.Year, month.Month, day);
        RefreshCalendar();
    }

    private void TodayButton_Click(object sender, RoutedEventArgs e)
    {
        _selectedDate = DateTime.Today;
        _calendarMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        RefreshCalendar();
    }

    private void AddAgendaButton_Click(object sender, RoutedEventArgs e)
    {
        OpenTaskEditor(null, _selectedDate);
    }

    private void RefreshCalendar()
    {
        MonthText.Text = _calendarMonth.ToString("yyyy年M月");
        BuildCalendarGrid();
        RefreshAgenda();
    }

    private void BuildCalendarGrid()
    {
        CalendarGrid.Children.Clear();
        var first = new DateTime(_calendarMonth.Year, _calendarMonth.Month, 1);
        var offset = ((int)first.DayOfWeek + 6) % 7;
        var start = first.AddDays(-offset);
        var tasksByDay = _data.Tasks
            .Where(t => t.DueAt.HasValue)
            .GroupBy(t => t.DueAt!.Value.Date)
            .ToDictionary(g => g.Key, g => g.OrderBy(t => t.DueAt).ToList());

        for (var i = 0; i < 42; i++)
        {
            var date = start.AddDays(i);
            var inMonth = date.Month == _calendarMonth.Month;
            var isToday = date == DateTime.Today;
            var isSelected = date == _selectedDate;

            var cell = new Border
            {
                Style = (Style)FindResource("CalendarDayCell"),
                Tag = date,
                Height = 30,
                Background = isSelected
                    ? new SolidColorBrush(Color.FromArgb(42, 110, 231, 216))
                    : Brushes.Transparent
            };
            if (isSelected)
            {
                cell.BorderBrush = new SolidColorBrush(Color.FromArgb(120, 110, 231, 216));
                cell.BorderThickness = new Thickness(1);
            }
            else if (isToday)
            {
                cell.BorderBrush = new SolidColorBrush(Color.FromArgb(80, 110, 231, 216));
                cell.BorderThickness = new Thickness(1);
            }

            cell.Opacity = inMonth || isSelected ? 1 : 0.34;
            cell.MouseLeftButtonUp += CalendarDay_Click;

            var content = new Grid();
            var dayText = new TextBlock
            {
                Text = date.Day.ToString(),
                FontSize = 10.5,
                FontFamily = (FontFamily)FindResource("NumberFont"),
                Foreground = isSelected
                    ? new SolidColorBrush(Color.FromRgb(207, 251, 245))
                    : isToday
                        ? new SolidColorBrush(Color.FromRgb(110, 231, 216))
                        : new SolidColorBrush(Color.FromRgb(164, 179, 198)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 3, 0, 0)
            };
            if (isToday) dayText.FontWeight = FontWeights.SemiBold;
            content.Children.Add(dayText);

            var dots = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 3),
                Height = 5
            };

            if (tasksByDay.TryGetValue(date, out var tasks))
            {
                foreach (var task in tasks.Take(3))
                {
                    dots.Children.Add(new Ellipse
                    {
                        Width = 3.4,
                        Height = 3.4,
                        Margin = new Thickness(1, 0, 1, 0),
                        Fill = Palette.QuadrantBrush(task.Quadrant),
                        Opacity = task.IsDone ? 0.35 : 1
                    });
                }
                if (tasks.Count > 3)
                {
                    dots.Children.Add(new TextBlock
                    {
                        Text = "+",
                        FontSize = 7,
                        Foreground = new SolidColorBrush(Color.FromRgb(120, 137, 158)),
                        Margin = new Thickness(1, -3, 0, 0)
                    });
                }
            }
            content.Children.Add(dots);
            cell.Child = content;
            cell.ToolTip = tasks == null
                ? $"{date:M月d日} · 暂无安排"
                : $"{date:M月d日} · {tasks.Count} 项安排";
            CalendarGrid.Children.Add(cell);
        }
    }

    private void CalendarDay_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: DateTime date }) return;
        _selectedDate = date;
        if (date.Month != _calendarMonth.Month || date.Year != _calendarMonth.Year)
            _calendarMonth = new DateTime(date.Year, date.Month, 1);
        RefreshCalendar();
        e.Handled = true;
    }

    private void RefreshAgenda()
    {
        var items = _data.Tasks
            .Where(t => t.DueAt?.Date == _selectedDate)
            .OrderBy(t => t.DueAt)
            .ThenBy(t => t.IsDone)
            .ToList();

        _agenda.Clear();
        foreach (var item in items) _agenda.Add(item);

        var suffix = _selectedDate == DateTime.Today ? " · 今天" : "";
        AgendaDateText.Text = _selectedDate.ToString("M月d日 · dddd") + suffix;
        AgendaCountText.Text = $"{items.Count} 项安排";
        AgendaEmpty.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
