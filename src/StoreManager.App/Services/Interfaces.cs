namespace StoreManager.App.Services;

/// <summary>Implemented by pages that respond to Ctrl+N.</summary>
public interface INewItemPage
{
    Task NewItemAsync();
}

/// <summary>Implemented by pages that respond to F5.</summary>
public interface IRefreshablePage
{
    Task RefreshAsync();
}
