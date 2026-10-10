using StoreManager.Services.Messaging;

namespace StoreManager.Tests.Services;

public class MessagingTests
{
    [Fact]
    public void Desktop_link_url_encodes_text_including_devanagari_and_rupee()
    {
        var uri = WhatsAppLinks.Desktop("+919876543210", "Namaste Aarti ji, ₹1,500 बाकी है & please pay");
        Assert.Equal("whatsapp", uri.Scheme);
        Assert.StartsWith("whatsapp://send?phone=919876543210&text=", uri.OriginalString);
        var text = uri.OriginalString.Split("&text=")[1];
        Assert.DoesNotContain(" ", text);
        Assert.DoesNotContain("&", text);
        Assert.Equal("Namaste Aarti ji, ₹1,500 बाकी है & please pay", Uri.UnescapeDataString(text));
    }

    [Fact]
    public void Web_link_uses_wa_me()
    {
        var uri = WhatsAppLinks.Web("+919876543210", "Hi there");
        Assert.Equal("https://wa.me/919876543210?text=Hi%20there", uri.OriginalString);
    }

    [Fact]
    public void Template_replaces_known_placeholders_case_insensitively_and_leaves_unknown()
    {
        var s = TemplateRenderer.Render("Namaste {Name} ji, from {store}. {oops}",
            new Dictionary<string, string> { ["name"] = "Aarti", ["STORE"] = "Shree Sarees" });
        Assert.Equal("Namaste Aarti ji, from Shree Sarees. {oops}", s);
    }
}
