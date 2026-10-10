using StoreManager.ViewModels.Infrastructure;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace StoreManager.App.Services;

public sealed class FileDialogs : IFileDialogs
{
    public async Task<PickedFile?> PickOpenAsync(IReadOnlyList<string> extensions, CancellationToken ct = default)
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
        foreach (var ext in extensions) picker.FileTypeFilter.Add(ext);

        var file = await picker.PickSingleFileAsync();
        if (file is null) return null;

        // Copy to memory so the readers get a seekable stream regardless of the file provider.
        var ms = new MemoryStream();
        await using (var src = await file.OpenStreamForReadAsync())
            await src.CopyToAsync(ms, ct);
        ms.Position = 0;
        return new PickedFile(file.Name, ms);
    }

    public async Task<Stream?> PickSaveAsync(string suggestedName, string extension, CancellationToken ct = default)
    {
        var picker = new FileSavePicker { SuggestedFileName = suggestedName };
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
        picker.FileTypeChoices.Add("Excel workbook", new List<string> { extension });

        var file = await picker.PickSaveFileAsync();
        if (file is null) return null;

        var stream = await file.OpenStreamForWriteAsync();
        stream.SetLength(0);
        return stream;
    }
}
