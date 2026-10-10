using StoreManager.Core.Validation;

namespace StoreManager.ViewModels.Infrastructure;

/// <summary>Maps validation codes from the Core layer to resource keys, so every message is localized.</summary>
public static class Messages
{
    public static string KeyFor(ValidationError e) => (e.Field, e.Code) switch
    {
        ("Name", _) => "Err_NameRequired",
        ("Mobile", ContactValidator.Required) => "Err_MobileRequired",
        ("Mobile", "Duplicate") => "Err_MobileDuplicate",
        ("Mobile", _) => "Err_MobileInvalid",
        ("WhatsApp", _) or ("AltPhone", _) => "Err_PhoneInvalid",
        ("Gstin", _) => "Err_GstinInvalid",
        _ => "Err_Generic"
    };

    public static string TagError(string? code) => code switch
    {
        "Required" => "Err_TagRequired",
        "Invalid" => "Err_TagInvalid",
        "Duplicate" => "Err_TagDuplicate",
        "System" => "Err_TagSystem",
        "SameTag" => "Err_TagSame",
        "NotFound" => "Err_TagNotFound",
        _ => "Err_Generic"
    };
}
