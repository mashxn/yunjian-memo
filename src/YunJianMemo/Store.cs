using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace YunJianMemo;

public static class AppPaths
{
    public static string DataDirectory
    {
        get
        {
            var custom = Environment.GetEnvironmentVariable("YUNJIAN_DATA_DIR");
            if (!string.IsNullOrWhiteSpace(custom)) return custom;
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "YunJianMemo");
        }
    }

    public static string DataFile => Path.Combine(DataDirectory, "data.json");
    public static string BackupFile => Path.Combine(DataDirectory, "data.backup.json");
    public static string LogFile => Path.Combine(DataDirectory, "error.log");
}

public static class Store
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    public static AppData Load()
    {
        try
        {
            if (!File.Exists(AppPaths.DataFile))
                return new AppData();

            var json = File.ReadAllText(AppPaths.DataFile);
            var data = JsonSerializer.Deserialize<AppData>(json, JsonOptions) ?? new AppData();
            Normalize(data);
            return data;
        }
        catch (Exception ex)
        {
            Log("读取数据失败，将使用备份或空数据。", ex);
            TryQuarantineBrokenFile();
            try
            {
                if (File.Exists(AppPaths.BackupFile))
                {
                    var backup = JsonSerializer.Deserialize<AppData>(
                        File.ReadAllText(AppPaths.BackupFile), JsonOptions);
                    if (backup != null)
                    {
                        Normalize(backup);
                        return backup;
                    }
                }
            }
            catch (Exception backupEx)
            {
                Log("读取备份失败。", backupEx);
            }
            return new AppData();
        }
    }

    public static bool Save(AppData data)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataDirectory);
            var temp = AppPaths.DataFile + ".tmp";
            var json = JsonSerializer.Serialize(data, JsonOptions);
            File.WriteAllText(temp, json);

            if (File.Exists(AppPaths.DataFile))
            {
                try { File.Copy(AppPaths.DataFile, AppPaths.BackupFile, true); }
                catch { /* 备份失败不阻断主存档 */ }
            }

            File.Move(temp, AppPaths.DataFile, true);
            return true;
        }
        catch (Exception ex)
        {
            Log("保存数据失败。", ex);
            return false;
        }
    }

    private static void Normalize(AppData data)
    {
        data.Tasks ??= new List<TaskItem>();
        data.PomodoroRecords ??= new List<PomodoroRecord>();
        data.DeletedTasks ??= new List<TaskItem>();
        data.Settings ??= new AppSettings();
        data.Timer ??= new TimerSnapshot();

        var seen = new HashSet<Guid>();
        foreach (var task in data.Tasks)
        {
            if (task.Id == Guid.Empty || !seen.Add(task.Id)) task.Id = Guid.NewGuid();
            task.Title = task.Title?.Trim() ?? "";
            task.Notes ??= "";
        }

        var s = data.Settings;
        s.FocusMinutes = Math.Clamp(s.FocusMinutes, 1, 180);
        s.ShortBreakMinutes = Math.Clamp(s.ShortBreakMinutes, 1, 60);
        s.LongBreakMinutes = Math.Clamp(s.LongBreakMinutes, 1, 120);
        s.LongBreakAfter = Math.Clamp(s.LongBreakAfter, 2, 8);
        if (s.WindowWidth < 340 || s.WindowWidth > 760) s.WindowWidth = 408;
        if (s.WindowHeight < 560 || s.WindowHeight > 1200) s.WindowHeight = 712;

        var timer = data.Timer;
        if (timer.RemainingSeconds < 0 || timer.RemainingSeconds > 3 * 60 * 60)
            timer.RemainingSeconds = s.FocusMinutes * 60.0;
        if (timer.FocusStreak < 0) timer.FocusStreak = 0;
    }

    private static void TryQuarantineBrokenFile()
    {
        try
        {
            if (!File.Exists(AppPaths.DataFile)) return;
            var target = Path.Combine(
                AppPaths.DataDirectory,
                $"data.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            File.Move(AppPaths.DataFile, target, false);
            Log($"已将损坏的数据文件保留为 {target}", null);
        }
        catch (Exception ex)
        {
            Log("保留损坏数据文件失败。", ex);
        }
    }

    public static void Log(string message, Exception? ex)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataDirectory);
            File.AppendAllText(AppPaths.LogFile,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}{ex}{Environment.NewLine}");
        }
        catch { /* 日志失败不影响主流程 */ }
    }
}
