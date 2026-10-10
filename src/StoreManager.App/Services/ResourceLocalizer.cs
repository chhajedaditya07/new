using Microsoft.Windows.ApplicationModel.Resources;
using StoreManager.ViewModels.Infrastructure;

namespace StoreManager.App.Services;

/// <summary>Reads strings from Strings/&lt;language&gt;/Resources.resw. A missing key shows the key itself, never a blank.</summary>
public sealed class ResourceLocalizer : ILocalizer
{
    private readonly ResourceLoader? _loader;

    public ResourceLocalizer()
    {
        try { _loader = new ResourceLoader(); }
        catch { _loader = null; }
    }

    public string Get(string key)
    {
        try
        {
            var s = _loader?.GetString(key);
            return string.IsNullOrEmpty(s) ? key : s;
        }
        catch { return key; }
    }
}
