using StoreManager.Core.Formatting;

namespace StoreManager.Tests.Core;

public class MoneyTests
{
    [Theory]
    [InlineData(0, "₹0")]
    [InlineData(50000, "₹500")]
    [InlineData(12550, "₹125.50")]
    [InlineData(99900, "₹999")]
    [InlineData(100000, "₹1,000")]
    [InlineData(12500000, "₹1,25,000")]
    [InlineData(123456700, "₹12,34,567")]
    [InlineData(1000000000, "₹1,00,00,000")]
    [InlineData(-50000, "-₹500")]
    public void Formats_with_indian_grouping(long paise, string expected) =>
        Assert.Equal(expected, Money.Format(paise));

    [Fact]
    public void Can_force_paise() => Assert.Equal("₹500.00", Money.Format(50000, alwaysShowPaise: true));

    [Theory]
    [InlineData("1,25,000", 12500000)]
    [InlineData("₹ 500.50", 50050)]
    [InlineData("500", 50000)]
    [InlineData("0.5", 50)]
    public void Parses_user_input(string text, long expected)
    {
        Assert.True(Money.TryParseToPaise(text, out var paise));
        Assert.Equal(expected, paise);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("-5")]
    public void Rejects_bad_input(string text) => Assert.False(Money.TryParseToPaise(text, out _));
}

public class PhoneNumberTests
{
    [Theory]
    [InlineData("9876543210", "+919876543210")]
    [InlineData("98765 43210", "+919876543210")]
    [InlineData("09876543210", "+919876543210")]
    [InlineData("+91-98765-43210", "+919876543210")]
    [InlineData("919876543210", "+919876543210")]
    [InlineData("0091 98765 43210", "+919876543210")]
    [InlineData("+44 7911 123456", "+447911123456")]
    public void Normalizes(string raw, string expected)
    {
        Assert.True(PhoneNumber.TryNormalize(raw, out var n));
        Assert.Equal(expected, n);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("5876543210")]      // Indian mobiles start 6-9
    [InlineData("98765432101")]
    [InlineData("abc")]
    public void Rejects(string raw) => Assert.False(PhoneNumber.TryNormalize(raw, out _));

    [Fact]
    public void Display_and_whatsapp_digits()
    {
        Assert.Equal("+91 98765 43210", PhoneNumber.Display("+919876543210"));
        Assert.Equal("919876543210", PhoneNumber.ToWhatsAppDigits("+919876543210"));
    }
}

public class IstDateTests
{
    [Fact]
    public void Converts_utc_to_ist_across_midnight()
    {
        var utc = new DateTime(2026, 10, 9, 20, 0, 0, DateTimeKind.Utc);   // 01:30 next day in IST
        Assert.Equal(new DateTime(2026, 10, 10, 1, 30, 0), IstDate.ToIst(utc));
        Assert.Equal(new DateOnly(2026, 10, 10), IstDate.TodayIst(utc));
        Assert.Equal("10-10-2026 01:30", IstDate.DateTimeText(utc));
    }

    [Fact]
    public void Round_trips() =>
        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            IstDate.FromIst(IstDate.ToIst(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc))));

    [Theory]
    [InlineData("15-08-1990", 1990, 8, 15)]
    [InlineData("5/3/2001", 2001, 3, 5)]
    [InlineData("1990-08-15", 1990, 8, 15)]
    public void Parses_common_formats(string text, int y, int m, int d)
    {
        Assert.True(IstDate.TryParse(text, out var date));
        Assert.Equal(new DateOnly(y, m, d), date);
    }

    [Fact]
    public void Formats_dd_mm_yyyy() => Assert.Equal("05-03-2001", IstDate.Date(new DateOnly(2001, 3, 5)));
}
