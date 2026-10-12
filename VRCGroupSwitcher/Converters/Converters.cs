using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace VRCGroupSwitcher.Converters
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; } = false;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool val = value switch
            {
                bool b => b,
                string s => !string.IsNullOrWhiteSpace(s),
                _ => value != null
            };
            if (Invert) val = !val;
            return val ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Visibility v && v == Visibility.Visible;
        }
    }

    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b) return !b;
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b) return !b;
            return true;
        }
    }

    public class NullOrEmptyToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; } = false;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool empty = value == null || (value is string s && string.IsNullOrWhiteSpace(s));
            bool show = Invert ? empty : !empty;
            return show ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StatusColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string status = value?.ToString()?.ToLower() ?? "";
            return status switch
            {
                "join me" => new SolidColorBrush(Color.FromRgb(56, 189, 248)),  // 青 (Sky: #38BDF8)
                "active" => new SolidColorBrush(Color.FromRgb(74, 222, 128)),   // 緑 (Green: #4ADE80)
                "ask me" => new SolidColorBrush(Color.FromRgb(251, 146, 60)),   // オレンジ (Orange: #FB923C)
                "busy" => new SolidColorBrush(Color.FromRgb(248, 113, 113)),     // 赤 (Red: #F87171)
                _ => new SolidColorBrush(Color.FromRgb(148, 163, 184))          // グレー (#94A3B8)
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StatusTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string status = value?.ToString()?.ToLower() ?? "";
            return status switch
            {
                "join me" => "だれでもおいで",
                "active" => "オンライン",
                "ask me" => "きいてみてね",
                "busy" => "取り込み中",
                "unchanged" => "変更しない (維持)",
                _ => string.IsNullOrEmpty(status) ? "(未設定)" : status
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
