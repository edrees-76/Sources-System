using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LiveChartsCore;
using LiveChartsCore.Drawing;
using LiveChartsCore.Kernel;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView.Drawing;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.SKCharts;
using LiveChartsCore.VisualElements;

namespace Sources.Helpers;

/// <summary>
/// تلميح ذكي للرسوم البيانية يحسب الاتجاه الأمثل ديناميكياً (Auto-Flip / Smart Positioning)
/// ويمنع اقتطاع التلميح من أي حافة (يمين، يسار، أعلى، أسفل) عبر احتساب المساحات المتبقية وضبط الحدود بدقة
/// </summary>
public class AutoFlipChartTooltip : IChartTooltip<SkiaSharpDrawingContext>
{
    private readonly SKDefaultTooltip _inner = new();
    private static readonly FieldInfo? PanelField = typeof(SKDefaultTooltip)
        .GetField("_panel", BindingFlags.NonPublic | BindingFlags.Instance);

    public IPaint<SkiaSharpDrawingContext>? FontPaint
    {
        get => _inner.FontPaint;
        set => _inner.FontPaint = value;
    }

    public IPaint<SkiaSharpDrawingContext>? BackgroundPaint
    {
        get => _inner.BackgroundPaint;
        set => _inner.BackgroundPaint = value;
    }

    public double TextSize
    {
        get => _inner.TextSize;
        set => _inner.TextSize = value;
    }

    public void Show(IEnumerable<ChartPoint> foundPoints, Chart<SkiaSharpDrawingContext> chart)
    {
        _inner.Show(foundPoints, chart);

        var panel = PanelField?.GetValue(_inner) as VisualElement<SkiaSharpDrawingContext>;
        if (panel == null) return;

        var size = panel.Measure(chart);
        double tooltipWidth = size.Width;
        double tooltipHeight = size.Height;

        double controlWidth = chart.ControlSize.Width;
        double controlHeight = chart.ControlSize.Height;

        if (controlWidth <= 0 || controlHeight <= 0 || tooltipWidth <= 0 || tooltipHeight <= 0) return;

        var firstPoint = foundPoints.FirstOrDefault();
        double targetX = panel.X + (tooltipWidth / 2.0);
        double targetY = panel.Y + (tooltipHeight / 2.0);

        if (firstPoint?.Context?.HoverArea != null)
        {
            try
            {
                dynamic h = firstPoint.Context.HoverArea;
                targetX = (double)h.X + ((double)h.Width / 2.0);
                targetY = (double)h.Y + ((double)h.Height / 2.0);
            }
            catch
            {
                // Fallback to panel midpoint
            }
        }

        var (optimalX, optimalY) = CalculateOptimalPosition(
            targetX, targetY, tooltipWidth, tooltipHeight, controlWidth, controlHeight);

        panel.X = optimalX;
        panel.Y = optimalY;
    }

    public void Hide(Chart<SkiaSharpDrawingContext> chart)
    {
        _inner.Hide(chart);
    }

    /// <summary>
    /// حساب الموضع الأمثل للتلميح مع عكس الاتجاه تلقائياً (Auto-Flip) وضمان عدم خروجه عن حدود الكانفاس
    /// </summary>
    public static (double X, double Y) CalculateOptimalPosition(
        double targetX,
        double targetY,
        double tooltipWidth,
        double tooltipHeight,
        double controlWidth,
        double controlHeight,
        double offset = 12.0,
        double margin = 6.0)
    {
        // حساب المسافات المتاحة حول النقطة إلى حواف البطاقة الأربعة
        double spaceLeft = targetX;
        double spaceRight = controlWidth - targetX;
        double spaceTop = targetY;
        double spaceBottom = controlHeight - targetY;

        double calculatedX;
        double calculatedY;

        // في الرسوم الأفقية (RowSeries): الأفضلية الأساسية هي العرض لليمين إن كفت المساحة، وإلا لليسار
        if (spaceRight >= tooltipWidth + offset)
        {
            // مساحة كافية على اليمين -> إظهار إلى اليمين
            calculatedX = targetX + offset;
            calculatedY = targetY - (tooltipHeight / 2.0);
        }
        else if (spaceLeft >= tooltipWidth + offset)
        {
            // مساحة كافية على اليسار -> عكس الاتجاه لليسار (Auto-Flip)
            calculatedX = targetX - tooltipWidth - offset;
            calculatedY = targetY - (tooltipHeight / 2.0);
        }
        else if (spaceTop >= tooltipHeight + offset)
        {
            // مساحة كافية في الأعلى
            calculatedX = targetX - (tooltipWidth / 2.0);
            calculatedY = targetY - tooltipHeight - offset;
        }
        else if (spaceBottom >= tooltipHeight + offset)
        {
            // مساحة كافية في الأسفل
            calculatedX = targetX - (tooltipWidth / 2.0);
            calculatedY = targetY + offset;
        }
        else
        {
            // إذا كانت جميع الجهات ضيقة، نختار الجهة ذات المساحة الأكبر
            if (spaceRight >= spaceLeft && spaceRight >= spaceTop && spaceRight >= spaceBottom)
            {
                calculatedX = targetX + offset;
                calculatedY = targetY - (tooltipHeight / 2.0);
            }
            else if (spaceLeft >= spaceRight && spaceLeft >= spaceTop && spaceLeft >= spaceBottom)
            {
                calculatedX = targetX - tooltipWidth - offset;
                calculatedY = targetY - (tooltipHeight / 2.0);
            }
            else if (spaceTop >= spaceBottom)
            {
                calculatedX = targetX - (tooltipWidth / 2.0);
                calculatedY = targetY - tooltipHeight - offset;
            }
            else
            {
                calculatedX = targetX - (tooltipWidth / 2.0);
                calculatedY = targetY + offset;
            }
        }

        // ─── الحماية النهائية الصارمة (Clamping): ضمان عدم خروج أي جزء من الـ Tooltip عن حدود الكانفاس ───
        double minX = margin;
        double maxX = Math.Max(margin, controlWidth - tooltipWidth - margin);
        double minY = margin;
        double maxY = Math.Max(margin, controlHeight - tooltipHeight - margin);

        double clampedX = Math.Max(minX, Math.Min(maxX, calculatedX));
        double clampedY = Math.Max(minY, Math.Min(maxY, calculatedY));

        return (clampedX, clampedY);
    }
}
