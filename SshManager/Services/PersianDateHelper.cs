using System.Globalization;

namespace SshManager.Services;

public static class PersianDateHelper
{
    private static readonly PersianCalendar Calendar = new();

  private static readonly string[] PersianMonthNames =
    [
        "", "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور",
        "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند"
    ];

    private static readonly string[] PersianDayNames =
    [
        "یکشنبه", "دوشنبه", "سه‌شنبه", "چهارشنبه", "پنجشنبه", "جمعه", "شنبه"
    ];

    public static string FormatLong(DateTime date)
    {
        var year = Calendar.GetYear(date);
        var month = Calendar.GetMonth(date);
        var day = Calendar.GetDayOfMonth(date);
        var dayName = PersianDayNames[(int)date.DayOfWeek];
        return $"{dayName}، {ToPersianDigits(day)} {PersianMonthNames[month]} {ToPersianDigits(year)}";
    }

    public static string FormatShort(DateTime date)
    {
        var year = Calendar.GetYear(date);
        var month = Calendar.GetMonth(date);
        var day = Calendar.GetDayOfMonth(date);
        return $"{ToPersianDigits(year)}/{ToPersianDigits(month, 2)}/{ToPersianDigits(day, 2)}";
    }

    public static string ToPersianDigits(int value, int minWidth = 0)
    {
        var text = value.ToString(CultureInfo.InvariantCulture);
        if (minWidth > 0)
            text = value.ToString($"D{minWidth}", CultureInfo.InvariantCulture);

        return text
            .Replace('0', '۰').Replace('1', '۱').Replace('2', '۲').Replace('3', '۳')
            .Replace('4', '۴').Replace('5', '۵').Replace('6', '۶').Replace('7', '۷')
            .Replace('8', '۸').Replace('9', '۹');
    }
}
