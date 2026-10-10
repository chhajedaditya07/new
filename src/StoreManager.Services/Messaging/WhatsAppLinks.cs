using StoreManager.Core.Formatting;

namespace StoreManager.Services.Messaging;

/// <summary>
/// Builds official WhatsApp deep links only. The user always presses Send; nothing is sent automatically
/// and no unofficial API is used.
/// </summary>
public static class WhatsAppLinks
{
    /// <summary>whatsapp://send?phone=919876543210&amp;text=… (WhatsApp Desktop).</summary>
    public static Uri Desktop(string normalizedPhone, string text) =>
        new($"whatsapp://send?phone={PhoneNumber.ToWhatsAppDigits(normalizedPhone)}&text={Uri.EscapeDataString(text)}");

    /// <summary>https://wa.me/919876543210?text=… (browser fallback).</summary>
    public static Uri Web(string normalizedPhone, string text) =>
        new($"https://wa.me/{PhoneNumber.ToWhatsAppDigits(normalizedPhone)}?text={Uri.EscapeDataString(text)}");
}
