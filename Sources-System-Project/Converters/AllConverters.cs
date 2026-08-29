using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.IO;
using System.Windows.Controls;

namespace Sources.Converters;

// ─── تحويل القيمة المنطقية إلى ظهور/إخفاء ───
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Visible;
}

public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Collapsed;
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is not true;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is not true;
}

// ─── تحويل القيمة المنطقية إلى قيمتين مختلفتين (نصوص غالباً) ───
public class BoolToValueConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (parameter is not string paramString) return null;
        var parts = paramString.Split('|');
        if (parts.Length < 2) return null;

        return value is true ? parts[0] : parts[1];
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

// ─── تحويل الحالة إلى لون ───
public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString() switch
        {
            "InUse" or "Active" => new SolidColorBrush(Color.FromRgb(63, 174, 122)),       // أخضر تشغيلي
            "Storage" => new SolidColorBrush(Color.FromRgb(79, 127, 163)),                 // أزرق تخزين
            "Waste" => new SolidColorBrush(Color.FromRgb(224, 169, 62)),                   // كهرماني/نفايات
            "Transfer" => new SolidColorBrush(Color.FromRgb(224, 169, 62)),                // كهرماني/نقل
            _ => new SolidColorBrush(Color.FromRgb(158, 158, 158))
        };
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

// ─── تحويل الحالة إلى نص عربي ───
public class StatusToArabicConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string? key = value?.ToString() switch
        {
            "InUse" or "Active" => "StatusInUse",
            "Storage" => "StatusStorage",
            "Waste" => "StatusWaste",
            "Transfer" => "StatusTransfer",
            _ => null
        };

        if (key != null && Application.Current.Resources.Contains(key))
            return Application.Current.FindResource(key);

        return value?.ToString() ?? "";
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

// ─── تحويل نوع المعاملة إلى عربي ───
public class TransactionTypeToArabicConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string? key = value?.ToString() switch
        {
            "Transfer" => "TransTransfer",
            "Receive" => "TransReceive",
            "Dispose" => "TransDispose",
            "Return" => "TransReturn",
            _ => null
        };

        if (key != null && Application.Current.Resources.Contains(key))
            return Application.Current.FindResource(key);

        return value?.ToString() ?? "";
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

// ─── تحويل نوع الموقع إلى نص مترجم ───
public class LocationTypeToDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string? key = value?.ToString() switch
        {
            "Lab" => "LocationTypeLab",
            "Storage" => "LocationTypeStorage",
            "Hospital" => "LocationTypeHospital",
            "Clinic" => "LocationTypeClinic",
            _ => null
        };

        if (key != null && Application.Current.Resources.Contains(key))
            return Application.Current.FindResource(key);

        return value?.ToString() ?? "";
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

// ─── تنسيق الأرقام العلمية ───
public class ScientificNotationConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double d)
        {
            if (d == 0) return "0";
            if (Math.Abs(d) >= 1e6 || Math.Abs(d) < 0.01)
                return d.ToString("0.####E+0");
            return d.ToString("N4");
        }
        return value?.ToString() ?? "";
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

// ─── BindingProxy للربط من DataGrid ───
public class BindingProxy : Freezable
{
    protected override Freezable CreateInstanceCore() => new BindingProxy();
    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register("Data", typeof(object), typeof(BindingProxy), new UIPropertyMetadata(null));
    public object Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }
}
// ─── تحويل حالة رؤية كلمة المرور إلى أيقونة العين ───
public class PasswordEyeIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? "EyeOff" : "Eye";
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

// ─── تحويل اسم الواجهة إلى عنوان عربي ───
public class ViewNameToTitleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string? key = value?.ToString() switch
        {
            "Dashboard" => "NavDashboard",
            "Radioisotopes" => "NavRadioisotopes",
            "Sources" => "NavSources",
            "Locations" => "NavLocations",
            "Borrowing" => "NavBorrowing",
            "Transactions" => "NavTransactions",
            "Reports" => "NavReports",
            "Alerts" => "MenuAlerts",
            "Users" => "NavUsers",
            "ActivityCalculator" => "NavActivityCalculator",
            "Help" => "NavHelp",
            "AboutSystem" => "NavAboutSystem",
            "Settings" => "NavSettings",
            "Deletions" => "NavDeletions",
            _ => null
        };

        if (key != null && Application.Current.Resources.Contains(key))
            return Application.Current.FindResource(key);

        return "Sources";
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

