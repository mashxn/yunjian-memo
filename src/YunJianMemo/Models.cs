using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace YunJianMemo;

public enum QuadrantKind
{
    ImportantUrgent = 1,
    ImportantNotUrgent = 2,
    UrgentNotImportant = 3,
    NotImportantNotUrgent = 4
}

public enum TimerMode
{
    Focus,
    ShortBreak,
    LongBreak
}

public class TaskItem : INotifyPropertyChanged
{
    private string _title = "";
    private string _notes = "";
    private DateTime? _dueAt;
    private bool _isImportant;
    private bool _isUrgent;
    private bool _isDone;
    private DateTime? _completedAt;
    private int _pomodoroCount;

    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public string Title
    {
        get => _title;
        set { if (_title != value) { _title = value; OnPropertyChanged(); } }
    }

    public string Notes
    {
        get => _notes;
        set { if (_notes != value) { _notes = value; OnPropertyChanged(); } }
    }

    public DateTime? DueAt
    {
        get => _dueAt;
        set
        {
            if (_dueAt != value)
            {
                _dueAt = value;
                OnPropertyChanged();
                RaiseComputed();
            }
        }
    }

    public bool IsImportant
    {
        get => _isImportant;
        set
        {
            if (_isImportant != value)
            {
                _isImportant = value;
                OnPropertyChanged();
                RaiseComputed();
            }
        }
    }

    public bool IsUrgent
    {
        get => _isUrgent;
        set
        {
            if (_isUrgent != value)
            {
                _isUrgent = value;
                OnPropertyChanged();
                RaiseComputed();
            }
        }
    }

    public bool IsDone
    {
        get => _isDone;
        set
        {
            if (_isDone != value)
            {
                _isDone = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsNotDone));
                RaiseComputed();
            }
        }
    }

    public DateTime? CompletedAt
    {
        get => _completedAt;
        set { if (_completedAt != value) { _completedAt = value; OnPropertyChanged(); } }
    }

    public int PomodoroCount
    {
        get => _pomodoroCount;
        set { if (_pomodoroCount != value) { _pomodoroCount = value; OnPropertyChanged(); } }
    }

    [JsonIgnore] public bool IsNotDone => !_isDone;
    [JsonIgnore] public bool HasDue => _dueAt.HasValue;

    [JsonIgnore]
    public QuadrantKind Quadrant => _isImportant
        ? (_isUrgent ? QuadrantKind.ImportantUrgent : QuadrantKind.ImportantNotUrgent)
        : (_isUrgent ? QuadrantKind.UrgentNotImportant : QuadrantKind.NotImportantNotUrgent);

    [JsonIgnore]
    public string QuadrantLabel => Quadrant switch
    {
        QuadrantKind.ImportantUrgent => "重要 · 紧急",
        QuadrantKind.ImportantNotUrgent => "重要 · 不紧急",
        QuadrantKind.UrgentNotImportant => "紧急 · 不重要",
        _ => "不紧急 · 不重要"
    };

    [JsonIgnore]
    public Brush QuadrantBrush => Palette.QuadrantBrush(Quadrant);

    [JsonIgnore]
    public string QuadrantColor => Palette.QuadrantColor(Quadrant);

    [JsonIgnore]
    public bool IsOverdue => !_isDone && _dueAt.HasValue && _dueAt.Value < DateTime.Now;

    [JsonIgnore]
    public string DueShort
    {
        get
        {
            if (!_dueAt.HasValue) return "未设截止";
            var due = _dueAt.Value;
            var today = DateTime.Today;
            if (due.Date == today) return $"今天 {due:HH:mm}";
            if (due.Date == today.AddDays(1)) return $"明天 {due:HH:mm}";
            if (due.Year == today.Year) return $"{due:M月d日 HH:mm}";
            return $"{due:yyyy/M/d HH:mm}";
        }
    }

    [JsonIgnore]
    public string DueFull => _dueAt?.ToString("yyyy年M月d日 HH:mm", CultureInfo.GetCultureInfo("zh-CN")) ?? "未设置截止时间";

    [JsonIgnore]
    public string DueCompact
    {
        get
        {
            if (!_dueAt.HasValue) return "";
            var due = _dueAt.Value;
            var today = DateTime.Today;
            if (due.Date == today) return due.ToString("HH:mm");
            if (due.Date == today.AddDays(1)) return $"明 {due:HH:mm}";
            if (due.Year == today.Year) return due.ToString("M/d");
            return due.ToString("yy/M/d");
        }
    }

    [JsonIgnore]
    public string TimeShort => _dueAt?.ToString("HH:mm") ?? "全天";

    [JsonIgnore]
    public string CalendarDayText => _dueAt?.ToString("M月d日") ?? "";

    [JsonIgnore]
    public string PomodoroText => _pomodoroCount > 0 ? $"🍅 {_pomodoroCount}" : "";

    [JsonIgnore]
    public string NotesPreview => string.IsNullOrWhiteSpace(_notes) ? "" : _notes.Replace("\r", " ").Replace("\n", " ");

    public event PropertyChangedEventHandler? PropertyChanged;

    public void RaiseComputed()
    {
        OnPropertyChanged(nameof(IsNotDone));
        OnPropertyChanged(nameof(HasDue));
        OnPropertyChanged(nameof(Quadrant));
        OnPropertyChanged(nameof(QuadrantLabel));
        OnPropertyChanged(nameof(QuadrantBrush));
        OnPropertyChanged(nameof(QuadrantColor));
        OnPropertyChanged(nameof(IsOverdue));
        OnPropertyChanged(nameof(DueShort));
        OnPropertyChanged(nameof(DueFull));
        OnPropertyChanged(nameof(DueCompact));
        OnPropertyChanged(nameof(TimeShort));
        OnPropertyChanged(nameof(CalendarDayText));
        OnPropertyChanged(nameof(PomodoroText));
        OnPropertyChanged(nameof(NotesPreview));
    }

    public TaskItem Clone() => new()
    {
        Id = Id,
        Title = Title,
        Notes = Notes,
        CreatedAt = CreatedAt,
        DueAt = DueAt,
        IsImportant = IsImportant,
        IsUrgent = IsUrgent,
        IsDone = IsDone,
        CompletedAt = CompletedAt,
        PomodoroCount = PomodoroCount
    };

    public void CopyFrom(TaskItem other)
    {
        Title = other.Title;
        Notes = other.Notes;
        DueAt = other.DueAt;
        IsImportant = other.IsImportant;
        IsUrgent = other.IsUrgent;
        IsDone = other.IsDone;
        CompletedAt = other.CompletedAt;
        PomodoroCount = other.PomodoroCount;
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class FocusTaskChoice
{
    public Guid? Id { get; init; }
    public string Title { get; init; } = "";
    public override string ToString() => Title;
}

public class PomodoroRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CompletedAt { get; set; } = DateTime.Now;
    public int Minutes { get; set; }
    public Guid? TaskId { get; set; }
    public string TaskTitle { get; set; } = "";
}

