using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using StoreManager.ViewModels.Infrastructure;

namespace StoreManager.App.Views;

/// <summary>Placeholder for modules that arrive in later build steps.</summary>
public sealed partial class ComingSoonPage : Page
{
    public ComingSoonPage() => InitializeComponent();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        var loc = App.Services.GetRequiredService<ILocalizer>();
        var tag = e.Parameter as string ?? "";
        TitleText.Text = loc.Get("Nav_Title_" + tag);
        BodyText.Text = loc.Get("ComingSoon_" + tag);
    }
}
