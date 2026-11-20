using System.Windows;
using System.Windows.Media;

namespace YunJianMemo;

public static class ThemeManager
{
    public static bool IsLight { get; private set; }
    public static Color AccentColor { get; private set; } = ColorFromHex("#6EE7D8");

    public static SolidColorBrush AccentBrush => Get("AccentBrush");
    public static SolidColorBrush AccentTextBrush => Get("AccentTextBrush");
    public static SolidColorBrush AccentOnBrush => Get("AccentOnBrush");

    public static void Apply(AppSettings settings)
    {
        IsLight = settings.Theme == "Light";
        AccentColor = ColorFromHex(settings.AccentColor);

        var onAccent = RelativeLuminance(AccentColor) > 0.58
            ? ColorFromHex("#07130F")
            : ColorFromHex("#F7FFFC");

        Set("AccentBrush", Opaque(AccentColor));
        Set("AccentDeepBrush", Blend(AccentColor, Colors.Black, 0.16));
        Set("AccentHoverBrush", Blend(AccentColor, Colors.White, 0.16));
        Set("AccentPressedBrush", Blend(AccentColor, Colors.Black, 0.14));
        Set("AccentSoftBrush", WithAlpha(AccentColor, IsLight ? 0.16 : 0.14));
        Set("AccentSoftStrongBrush", WithAlpha(AccentColor, IsLight ? 0.25 : 0.18));
        Set("AccentHoverSoftBrush", WithAlpha(AccentColor, IsLight ? 0.32 : 0.24));
        Set("AccentBorderBrush", WithAlpha(AccentColor, IsLight ? 0.70 : 0.60));
        Set("AccentSelectionBrush", WithAlpha(AccentColor, 0.45));
        Set("AccentTextBrush", IsLight
            ? Blend(AccentColor, Colors.Black, 0.34)
            : Blend(AccentColor, Colors.White, 0.38));
        Set("AccentOnBrush", onAccent);
        Set("CalendarSelectedBrush", WithAlpha(AccentColor, IsLight ? 0.28 : 0.26));
        Set("CalendarSelectedBorderBrush", WithAlpha(AccentColor, IsLight ? 0.80 : 0.62));
        Set("CalendarTodayBorderBrush", WithAlpha(AccentColor, IsLight ? 0.55 : 0.40));
        Set("Q2BorderBrush", WithAlpha(ColorFromHex("#6EE7D8"), IsLight ? 0.24 : 0.14));

        if (IsLight) ApplyLight();
        else ApplyDark();

        Set("DangerBrush", Get("CoralBrush").Color);
        Set("PanelBrush", Get("CardBackgroundBrush").Color);
        Set("PanelBorderBrush", Get("CardBorderBrush").Color);
        Set("DarkSurfaceBrush", Get("OverlayPanelBrush").Color);
    }

    private static void ApplyDark()
    {
        Set("TextBrush", "#F1F6FB");
        Set("SubTextBrush", "#9AA7BA");
        Set("MutedBrush", "#68778D");
        Set("TextFaintBrush", "#4F5D71");
        Set("TaskDoneBrush", "#6A7688");
        Set("ShellBorderBrush", "#26FFFFFF");

        Set("CardBackgroundBrush", "#0CFFFFFF");
        Set("CardBorderBrush", "#1FFFFFFF");
        Set("SurfaceBackgroundBrush", "#0BFFFFFF");
        Set("SurfaceBorderBrush", "#14FFFFFF");
        Set("SurfaceHoverBrush", "#16FFFFFF");
        Set("SurfacePressedBrush", "#24FFFFFF");
        Set("SurfaceStrongBorderBrush", "#26FFFFFF");
        Set("SurfaceHoverBorderBrush", "#3AFFFFFF");
        Set("StrongBorderBrush", "#33FFFFFF");

        Set("OverlayBackdropBrush", "#B0060A10");
        Set("OverlayPanelBrush", "#FC151D2A");
        Set("OverlayBorderBrush", "#2AFFFFFF");
        Set("PopupBackgroundBrush", "#FA1A2230");
        Set("MenuBackgroundBrush", "#FA1A2230");
        Set("MenuBorderBrush", "#33FFFFFF");
        Set("MenuHoverBrush", "#14FFFFFF");
        Set("MenuTextBrush", "#CBD6E5");
        Set("TooltipBackgroundBrush", "#FA1B2432");

        Set("InputBrush", "#12FFFFFF");
        Set("InputBorderBrush", "#26FFFFFF");
        Set("InputFocusBackgroundBrush", "#18FFFFFF");

        Set("ToastBackgroundBrush", "#F5232D3D");
        Set("ToastBorderBrush", "#2EFFFFFF");
        Set("ToastTextBrush", "#DFE8F3");

        Set("ScrollThumbBrush", "#33FFFFFF");
        Set("CalendarHoverBrush", "#12FFFFFF");
        Set("CalendarDayTextBrush", "#A4B3C6");
        Set("CalendarDayMutedBrush", "#5A6A80");
        Set("ProgressTrackBrush", "#1AFFFFFF");
        Set("SwitchTrackBrush", "#26FFFFFF");
        Set("SwitchThumbOffBrush", "#AEBACB");
        Set("TaskCheckBorderBrush", "#55FFFFFF");
        Set("SessionDotOffBrush", "#26FFFFFF");

        Set("Q1TitleBrush", "#F0DEDE");
        Set("Q1CountBrush", "#B67C7C");
        Set("Q2TitleBrush", "#D8EEEB");
        Set("Q2CountBrush", "#79B7AD");
        Set("Q3TitleBrush", "#EDE4D4");
        Set("Q3CountBrush", "#B09365");
        Set("Q4TitleBrush", "#D5DCE6");
        Set("Q4CountBrush", "#77869A");
        Set("Q1BorderBrush", "#20FF7A7A");
        Set("Q3BorderBrush", "#20FFC46B");
        Set("Q4BorderBrush", "#1FFFFFFF");
    }

