using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Graph.Domain.Interfaces;
using Main.Views;
using Serilog;
using SukiUI.Dialogs;
using SukiUI.Toasts;

namespace Main.ViewModels;

public partial class GalleryViewModel : ViewModelBase {
    private readonly IFolderService _folderService;

    public GalleryViewModel(IFolderService folderService) {
        _folderService = folderService;
    }

    [RelayCommand]
    public async Task OpenCreateFolderDialogAsync() {
        var folders = await _folderService.FindAllAsync();

        // 2. Initialize the ViewModel
        var vm = new CreateFolderDialogViewModel(_folderService, result => {
            if (result != null) {
                // Refresh logic here
                Log.Information("Folder created: {result}", result.Name);
                RefreshGallery();

                MainWindow.ToastManager.CreateToast()
                    .WithTitle("Success")
                    .WithContent($"Folder '{result.Name}' has been created.")
                    .Dismiss().After(TimeSpan.FromSeconds(3))
                    .Queue();
            }
        }, folders);

        // 3. Trigger SukiDialog
        MainWindow.DialogManager.CreateDialog()
            .WithContent(new CreateFolderDialog { DataContext = vm })
            .TryShow();
    }

    private void RefreshGallery() {
        // Implement gallery refresh logic
    }
}
