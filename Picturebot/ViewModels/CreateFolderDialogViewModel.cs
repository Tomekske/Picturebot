using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Database.Domain.Entities;
using Graph.Domain.Interfaces;
using Picturebot.Views;

namespace Picturebot.ViewModels;

public partial class CreateFolderDialogViewModel : ViewModelBase {
    private readonly IFolderService _folderService;
    private readonly Action<Folder?> _onResult;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateFolderCommand))]
    private string _folderName = string.Empty;

    [ObservableProperty]
    private LocationItem? _selectedParent;

    public CreateFolderDialogViewModel(IFolderService folderService, Action<Folder?> onResult,
        List<Folder> existingFolders) {
        _folderService = folderService;
        _onResult = onResult;

        Parents.Add(new LocationItem { Name = "Library", Id = null });
        var folderMap = existingFolders.ToDictionary(f => f.Id);
        foreach (var f in existingFolders) {
            Parents.Add(new LocationItem { Name = LocationItem.GetFolderPath(f, folderMap), Id = f.Id });
        }

        SelectedParent = Parents[0];
    }

    public ObservableCollection<LocationItem> Parents { get; } = new();

    [RelayCommand(CanExecute = nameof(CanCreateFolder))]
    private async Task CreateFolderAsync() {
        try {
            var createdFolder = await _folderService.CreateAsync(SelectedParent?.Id, FolderName);
            _onResult(createdFolder);
            MainWindow.DialogManager.DismissDialog();
        } catch (Exception ex) {
            // Handle error (e.g., duplicate name)
            Console.WriteLine(ex.Message);
        }
    }

    private bool CanCreateFolder() {
        return !string.IsNullOrWhiteSpace(FolderName);
    }

    [RelayCommand]
    private void Cancel() {
        _onResult(null);
        MainWindow.DialogManager.DismissDialog();
    }
}