    private static void ApplyLight()
    {
        Set("TextBrush", "#2B2F36");
        Set("SubTextBrush", "#5F6570");
        Set("MutedBrush", "#81858E");
        Set("TextFaintBrush", "#918B82");
        Set("TaskDoneBrush", "#9A958C");
        Set("ShellBorderBrush", "#33000000");

        Set("CardBackgroundBrush", "#B8FFFFFF");
        Set("CardBorderBrush", "#1A000000");
        Set("SurfaceBackgroundBrush", "#0D000000");
        Set("SurfaceBorderBrush", "#14000000");
        Set("SurfaceHoverBrush", "#10000000");
        Set("SurfacePressedBrush", "#1C000000");
        Set("SurfaceStrongBorderBrush", "#26000000");
        Set("SurfaceHoverBorderBrush", "#3A000000");
        Set("StrongBorderBrush", "#33000000");

        Set("OverlayBackdropBrush", "#4D000000");
        Set("OverlayPanelBrush", "#FAFCFAF7");
        Set("OverlayBorderBrush", "#1A000000");
        Set("PopupBackgroundBrush", "#FDFBF7");
        Set("MenuBackgroundBrush", "#FDFBF7");
        Set("MenuBorderBrush", "#33000000");
        Set("MenuHoverBrush", "#0F000000");
        Set("MenuTextBrush", "#2B2F36");
        Set("TooltipBackgroundBrush", "#FDFBF7");

        Set("InputBrush", "#66FFFFFF");
        Set("InputBorderBrush", "#1F000000");
        Set("InputFocusBackgroundBrush", "#E6FFFFFF");

        Set("ToastBackgroundBrush", "#F5FFFFFF");
        Set("ToastBorderBrush", "#1A000000");
        Set("ToastTextBrush", "#2B2F36");

        Set("ScrollThumbBrush", "#33000000");
        Set("CalendarHoverBrush", "#10000000");
        Set("CalendarDayTextBrush", "#5F6570");
        Set("CalendarDayMutedBrush", "#B0A99E");
        Set("ProgressTrackBrush", "#16000000");
        Set("SwitchTrackBrush", "#1A000000");
        Set("SwitchThumbOffBrush", "#8C8B86");
        Set("TaskCheckBorderBrush", "#33000000");
        Set("SessionDotOffBrush", "#22000000");

        Set("Q1TitleBrush", "#A44040");
        Set("Q1CountBrush", "#B56A6A");
        Set("Q2TitleBrush", "#1F6F66");
        Set("Q2CountBrush", "#4E948B");
        Set("Q3TitleBrush", "#8A651B");
        Set("Q3CountBrush", "#A88643");
        Set("Q4TitleBrush", "#5A6372");
        Set("Q4CountBrush", "#7A8492");
        Set("Q1BorderBrush", "#36FF7A7A");
        Set("Q3BorderBrush", "#36FFC46B");
        Set("Q4BorderBrush", "#1A000000");
    }

    public static LinearGradientBrush CreateShellBrush(double opacity)
    {
        var alpha = (byte)Math.Round(255 * Math.Clamp(opacity, 0.65, 1.0) * 0.97);
        Color[] colors = IsLight
            ? new[]
            {
                Color.FromArgb(alpha, 250, 246, 238),
                Color.FromArgb(alpha, 243, 237, 227),
                Color.FromArgb(alpha, 237, 230, 219)
            }
            : new[]
            {
                Color.FromArgb(alpha, 23, 32, 48),
                Color.FromArgb(alpha, 14, 20, 31),
                Color.FromArgb(alpha, 11, 16, 24)
            };

        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1)
        };
        brush.GradientStops.Add(new GradientStop(colors[0], 0));
        brush.GradientStops.Add(new GradientStop(colors[1], 0.55));
        brush.GradientStops.Add(new GradientStop(colors[2], 1));
        brush.Freeze();
        return brush;
    }

    public static SolidColorBrush Get(string key)
    {
        if (Application.Current.Resources[key] is SolidColorBrush brush) return brush;
        var created = new SolidColorBrush(Colors.Transparent);
        Application.Current.Resources[key] = created;
        return created;
    }

    public static void Set(string key, string hex) => Set(key, ColorFromHex(hex));

    public static void Set(string key, Color color)
    {
        var resources = Application.Current.Resources;
        if (resources[key] is SolidColorBrush brush && !brush.IsFrozen)
        {
            brush.Color = color;
        }
        else
        {
            resources[key] = new SolidColorBrush(color);
        }
    }

    public static Color ColorFromHex(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return ColorFromHex("#6EE7D8");
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch
        {
            return Color.FromRgb(0x6E, 0xE7, 0xD8);
        }
    }

    private static Color Opaque(Color color) => Color.FromArgb(255, color.R, color.G, color.B);

    private static Color WithAlpha(Color color, double alpha)
        => Color.FromArgb((byte)Math.Round(255 * Math.Clamp(alpha, 0, 1)), color.R, color.G, color.B);

    private static Color Blend(Color a, Color b, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        return Color.FromArgb(
            a.A,
            (byte)Math.Round(a.R + (b.R - a.R) * amount),
            (byte)Math.Round(a.G + (b.G - a.G) * amount),
            (byte)Math.Round(a.B + (b.B - a.B) * amount));
    }

    private static double RelativeLuminance(Color color)
    {
        static double Channel(byte value)
        {
            var c = value / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
    }
}
