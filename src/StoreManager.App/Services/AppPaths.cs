namespace StoreManager.App.Services;

/// <summary>All data lives under %LOCALAPPDATA%\StoreManager unless STOREMANAGER_DATA overrides it (used for testing).</summary>
public sealed class AppPaths
{
    public AppPaths()
    {
        DataDir = Environment.GetEnvironmentVariable("STOREMANAGER_DATA")
                  ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StoreManager");
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(LogDir);
    }

    public string DataDir { get; }
    public string DatabasePath => Path.Combine(DataDir, "store.db");
    public string LogDir => Path.Combine(DataDir, "logs");
}