// ─── تحويل القيمة الفارغة (Null أو الصفر) إلى ظهور/إخفاء ───
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isNullOrEmpty = value == null 
            || (value is string s && string.IsNullOrWhiteSpace(s))
            || (value is int i && i == 0);
        bool inverse = parameter?.ToString() == "Inverse";

        if (inverse)
            return isNullOrEmpty ? Visibility.Visible : Visibility.Collapsed;
        
        return isNullOrEmpty ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class InverseNullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isNullOrEmpty = value == null 
            || (value is string s && string.IsNullOrWhiteSpace(s))
            || (value is int i && i == 0);
        return isNullOrEmpty ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

// ─── تحويل المسار النسبي إلى مسار مطلق ───
public class RelativePathToAbsolutePathConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            return null;

        string relativePath = value.ToString()!;
        if (Path.IsPathRooted(relativePath))
            return relativePath;

        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
// ─── تحويل المساواة إلى ظهور/إخفاء (لخطوات المعالج) ───
public class EqualityToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return Visibility.Collapsed;
        
        bool isEqual = value.ToString() == parameter.ToString();
        return isEqual ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

// ─── محول الفهرس (Index Converter) لترقيم الأسطر ───
public class IndexConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DataGridRow row)
        {
            var dataGrid = ItemsControl.ItemsControlFromItemContainer(row) as DataGrid;
            if (dataGrid == null)
            {
                DependencyObject? parent = VisualTreeHelper.GetParent(row);
                while (parent != null && parent is not DataGrid)
                {
                    parent = VisualTreeHelper.GetParent(parent);
                }
                dataGrid = parent as DataGrid;
            }

            if (dataGrid != null && row.Item != null && row.Item != CollectionView.NewItemPlaceholder)
            {
                int index = dataGrid.Items.IndexOf(row.Item);
                if (index >= 0)
                    return (index + 1).ToString();
            }

            int fallback = row.GetIndex();
            return fallback >= 0 ? (fallback + 1).ToString() : "1";
        }
        return "1";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

// ─── تحويل درجة الخطورة إلى لون ───
public class SeverityToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString() switch
        {
            "Critical" => new SolidColorBrush(Color.FromRgb(244, 67, 54)), // أحمر
            "Warning" => new SolidColorBrush(Color.FromRgb(255, 152, 0)),  // برتقالي
            "Info" => new SolidColorBrush(Color.FromRgb(33, 150, 243)),     // أزرق
            _ => new SolidColorBrush(Color.FromRgb(158, 158, 158))
        };
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

// ─── تحويل سلسلة Hex (#AARRGGBB | #RRGGBB) إلى SolidColorBrush ───
public class HexStringToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            var hex = value?.ToString();
            if (string.IsNullOrWhiteSpace(hex)) return new SolidColorBrush(Colors.Transparent);
            var c = (Color)ColorConverter.ConvertFromString(hex);
            return new SolidColorBrush(c);
        }
        catch
        {
            return new SolidColorBrush(Colors.Transparent);
        }
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

// ─── محول ترقيم الصفوف التلقائي (يدعم التنقل بين الصفحات Pagination) ───
public class RowIndexConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            int alternationIndex = 0;
            int currentPage = 1;
            int pageSize = 16;

            if (values.Length > 0 && values[0] != null && values[0] != DependencyProperty.UnsetValue)
            {
                if (int.TryParse(values[0].ToString(), out int parsedAlt))
                    alternationIndex = parsedAlt;
            }

            if (values.Length > 1 && values[1] != null && values[1] != DependencyProperty.UnsetValue)
            {
                if (int.TryParse(values[1].ToString(), out int parsedPage))
                    currentPage = parsedPage;
            }

            if (values.Length > 2 && values[2] != null && values[2] != DependencyProperty.UnsetValue)
            {
                if (int.TryParse(values[2].ToString(), out int parsedSize))
                    pageSize = parsedSize;
            }

            if (currentPage < 1) currentPage = 1;
            int globalIndex = (currentPage - 1) * pageSize + (alternationIndex + 1);
            return globalIndex.ToString();
        }
        catch
        {
            return "1";
        }
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

// ─── محول مفتاح المورد إلى نص مترجم ───
public class StringToResourceConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string key && !string.IsNullOrEmpty(key))
        {
            if (Application.Current != null && Application.Current.Resources.Contains(key))
                return Application.Current.FindResource(key);
            return key;
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

// ─── محول المساواة إلى قيمة منطقية (Equality to Boolean) ───
public class EqualityToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return false;
        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b && parameter != null)
            return parameter.ToString()!;
        return Binding.DoNothing;
    }
}

// ─── محول زيادة الفهرس بمقدار واحد (+1) ───
public class AddOneConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int i) return (i + 1).ToString();
        if (int.TryParse(value?.ToString(), out int parsed)) return (parsed + 1).ToString();
        return "1";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}