public class TimerSnapshot
{
    public TimerMode Mode { get; set; } = TimerMode.Focus;
    public double RemainingSeconds { get; set; } = 25 * 60;
    public bool IsRunning { get; set; }
    public DateTime? DeadlineUtc { get; set; }
    public Guid? LinkedTaskId { get; set; }
    public int FocusStreak { get; set; }
}

public class AppSettings
{
    public bool DesktopMode { get; set; } = true;
    public bool AlwaysOnTop { get; set; }
    public bool AutoStart { get; set; }
    public bool SoundEnabled { get; set; } = true;
    public bool AutoStartNext { get; set; }
    public bool SnapToEdge { get; set; } = true;
    public string Theme { get; set; } = "Dark";
    public string AccentColor { get; set; } = "#6EE7D8";
    public double CardOpacity { get; set; } = 0.96;
    public double UiScale { get; set; } = 1.0;
    public int FocusMinutes { get; set; } = 25;
    public int ShortBreakMinutes { get; set; } = 5;
    public int LongBreakMinutes { get; set; } = 15;
    public int LongBreakAfter { get; set; } = 4;
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double WindowWidth { get; set; } = 408;
    public double WindowHeight { get; set; } = 712;
}

public class AppData
{
    public List<TaskItem> Tasks { get; set; } = new();
    public List<PomodoroRecord> PomodoroRecords { get; set; } = new();
    public List<TaskItem> DeletedTasks { get; set; } = new();
    public AppSettings Settings { get; set; } = new();
    public TimerSnapshot Timer { get; set; } = new();
    public bool HasLaunched { get; set; }
}

public static class Palette
{
    public const string AccentHex = "#6EE7D8";
    public const string CoralHex = "#FF7A7A";
    public const string AmberHex = "#FFC46B";
    public const string VioletHex = "#A78BFA";
    public const string SlateHex = "#8B9BB4";

    private static readonly Dictionary<QuadrantKind, SolidColorBrush> QuadrantBrushes = new()
    {
        [QuadrantKind.ImportantUrgent] = Frozen(CoralHex),
        [QuadrantKind.ImportantNotUrgent] = Frozen(AccentHex),
        [QuadrantKind.UrgentNotImportant] = Frozen(AmberHex),
        [QuadrantKind.NotImportantNotUrgent] = Frozen(SlateHex)
    };

    public static Brush QuadrantBrush(QuadrantKind kind) => QuadrantBrushes[kind];

    public static string QuadrantColor(QuadrantKind kind) => kind switch
    {
        QuadrantKind.ImportantUrgent => CoralHex,
        QuadrantKind.ImportantNotUrgent => AccentHex,
        QuadrantKind.UrgentNotImportant => AmberHex,
        _ => SlateHex
    };

    public static SolidColorBrush Frozen(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
