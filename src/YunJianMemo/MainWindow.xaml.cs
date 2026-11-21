using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;

namespace YunJianMemo;

public partial class MainWindow : Window
{
    private readonly AppData _data;
    private readonly ObservableCollection<TaskItem> _q1 = new();
    private readonly ObservableCollection<TaskItem> _q2 = new();
    private readonly ObservableCollection<TaskItem> _q3 = new();
    private readonly ObservableCollection<TaskItem> _q4 = new();
    private readonly ObservableCollection<TaskItem> _agenda = new();
    private readonly ObservableCollection<TaskItem> _allTasks = new();
    private readonly ObservableCollection<TaskItem> _trayTasks = new();
    private readonly DesktopHost _desktopHost = new();

    private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(20) };
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly DispatcherTimer _toastTimer = new() { Interval = TimeSpan.FromSeconds(3.6) };
    private readonly DispatcherTimer _deleteResetTimer = new() { Interval = TimeSpan.FromSeconds(3) };

    private WinForms.NotifyIcon? _tray;
    private WinForms.ContextMenuStrip? _trayMenu;
    private WinForms.ToolStripMenuItem? _trayDesktopItem;
    private WinForms.ToolStripMenuItem? _trayTopItem;
    private WinForms.ToolStripMenuItem? _trayAutoStartItem;
    private Drawing.Icon? _trayIcon;

    private HwndSource? _hwndSource;
    private IntPtr _hwnd;
    private bool _hotkeyRegistered;
    private const int HotkeyId = 0x4D4D;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;

    private bool _initializing = true;
    private bool _reallyClosing;
    private bool _isEmbedded;
    private bool _widgetHidden;
    private Rect _hiddenScreenRect;
    private bool _desktopAttachFailed;
    private ScaleTransform _uiScaleTransform = new(1, 1);
    private bool _updatingAppearance;
    private bool _updatingTimerModeTabs;
    private bool _updatingFocusChoices;
    private bool _deleteArmed;
    private bool _isDragging;
    private bool _dragMoved;
    private Native.POINT _dragStartCursor;
    private Native.RECT _dragStartRect;
    private DateTime _lastClockDate = DateTime.Today;
    private Action? _undoAction;

    private TaskItem? _editingTask;
    private string _allFilter = "pending";
    private bool _taskTrayOpen;
    private DateTime _calendarMonth;
    private DateTime _selectedDate;

    public MainWindow()
    {
        InitializeComponent();

        _data = Store.Load();
        _data.Settings.AutoStart = StartupManager.IsEnabled();

        ThemeManager.Apply(_data.Settings);
        Shell.Background = ThemeManager.CreateShellBrush(_data.Settings.CardOpacity);
        RootGrid.LayoutTransform = new ScaleTransform(_data.Settings.UiScale, _data.Settings.UiScale);

        Q1List.ItemsSource = _q1;
        Q2List.ItemsSource = _q2;
        Q3List.ItemsSource = _q3;
        Q4List.ItemsSource = _q4;
        AgendaList.ItemsSource = _agenda;
        AllTasksList.ItemsSource = _allTasks;
        TrayTasksList.ItemsSource = _trayTasks;

        _calendarMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        _selectedDate = DateTime.Today;
        SetInitialWindowBounds();

        _saveTimer.Tick += (_, _) => SaveNow();
        _toastTimer.Tick += (_, _) => FadeOutToast();
        _deleteResetTimer.Tick += (_, _) => ResetDeleteConfirmation();
        _clockTimer.Tick += ClockTimer_Tick;
        _deleteResetTimer.Stop();
        _toastTimer.Stop();

        ApplySettingsToUi();
        InitializePomodoro();
        RefreshAll();

        SourceInitialized += Window_SourceInitialized;
        PreviewKeyDown += Window_PreviewKeyDown;
        Closing += Window_Closing;

        if (System.Windows.Application.Current != null)
            System.Windows.Application.Current.SessionEnding += (_, _) =>
            {
                _reallyClosing = true;
                SaveNow();
            };
    }

    // ───────────────────────── 生命周期 ─────────────────────────

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        _hwndSource = HwndSource.FromHwnd(_hwnd);
        _hwndSource?.AddHook(WndProc);
        _hotkeyRegistered = Native.RegisterHotKey(_hwnd, HotkeyId, ModControl | ModAlt, 0x4D);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Native.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            ToggleWidgetVisibility();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _initializing = true;
        SetInitialWindowBounds();
        ApplyDesktopMode(showToast: false);
        var startupSizeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        startupSizeTimer.Tick += (_, _) =>
        {
            startupSizeTimer.Stop();
            if (_reallyClosing) return;
            Width = 420 * Math.Clamp(_data.Settings.UiScale, 0.9, 1.2);
            Height = 700 * Math.Clamp(_data.Settings.UiScale, 0.9, 1.2);
        };
        startupSizeTimer.Start();
        if (_data.Settings.AutoStart) StartupManager.SetEnabled(true);
        SetupTray();
        RestorePomodoroTimer();
        RefreshAll();
        _initializing = false;
        _clockTimer.Start();
        UpdateTrayMenu();

        if (_desktopAttachFailed)
        {
            ShowToast("未能嵌入桌面壁纸层，已切换为悬浮模式", null, 5);
        }
        else if (!_data.HasLaunched)
        {
            _data.HasLaunched = true;
            QueueSave();
            ShowToast("桌面组件已就位 · 右键卡片可以新建任务", null, 4.5);
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!_reallyClosing)
        {
            e.Cancel = true;
            HideWidget(true);
            return;
        }

        _clockTimer.Stop();
        StopTimerTicker();
        SaveNow();
        DisposeTray();
        if (_hotkeyRegistered) Native.UnregisterHotKey(_hwnd, HotkeyId);
        _hwndSource?.RemoveHook(WndProc);
    }

    // ───────────────────────── 位置与桌面嵌入 ─────────────────────────

    private void SetInitialWindowBounds()
    {
        var work = SystemParameters.WorkArea;
        var scale = Math.Clamp(_data.Settings.UiScale, 0.9, 1.2);
        var desiredWidth = 420 * scale;
        var desiredHeight = 700 * scale;
        Width = desiredWidth;
        Height = Math.Max(MinHeight, Math.Min(desiredHeight, work.Height - 24));

        var left = _data.Settings.WindowLeft ?? work.Right - Width - 24;
        var top = _data.Settings.WindowTop ?? work.Top + (work.Height - Height) / 2;

        var virtualLeft = SystemParameters.VirtualScreenLeft;
        var virtualTop = SystemParameters.VirtualScreenTop;
        var virtualRight = virtualLeft + SystemParameters.VirtualScreenWidth;
        var virtualBottom = virtualTop + SystemParameters.VirtualScreenHeight;

        Left = Math.Clamp(left, virtualLeft + 8, Math.Max(virtualLeft + 8, virtualRight - Width - 8));
        Top = Math.Clamp(top, virtualTop + 8, Math.Max(virtualTop + 8, virtualBottom - Height - 8));
    }

    private void ApplyTheme(bool refresh)
    {
        ThemeManager.Apply(_data.Settings);
        Shell.Background = ThemeManager.CreateShellBrush(_data.Settings.CardOpacity);
        if (refresh)
        {
            RefreshAll();
            UpdateTimerUi();
        }
    }

    private void UpdateShellBackground()
    {
        Shell.Background = ThemeManager.CreateShellBrush(_data.Settings.CardOpacity);
    }

    private void ApplyUiScale(bool resizeWindow)
    {
        var scale = Math.Clamp(_data.Settings.UiScale, 0.9, 1.2);
        _uiScaleTransform.ScaleX = scale;
        _uiScaleTransform.ScaleY = scale;
        RootGrid.LayoutTransform = _uiScaleTransform;

        if (!resizeWindow) return;

        var dpi = VisualTreeHelper.GetDpi(this);
        var screenRect = _desktopHost.GetScreenRect();
        if (screenRect == Rect.Empty)
            screenRect = new Rect(Left, Top, ActualWidth, ActualHeight);

        var newWidth = 420 * scale;
        var newHeight = Math.Max(MinHeight, Math.Min(700 * scale, SystemParameters.WorkArea.Height - 24));
        var newWidthPx = newWidth * dpi.DpiScaleX;
        var newHeightPx = newHeight * dpi.DpiScaleY;

        var centerX = screenRect.Left + screenRect.Width / 2;
        var centerY = screenRect.Top + screenRect.Height / 2;
        var left = (int)Math.Round(centerX - newWidthPx / 2);
        var top = (int)Math.Round(centerY - newHeightPx / 2);

        var screen = WinForms.Screen.FromPoint(new Drawing.Point((int)centerX, (int)centerY));
        var work = screen.WorkingArea;
        left = Math.Clamp(left, work.Left, Math.Max(work.Left, work.Right - (int)newWidthPx));
        top = Math.Clamp(top, work.Top, Math.Max(work.Top, work.Bottom - (int)newHeightPx));

        Width = newWidth;
        Height = newHeight;
        if (_desktopHost.IsAttached)
        {
            _desktopHost.MoveToScreen(left, top, (int)newWidthPx, (int)newHeightPx);
        }
        else
        {
            Left = left / dpi.DpiScaleX;
            Top = top / dpi.DpiScaleY;
        }
        SaveWindowPosition();
    }

    private void ApplyDesktopMode(bool showToast)
    {
        var wantDesktop = _data.Settings.DesktopMode;
        if (wantDesktop)
        {
            Topmost = false;
            bool attached;
            try
            {
                attached = _desktopHost.Attach(this);
            }
            catch (Exception ex)
            {
                Store.Log("嵌入桌面失败。", ex);
                attached = false;
            }

            _isEmbedded = attached;
            if (!attached)
            {
                _data.Settings.DesktopMode = false;
                _desktopAttachFailed = true;
                _initializing = true;
                DesktopModeSwitch.IsChecked = false;
                _initializing = false;
                Topmost = _data.Settings.AlwaysOnTop;
                Activate();
                QueueSave();
                if (showToast) ShowToast("未能嵌入桌面，已切换为悬浮模式", null, 4);
            }
            else
            {
                Topmost = false;
            }
        }
        else
        {
            var rect = _desktopHost.GetScreenRect();
            if (_desktopHost.IsAttached) _desktopHost.Detach(this);
            _isEmbedded = false;
            if (rect != Rect.Empty)
            {
                _desktopHost.MoveToScreen((int)rect.Left, (int)rect.Top,
                    (int)rect.Width, (int)rect.Height);
            }
            Topmost = _data.Settings.AlwaysOnTop;
            if (IsVisible) Activate();
        }

        AlwaysOnTopSwitch.IsEnabled = !_isEmbedded;
        UpdateSettingsLabels();
        UpdateTrayMenu();
    }

    private bool SetDesktopMode(bool enabled, bool showToast)
    {
        if (_widgetHidden) ShowWidget(false);
        _data.Settings.DesktopMode = enabled;
        if (enabled) _data.Settings.AlwaysOnTop = false;
        _initializing = true;
        DesktopModeSwitch.IsChecked = enabled;
        AlwaysOnTopSwitch.IsChecked = _data.Settings.AlwaysOnTop;
        _initializing = false;
        ApplyDesktopMode(showToast);
        QueueSave();
        return _isEmbedded == enabled;
    }

    private void PositionWindowAtDefault()
    {
        var work = WinForms.Screen.PrimaryScreen?.WorkingArea ?? new Drawing.Rectangle(0, 0, 1920, 1080);
        var dpi = VisualTreeHelper.GetDpi(this);
        var width = (int)Math.Round(Width * dpi.DpiScaleX);
        var height = (int)Math.Round(Height * dpi.DpiScaleY);
        var left = work.Right - width - 24;
        var top = work.Top + Math.Max(12, (work.Height - height) / 2);
        if (_desktopHost.IsAttached)
        {
            _desktopHost.MoveToScreen(left, top, width, height);
        }
        else
        {
            Left = left / dpi.DpiScaleX;
            Top = top / dpi.DpiScaleY;
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsInteractiveSource(e.OriginalSource)) return;
        if (_hwnd == IntPtr.Zero) return;
        if (!Native.GetCursorPos(out _dragStartCursor)) return;
        if (!Native.GetWindowRect(_hwnd, out _dragStartRect)) return;
        _isDragging = true;
        _dragMoved = false;
        HeaderArea.CaptureMouse();
        e.Handled = true;
    }

    private void Header_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || e.LeftButton != MouseButtonState.Pressed) return;
        if (!Native.GetCursorPos(out var point)) return;
        var dx = point.X - _dragStartCursor.X;
        var dy = point.Y - _dragStartCursor.Y;
        if (!_dragMoved && Math.Abs(dx) + Math.Abs(dy) < 3) return;
        _dragMoved = true;
        _desktopHost.MoveToScreen(_dragStartRect.Left + dx, _dragStartRect.Top + dy,
            _dragStartRect.Width, _dragStartRect.Height);
    }

    private void Header_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        HeaderArea.ReleaseMouseCapture();
        if (_dragMoved)
        {
            SnapToScreenEdge();
            SaveWindowPosition();
        }
        e.Handled = true;
    }

    private void SnapToScreenEdge()
    {
        var rect = _desktopHost.GetScreenRect();
        if (rect == Rect.Empty) return;
        var center = new Drawing.Point((int)(rect.Left + rect.Width / 2), (int)(rect.Top + rect.Height / 2));
        var work = WinForms.Screen.FromPoint(center).WorkingArea;
        const int margin = 10;
        const int snap = 22;
        var left = (int)rect.Left;
        var top = (int)rect.Top;

        if (Math.Abs(left - work.Left) <= snap) left = work.Left + margin;
        if (Math.Abs(work.Right - (left + rect.Width)) <= snap) left = work.Right - (int)rect.Width - margin;
        if (Math.Abs(top - work.Top) <= snap) top = work.Top + margin;
        if (Math.Abs(work.Bottom - (top + rect.Height)) <= snap) top = work.Bottom - (int)rect.Height - margin;

        left = Math.Clamp(left, work.Left, Math.Max(work.Left, work.Right - (int)rect.Width));
        top = Math.Clamp(top, work.Top, Math.Max(work.Top, work.Bottom - (int)rect.Height));

        if (left != (int)rect.Left || top != (int)rect.Top)
        {
            _desktopHost.MoveToScreen(left, top, (int)rect.Width, (int)rect.Height);
        }
    }

    private void SaveWindowPosition()
    {
        var rect = _desktopHost.GetScreenRect();
        if (rect == Rect.Empty) return;
        var dpi = VisualTreeHelper.GetDpi(this);
        _data.Settings.WindowLeft = rect.Left / dpi.DpiScaleX;
        _data.Settings.WindowTop = rect.Top / dpi.DpiScaleY;
        QueueSave();
    }

    private static bool IsInteractiveSource(object source)
    {
        var current = source as DependencyObject;
        while (current != null)
        {
            if (current is ButtonBase || current is TextBoxBase || current is ComboBox || current is CheckBox)
                return true;
            if (current is FrameworkContentElement content) current = content.Parent;
            else if (current is Visual || current is System.Windows.Media.Media3D.Visual3D)
                current = VisualTreeHelper.GetParent(current);
            else current = LogicalTreeHelper.GetParent(current);
        }
        return false;
    }

    // ───────────────────────── 显示 / 隐藏 ─────────────────────────

    private void ToggleWidgetVisibility()
    {
        if (_widgetHidden) ShowWidget(true);
        else HideWidget(true);
    }

    private void HideWidget(bool showBalloon)
    {
        if (_widgetHidden) return;
        SaveNow();

        var rect = _desktopHost.GetScreenRect();
        if (rect == Rect.Empty)
        {
            rect = new Rect(Left, Top, ActualWidth, ActualHeight);
        }
        _hiddenScreenRect = rect;
        _widgetHidden = true;

        // 不用 Window.Hide()：桌面壁纸层的分层窗口在隐藏/显示后可能丢失渲染表面。
        // 把窗口移到虚拟桌面之外，内容仍在渲染，呼出时再移回原位。
        var width = Math.Max(1, (int)rect.Width);
        var height = Math.Max(1, (int)rect.Height);
        _desktopHost.MoveToScreen(-30000, -30000, width, height);

        if (showBalloon && _tray != null)
        {
            _tray.ShowBalloonTip(2500, "云笺仍在运行", "双击托盘图标，或按 Ctrl+Alt+M 重新呼出。",
                WinForms.ToolTipIcon.None);
        }
    }

    private void ShowWidget(bool activate)
    {
        if (!IsVisible) Show();
        if (_widgetHidden)
        {
            _widgetHidden = false;
            var rect = _hiddenScreenRect;
            if (rect != Rect.Empty)
            {
                _desktopHost.MoveToScreen((int)rect.Left, (int)rect.Top,
                    (int)rect.Width, (int)rect.Height);
            }
            else
            {
                PositionWindowAtDefault();
            }
        }

        if (_isEmbedded)
        {
            _desktopHost.BringToFront();
            if (activate) Activate();
        }
        else if (activate)
        {
            Activate();
            Topmost = _data.Settings.AlwaysOnTop;
        }
    }

    private void HideButton_Click(object sender, RoutedEventArgs e) => HideWidget(false);

    // ───────────────────────── 托盘 ─────────────────────────

    private void SetupTray()
    {
        if (_tray != null) return;
        try
        {
            using var stream = System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/Assets/app.ico"))?.Stream;
            if (stream != null)
            {
                using var icon = new Drawing.Icon(stream);
                _trayIcon = (Drawing.Icon)icon.Clone();
            }
        }
        catch (Exception ex)
        {
            Store.Log("加载托盘图标失败。", ex);
        }

        var menu = new WinForms.ContextMenuStrip
        {
            BackColor = Drawing.Color.FromArgb(26, 34, 48),
            ForeColor = Drawing.Color.FromArgb(220, 232, 243),
            ShowImageMargin = false,
            ShowCheckMargin = false,
            Font = new Drawing.Font("Microsoft YaHei UI", 9f),
            Renderer = new DarkMenuRenderer()
        };

        var showItem = new WinForms.ToolStripMenuItem("显示云笺");
        showItem.Click += (_, _) => Dispatcher.Invoke(() => ShowWidget(true));
        var hideItem = new WinForms.ToolStripMenuItem("隐藏到托盘");
        hideItem.Click += (_, _) => Dispatcher.Invoke(() => HideWidget(false));
        _trayDesktopItem = new WinForms.ToolStripMenuItem("固定到桌面");
        _trayDesktopItem.Click += (_, _) => Dispatcher.Invoke(() => SetDesktopMode(!_isEmbedded, true));
        _trayTopItem = new WinForms.ToolStripMenuItem("窗口置顶");
        _trayTopItem.Click += (_, _) => Dispatcher.Invoke(() =>
        {
            _data.Settings.AlwaysOnTop = !_data.Settings.AlwaysOnTop;
            if (!_isEmbedded) Topmost = _data.Settings.AlwaysOnTop;
            UpdateSettingsLabels();
            QueueSave();
        });
        _trayAutoStartItem = new WinForms.ToolStripMenuItem("开机自启");
        _trayAutoStartItem.Click += (_, _) => Dispatcher.Invoke(() =>
        {
            var target = !StartupManager.IsEnabled();
            if (StartupManager.SetEnabled(target))
            {
                _data.Settings.AutoStart = target;
                _initializing = true;
                AutoStartSwitch.IsChecked = target;
                _initializing = false;
                QueueSave();
            }
            else ShowToast("设置开机自启失败", null, 3);
        });

        menu.Items.Add(showItem);
        menu.Items.Add(hideItem);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(_trayDesktopItem);
        menu.Items.Add(_trayTopItem);
        menu.Items.Add(_trayAutoStartItem);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        var dataItem = new WinForms.ToolStripMenuItem("打开数据文件夹");
        dataItem.Click += (_, _) => Dispatcher.Invoke(OpenDataFolder);
        menu.Items.Add(dataItem);
        var exitItem = new WinForms.ToolStripMenuItem("退出云笺");
        exitItem.Click += (_, _) => Dispatcher.Invoke(ExitApplication);
        menu.Items.Add(exitItem);
        menu.Opening += (_, _) => UpdateTrayMenu();

        _trayMenu = menu;
        _tray = new WinForms.NotifyIcon
        {
            Icon = _trayIcon ?? Drawing.SystemIcons.Application,
            Text = "云笺 · 桌面备忘录",
            Visible = true,
            ContextMenuStrip = menu
        };
        _tray.DoubleClick += (_, _) => Dispatcher.Invoke(() => ShowWidget(true));
        UpdateTrayMenu();
    }

    private void UpdateTrayMenu()
    {
        if (_tray == null) return;
        var pending = _data.Tasks.Count(t => !t.IsDone);
        _tray.Text = pending > 0 ? $"云笺 · {pending} 项待办" : "云笺 · 桌面备忘录";
        if (_trayDesktopItem != null)
            _trayDesktopItem.Text = _isEmbedded ? "✓ 固定在桌面" : "固定到桌面";
        if (_trayTopItem != null)
        {
            _trayTopItem.Enabled = !_isEmbedded;
            _trayTopItem.Text = _data.Settings.AlwaysOnTop ? "✓ 窗口置顶" : "窗口置顶";
        }
        if (_trayAutoStartItem != null)
            _trayAutoStartItem.Text = _data.Settings.AutoStart ? "✓ 开机自启" : "开机自启";
    }

    private void DisposeTray()
    {
        if (_tray != null)
        {
            _tray.Visible = false;
            _tray.Dispose();
            _tray = null;
        }
        _trayMenu?.Dispose();
        _trayMenu = null;
        _trayIcon?.Dispose();
        _trayIcon = null;
    }

    private sealed class DarkMenuRenderer : WinForms.ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColors()) { }
        protected override void OnRenderToolStripBorder(WinForms.ToolStripRenderEventArgs e)
        {
            using var pen = new Drawing.Pen(Drawing.Color.FromArgb(70, 255, 255, 255));
            var rect = new Drawing.Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            e.Graphics.DrawRectangle(pen, rect);
        }
    }

    private sealed class DarkColors : WinForms.ProfessionalColorTable
    {
        private static readonly Drawing.Color Background = Drawing.Color.FromArgb(26, 34, 48);
        private static readonly Drawing.Color Hover = Drawing.Color.FromArgb(44, 55, 72);
        public override Drawing.Color ToolStripDropDownBackground => Background;
        public override Drawing.Color MenuItemSelected => Hover;
        public override Drawing.Color MenuItemBorder => Drawing.Color.FromArgb(80, 110, 231, 216);
        public override Drawing.Color MenuBorder => Drawing.Color.FromArgb(70, 255, 255, 255);
        public override Drawing.Color ImageMarginGradientBegin => Background;
        public override Drawing.Color ImageMarginGradientMiddle => Background;
        public override Drawing.Color ImageMarginGradientEnd => Background;
        public override Drawing.Color SeparatorDark => Drawing.Color.FromArgb(36, 255, 255, 255);
        public override Drawing.Color SeparatorLight => Drawing.Color.FromArgb(20, 255, 255, 255);
    }

    // ───────────────────────── 设置 ─────────────────────────

    private void ApplySettingsToUi()
    {
        _initializing = true;
        _updatingAppearance = true;
        DesktopModeSwitch.IsChecked = _data.Settings.DesktopMode;
        AlwaysOnTopSwitch.IsChecked = _data.Settings.AlwaysOnTop;
        AutoStartSwitch.IsChecked = _data.Settings.AutoStart;
        SoundSwitch.IsChecked = _data.Settings.SoundEnabled;
        AutoNextSwitch.IsChecked = _data.Settings.AutoStartNext;

        ThemeDarkTab.IsChecked = _data.Settings.Theme != "Light";
        ThemeLightTab.IsChecked = _data.Settings.Theme == "Light";
        foreach (var swatch in AccentSwatchesPanel.Children.OfType<RadioButton>())
            swatch.IsChecked = string.Equals(swatch.Tag as string, _data.Settings.AccentColor,
                StringComparison.OrdinalIgnoreCase);
        OpacitySlider.Value = Math.Round(_data.Settings.CardOpacity * 100);
        OpacityValueText.Text = $"{OpacitySlider.Value:0}%";
        foreach (var item in ScaleCombo.Items.OfType<ComboBoxItem>())
        {
            if (double.TryParse(item.Tag?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture,
                    out var scale) && Math.Abs(scale - _data.Settings.UiScale) < 0.001)
            {
                ScaleCombo.SelectedItem = item;
                break;
            }
        }
        if (ScaleCombo.SelectedItem == null)
            ScaleCombo.SelectedItem = ScaleCombo.Items.OfType<ComboBoxItem>().FirstOrDefault();

        FocusMinutesBox.Text = _data.Settings.FocusMinutes.ToString();
        ShortBreakBox.Text = _data.Settings.ShortBreakMinutes.ToString();
        LongBreakBox.Text = _data.Settings.LongBreakMinutes.ToString();
        AlwaysOnTopSwitch.IsEnabled = !_data.Settings.DesktopMode;
        DataPathText.Text = AppPaths.DataDirectory;
        DataPathText.ToolTip = AppPaths.DataDirectory;
        VersionText.Text = $"云笺 v1.1 · 本地优先 · 数据保存于 {AppPaths.DataDirectory}";
        _updatingAppearance = false;
        _initializing = false;
        UpdateSettingsLabels();
    }

    private void Theme_Checked(object sender, RoutedEventArgs e)
    {
        if (_initializing || _updatingAppearance ||
            sender is not RadioButton { IsChecked: true, Tag: string theme }) return;
        if (theme != "Light" && theme != "Dark") return;
        if (_data.Settings.Theme == theme) return;
        _data.Settings.Theme = theme;
        ApplyTheme(true);
        QueueSave();
    }

    private void Accent_Checked(object sender, RoutedEventArgs e)
    {
        if (_initializing || _updatingAppearance ||
            sender is not RadioButton { IsChecked: true, Tag: string accent }) return;
        if (_data.Settings.AccentColor.Equals(accent, StringComparison.OrdinalIgnoreCase)) return;
        _data.Settings.AccentColor = accent;
        ApplyTheme(true);
        QueueSave();
    }

    private void Opacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_initializing || _updatingAppearance) return;
        var value = Math.Round(OpacitySlider.Value);
        OpacityValueText.Text = $"{value:0}%";
        _data.Settings.CardOpacity = value / 100.0;
        UpdateShellBackground();
        QueueSave();
    }

    private void Scale_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing || _updatingAppearance || ScaleCombo.SelectedItem is not ComboBoxItem item) return;
        if (!double.TryParse(item.Tag?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture,
                out var scale)) return;
        if (Math.Abs(_data.Settings.UiScale - scale) < 0.001) return;
        _data.Settings.UiScale = scale;
        ApplyUiScale(true);
        QueueSave();
    }

    private void UpdateSettingsLabels()
    {
        ContextDesktopModeItem.Header = _isEmbedded ? "切换到悬浮模式" : "固定到桌面";
        ContextTopmostItem.Header = _data.Settings.AlwaysOnTop ? "取消窗口置顶" : "窗口置顶";
        ContextTopmostItem.IsEnabled = !_isEmbedded;
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        ShowOverlay(SettingsOverlay);
    }

    private void CloseSettings_Click(object sender, RoutedEventArgs e)
    {
        HideOverlay(SettingsOverlay);
    }

    private void DesktopMode_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        var enabled = DesktopModeSwitch.IsChecked == true;
        var ok = SetDesktopMode(enabled, false);
        if (enabled && !ok) ShowToast("无法嵌入桌面，请尝试关闭后再开启", null, 3.5);
        else ShowToast(enabled ? "已固定在桌面壁纸层" : "已切换为悬浮窗口", null, 2.2);
    }

    private void AlwaysOnTop_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        _data.Settings.AlwaysOnTop = AlwaysOnTopSwitch.IsChecked == true;
        if (!_isEmbedded) Topmost = _data.Settings.AlwaysOnTop;
        UpdateSettingsLabels();
        UpdateTrayMenu();
        QueueSave();
    }

    private void AutoStart_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        var enabled = AutoStartSwitch.IsChecked == true;
        if (!StartupManager.SetEnabled(enabled))
        {
            _initializing = true;
            AutoStartSwitch.IsChecked = !enabled;
            _initializing = false;
            ShowToast("设置开机自启失败，请检查权限", null, 3);
            return;
        }
        _data.Settings.AutoStart = enabled;
        UpdateTrayMenu();
        QueueSave();
        ShowToast(enabled ? "已设置为开机自启" : "已关闭开机自启", null, 2.2);
    }

    private void Sound_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        _data.Settings.SoundEnabled = SoundSwitch.IsChecked == true;
        QueueSave();
    }

    private void AutoNext_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        _data.Settings.AutoStartNext = AutoNextSwitch.IsChecked == true;
        QueueSave();
    }

    private void Duration_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        var focus = ParseDuration(FocusMinutesBox.Text, 25, 1, 180);
        var shortBreak = ParseDuration(ShortBreakBox.Text, 5, 1, 60);
        var longBreak = ParseDuration(LongBreakBox.Text, 15, 1, 120);
        FocusMinutesBox.Text = focus.ToString();
        ShortBreakBox.Text = shortBreak.ToString();
        LongBreakBox.Text = longBreak.ToString();
        if (_data.Settings.FocusMinutes == focus &&
            _data.Settings.ShortBreakMinutes == shortBreak &&
            _data.Settings.LongBreakMinutes == longBreak) return;

        _data.Settings.FocusMinutes = focus;
        _data.Settings.ShortBreakMinutes = shortBreak;
        _data.Settings.LongBreakMinutes = longBreak;
        ApplyDurationChanges();
        QueueSave();
    }

    private static int ParseDuration(string? text, int fallback, int min, int max)
        => int.TryParse(text, out var value) ? Math.Clamp(value, min, max) : fallback;

    private void OpenDataFolder_Click(object sender, RoutedEventArgs e) => OpenDataFolder();

    private void OpenDataFolder()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataDirectory);
            Process.Start(new ProcessStartInfo("explorer.exe", AppPaths.DataDirectory) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Store.Log("打开数据目录失败。", ex);
            ShowToast("无法打开数据文件夹", null, 3);
        }
    }

    private void ResetPosition_Click(object sender, RoutedEventArgs e)
    {
        _data.Settings.WindowLeft = null;
        _data.Settings.WindowTop = null;
        PositionWindowAtDefault();
        QueueSave();
        ShowToast("窗口位置已重置", null, 2.2);
    }

    private void ExitApp_Click(object sender, RoutedEventArgs e) => ExitApplication();

    private void ExitApplication()
    {
        _reallyClosing = true;
        SaveNow();
        Close();
        System.Windows.Application.Current.Shutdown();
    }

    private void OverlayBackdrop_Click(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource != sender) return;
        if (ReferenceEquals(sender, AllTasksOverlay)) HideOverlay(AllTasksOverlay);
        else if (ReferenceEquals(sender, TaskEditor)) CloseTaskEditor();
        else if (ReferenceEquals(sender, SettingsOverlay)) HideOverlay(SettingsOverlay);
    }

    private void OverlayCard_Click(object sender, MouseButtonEventArgs e) => e.Handled = true;

    private void ShowOverlay(FrameworkElement overlay)
    {
        overlay.Visibility = Visibility.Visible;
        overlay.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)));
    }

    private void HideOverlay(FrameworkElement overlay)
    {
        overlay.Visibility = Visibility.Collapsed;
        overlay.BeginAnimation(OpacityProperty, null);
        overlay.Opacity = 1;
    }

    private bool AnyOverlayOpen =>
        TaskEditor.Visibility == Visibility.Visible ||
        AllTasksOverlay.Visibility == Visibility.Visible ||
        SettingsOverlay.Visibility == Visibility.Visible;

    // ───────────────────────── 导航 ─────────────────────────

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (_initializing || sender is not RadioButton { IsChecked: true } radio) return;
        var target = radio.Tag as string;
        QuadrantView.Visibility = target == "quadrant" ? Visibility.Visible : Visibility.Collapsed;
        CalendarView.Visibility = target == "calendar" ? Visibility.Visible : Visibility.Collapsed;
        PomodoroView.Visibility = target == "pomodoro" ? Visibility.Visible : Visibility.Collapsed;

        var view = target switch
        {
            "calendar" => (FrameworkElement)CalendarView,
            "pomodoro" => PomodoroView,
            _ => QuadrantView
        };
        view.BeginAnimation(OpacityProperty, null);
        view.Opacity = 1;

        if (target == "calendar") RefreshCalendar();
        else if (target == "pomodoro")
        {
            RefreshFocusChoices();
            UpdateTimerUi();
        }
        else RefreshQuadrants();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (TaskEditor.Visibility == Visibility.Visible) { CloseTaskEditor(); e.Handled = true; return; }
            if (AllTasksOverlay.Visibility == Visibility.Visible) { HideOverlay(AllTasksOverlay); e.Handled = true; return; }
            if (SettingsOverlay.Visibility == Visibility.Visible) { HideOverlay(SettingsOverlay); e.Handled = true; return; }
        }

        if (_initializing || TaskEditor.Visibility == Visibility.Visible) return;

        var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        if (ctrl && e.Key == Key.N)
        {
            OpenTaskEditor(null);
            e.Handled = true;
        }
        else if (ctrl && e.Key == Key.D1) SelectNav(NavQuadrant);
        else if (ctrl && e.Key == Key.D2) SelectNav(NavCalendar);
        else if (ctrl && e.Key == Key.D3) SelectNav(NavPomodoro);
        else if (e.Key == Key.Space && PomodoroView.Visibility == Visibility.Visible && !AnyOverlayOpen)
        {
            if (Keyboard.FocusedElement is TextBoxBase or ComboBox) return;
            ToggleTimer();
            e.Handled = true;
        }
    }

    private void SelectNav(RadioButton tab)
    {
        tab.IsChecked = true;
        if (tab == NavQuadrant) RefreshQuadrants();
        else if (tab == NavCalendar) RefreshCalendar();
        else
        {
            RefreshFocusChoices();
            UpdateTimerUi();
        }
    }

    // ───────────────────────── 顶栏与刷新 ─────────────────────────

    private void ClockTimer_Tick(object? sender, EventArgs e)
    {
        UpdateHeaderClock();
        if (DateTime.Today != _lastClockDate)
        {
            _lastClockDate = DateTime.Today;
            _selectedDate = DateTime.Today;
            _calendarMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            RefreshAll();
            SaveNow();
        }
    }

    private void UpdateHeaderClock()
    {
        var now = DateTime.Now;
        HeaderDateText.Text = now.ToString("M月d日 · dddd");
        HeaderClockText.Text = now.ToString("HH:mm");
    }

    private void RefreshAll()
    {
        RefreshHeader();
        RefreshQuadrants();
        RefreshCalendar();
        RefreshAllTasks();
        RefreshFocusChoices();
        RefreshPomodoroStats();
        UpdateTrayMenu();
        UpdateSettingsLabels();
    }

    private void RefreshHeader()
    {
        var now = DateTime.Now;
        HeaderDateText.Text = now.ToString("M月d日 · dddd");
        HeaderClockText.Text = now.ToString("HH:mm");

        var pending = _data.Tasks.Where(t => !t.IsDone).ToList();
        var todayPending = pending.Where(t => t.DueAt?.Date == DateTime.Today).ToList();
        var overdue = pending.Count(t => t.IsOverdue);
        var completedToday = _data.Tasks.Count(t => t.IsDone && t.CompletedAt?.Date == DateTime.Today);

        HeaderSummaryText.Text = todayPending.Count == 0
            ? "今天没有待办截止"
            : $"今天还有 {todayPending.Count} 件事";
        if (overdue > 0) HeaderSummaryText.Text += $" · {overdue} 项已逾期";
        HeaderProgressText.Text = $"今日完成 {completedToday} · 专注 {GetTodayFocusMinutes()} 分钟";

        var withDue = pending.Where(t => t.DueAt.HasValue).OrderBy(t => t.DueAt).ToList();
        var next = withDue.FirstOrDefault();
        if (next == null)
        {
            NextDueText.Text = pending.Count > 0 ? "待办未设截止时间" : "今天没有截止任务";
        }
        else
        {
            var title = Shorten(next.Title, 10);
            NextDueText.Text = next.IsOverdue
                ? $"已逾期 · {title}"
                : $"{next.DueShort} · {title}";
        }
        NextDueText.ToolTip = next?.DueFull;

        CompletedCountText.Text = $"今日完成 {completedToday}";
        UpdateTrayMenu();
    }

    private static string Shorten(string text, int max)
    {
        if (string.IsNullOrWhiteSpace(text)) return "未命名";
        return text.Length <= max ? text : text[..max] + "…";
    }

    private void QueueSave()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void SaveNow()
    {
        _saveTimer.Stop();
        CaptureTimerSnapshot();
        var ok = Store.Save(_data);
        SaveStatusText.Text = ok
            ? $"已保存 · {DateTime.Now:HH:mm}"
            : "保存失败 · 请检查磁盘";
        if (!ok) ShowToast("数据保存失败，请检查磁盘空间", null, 4);
    }

    // ───────────────────────── 轻提示 ─────────────────────────

    private void ShowToast(string message, Action? undo = null, double seconds = 3.6)
    {
        _undoAction = undo;
        ToastText.Text = message;
        ToastUndoButton.Visibility = undo != null ? Visibility.Visible : Visibility.Collapsed;
        ToastBar.IsHitTestVisible = undo != null;
        ToastBar.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(130)));
        _toastTimer.Stop();
        _toastTimer.Interval = TimeSpan.FromSeconds(seconds);
        _toastTimer.Start();
    }

    private void FadeOutToast()
    {
        _toastTimer.Stop();
        var animation = new DoubleAnimation(ToastBar.Opacity, 0, TimeSpan.FromMilliseconds(220));
        animation.Completed += (_, _) =>
        {
            ToastBar.IsHitTestVisible = false;
            ToastBar.BeginAnimation(OpacityProperty, null);
            ToastBar.Opacity = 0;
            _undoAction = null;
            ToastUndoButton.Visibility = Visibility.Collapsed;
        };
        ToastBar.BeginAnimation(OpacityProperty, animation);
    }

    private void ToastUndo_Click(object sender, RoutedEventArgs e)
    {
        var action = _undoAction;
        _undoAction = null;
        _toastTimer.Stop();
        FadeOutToast();
        action?.Invoke();
    }

    // ───────────────────────── 右键菜单 ─────────────────────────

    private void ContextNewTask_Click(object sender, RoutedEventArgs e) => OpenTaskEditor(null);
    private void ContextAllTasks_Click(object sender, RoutedEventArgs e) => ShowAllTasks();
    private void ContextDesktopMode_Click(object sender, RoutedEventArgs e) => SetDesktopMode(!_isEmbedded, true);
    private void ContextTopmost_Click(object sender, RoutedEventArgs e)
    {
        if (_isEmbedded) return;
        _data.Settings.AlwaysOnTop = !_data.Settings.AlwaysOnTop;
        Topmost = _data.Settings.AlwaysOnTop;
        _initializing = true;
        AlwaysOnTopSwitch.IsChecked = _data.Settings.AlwaysOnTop;
        _initializing = false;
        UpdateSettingsLabels();
        QueueSave();
    }
    private void ContextHide_Click(object sender, RoutedEventArgs e) => HideWidget(false);
    private void ContextExit_Click(object sender, RoutedEventArgs e) => ExitApplication();
}
