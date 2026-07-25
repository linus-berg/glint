using System.Globalization;
using Avalonia.Data.Converters;
using Glint.Models;

namespace Glint.Controls;

/// <summary>
/// Converters for ToolType to boolean for button active states.
/// Used as x:Static references in XAML.
/// </summary>
public static class ToolTypeConverters
{
    public static readonly IValueConverter IsFreehand = new ToolTypeConverter(ToolType.Freehand);
    public static readonly IValueConverter IsArrow = new ToolTypeConverter(ToolType.Arrow);
    public static readonly IValueConverter IsText = new ToolTypeConverter(ToolType.Text);
    public static readonly IValueConverter IsBlur = new ToolTypeConverter(ToolType.Blur);
    public static readonly IValueConverter IsRedaction = new ToolTypeConverter(ToolType.Redaction);
    public static readonly IValueConverter IsLoupe = new ToolTypeConverter(ToolType.Loupe);
    public static readonly IValueConverter IsRectangle = new ToolTypeConverter(ToolType.Rectangle);
    public static readonly IValueConverter IsEllipse = new ToolTypeConverter(ToolType.Ellipse);
    public static readonly IValueConverter IsStep = new ToolTypeConverter(ToolType.Step);
    public static readonly IValueConverter IsHighlighter = new ToolTypeConverter(ToolType.Highlighter);
}

public class ToolTypeConverter : IValueConverter
{
    private readonly ToolType _target;

    public ToolTypeConverter(ToolType target)
    {
        _target = target;
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is ToolType tool && tool == _target;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return _target;
    }
}

/// <summary>
/// Converters for index-based selection (color picker, stroke picker).
/// </summary>
public static class IndexConverters
{
    public static readonly IValueConverter Is0 = new IndexConverter(0);
    public static readonly IValueConverter Is1 = new IndexConverter(1);
    public static readonly IValueConverter Is2 = new IndexConverter(2);
    public static readonly IValueConverter Is3 = new IndexConverter(3);
    public static readonly IValueConverter Is4 = new IndexConverter(4);
    public static readonly IValueConverter Is5 = new IndexConverter(5);
    public static readonly IValueConverter Is6 = new IndexConverter(6);
    public static readonly IValueConverter Is7 = new IndexConverter(7);
}

public class IndexConverter : IValueConverter
{
    private readonly int _target;

    public IndexConverter(int target)
    {
        _target = target;
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is int index && index == _target;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return _target;
    }
}
