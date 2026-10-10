using StoreManager.Core.Entities;
using StoreManager.Core.Validation;

namespace StoreManager.Tests.Core;

public class ContactValidatorTests
{
    private static Contact Valid() => new() { Name = "  Aarti Sharma ", Mobile = "98765 43210" };

    [Fact]
    public void Valid_contact_is_normalized()
    {
        var c = Valid();
        Assert.Empty(ContactValidator.ValidateAndNormalize(c));
        Assert.Equal("Aarti Sharma", c.Name);
        Assert.Equal("+919876543210", c.Mobile);
    }

    [Fact]
    public void Name_and_mobile_are_required()
    {
        var errors = ContactValidator.ValidateAndNormalize(new Contact());
        Assert.Contains(errors, e => e.Field == "Name" && e.Code == ContactValidator.Required);
        Assert.Contains(errors, e => e.Field == "Mobile" && e.Code == ContactValidator.Required);
    }

    [Fact]
    public void Bad_phone_numbers_are_flagged_per_field()
    {
        var c = Valid();
        c.Mobile = "123";
        c.WhatsApp = "nope";
        c.AltPhone = "9123456789";
        var errors = ContactValidator.ValidateAndNormalize(c);
        Assert.Contains(errors, e => e.Field == "Mobile" && e.Code == ContactValidator.Invalid);
        Assert.Contains(errors, e => e.Field == "WhatsApp");
        Assert.DoesNotContain(errors, e => e.Field == "AltPhone");
        Assert.Equal("+919123456789", c.AltPhone);
    }

    [Theory]
    [InlineData("24AAACC1206D1ZM", true)]
    [InlineData("24aaacc1206d1zm", true)]    // lower case is normalised
    [InlineData("24AAACC1206D1XM", false)]   // 14th char must be Z
    [InlineData("12345", false)]
    public void Gstin_format(string gstin, bool ok)
    {
        var c = Valid();
        c.Gstin = gstin;
        var errors = ContactValidator.ValidateAndNormalize(c);
        Assert.Equal(ok, errors.All(e => e.Field != "Gstin"));
    }

    [Fact]
    public void Blank_optionals_become_null()
    {
        var c = Valid();
        c.City = "  ";
        c.Gstin = "";
        c.WhatsApp = " ";
        ContactValidator.ValidateAndNormalize(c);
        Assert.Null(c.City);
        Assert.Null(c.Gstin);
        Assert.Null(c.WhatsApp);
    }
}
