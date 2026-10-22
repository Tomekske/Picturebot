using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Database.Domain.Entities;
using Domain.Enums;
using Domain.TimeZones;
using Graph.Domain.Interfaces;
using Picturebot.Services;
using Picturebot.Views;
using Serilog;
using SukiUI.Toasts;

namespace Picturebot.ViewModels;

public partial class EditNodeDialogViewModel : ViewModelBase {
    private readonly INodeService _nodeService;
    private readonly IFolderService _folderService;
    private readonly Node _nodeToEdit;
    private readonly IRecentLocationsService? _recentLocations;
    private readonly Action<Node?> _onResult;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _nodeName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimeZoneHint))]
    private string? _timeZoneId;

    // The zone used when this node has none: the nearest folder's above it, else the default
    [ObservableProperty]
    private string? _inheritedTimeZoneId;

    private TimeZoneSource? _inheritedTimeZone;
    private readonly string? _defaultTimeZoneId;

    public LocationPickerViewModel Picker { get; }

    public string Title => $"Edit {_nodeToEdit.Type}";

    public bool HasTimeZone => _nodeToEdit is Album or Folder;

    public string TimeZoneHint => ViewModels.TimeZoneHint.Describe(TimeZoneId, _inheritedTimeZone, _defaultTimeZoneId,
        isFolder: _nodeToEdit is Folder);

    public EditNodeDialogViewModel(
        INodeService nodeService,
        IFolderService folderService,
        Node nodeToEdit,
        List<Node> allNodes,
        Action<Node?> onResult,
        string? defaultTimeZoneId = null,
        IRecentLocationsService? recentLocations = null) {
        _nodeService = nodeService;
        _folderService = folderService;
        _nodeToEdit = nodeToEdit;
        _recentLocations = recentLocations;
        _onResult = onResult;
        _defaultTimeZoneId = defaultTimeZoneId;

        _nodeName = nodeToEdit.Name;
        _timeZoneId = nodeToEdit switch {
            Album album => album.TimeZoneId,
            Folder folder => folder.TimeZoneId,
            _ => null
        };

        // A folder can't move into itself or into what it contains, so it and its subtree aren't offered.
        Picker = new LocationPickerViewModel(allNodes, recentLocations, nodeToEdit.ParentId,
            nodeToEdit is Folder ? nodeToEdit.Id : null);
        Picker.PropertyChanged += (_, e) => {
            if (e.PropertyName == nameof(LocationPickerViewModel.Current)) {
                _ = LoadInheritedTimeZoneAsync(Picker.NearestExistingId());
            }
        };
        _ = LoadInheritedTimeZoneAsync(Picker.NearestExistingId());
    }

    /// <summary>
    ///     Looks up what the node inherits under the selected parent (it changes when the node is moved).
    /// </summary>
    private async Task LoadInheritedTimeZoneAsync(int? parentId) {
        try {
            _inheritedTimeZone = parentId.HasValue ? await _nodeService.ResolveTimeZoneAsync(parentId.Value) : null;
        } catch (Exception ex) {
            Log.Warning(ex, "Could not resolve the inherited time zone of {Name}", _nodeToEdit.Name);
            _inheritedTimeZone = null;
        }
        InheritedTimeZoneId = ViewModels.TimeZoneHint.InheritedZoneId(_inheritedTimeZone, _defaultTimeZoneId);
        OnPropertyChanged(nameof(TimeZoneHint));
    }

    partial void OnTimeZoneIdChanged(string? value) => OnPropertyChanged(nameof(TimeZoneHint));

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync() {
        try {
            // Folders typed into the location picker are created first, outermost first.
            var parentId = await Picker.EnsurePathAsync(_folderService, new List<Folder>());
            if (parentId.HasValue) {
                _recentLocations?.Remember(parentId.Value);
            }

            _nodeToEdit.Name = NodeName;
            _nodeToEdit.ParentId = parentId;
            // The same zone as the inherited one is stored as "not set", so it keeps following the folder above
            var ownTimeZoneId = ViewModels.TimeZoneHint.Normalize(TimeZoneId, _inheritedTimeZone, _defaultTimeZoneId);
            switch (_nodeToEdit) {
                case Album album:
                    album.TimeZoneId = ownTimeZoneId;
                    break;
                case Folder folder:
                    folder.TimeZoneId = ownTimeZoneId;
                    break;
            }

            await _nodeService.UpdateNodeAsync(_nodeToEdit);
            
            _onResult(_nodeToEdit);
            MainWindow.DialogManager.DismissDialog();
            
            MainWindow.ToastManager.CreateToast()
                .WithTitle("Success")
                .WithContent($"{_nodeToEdit.Type} updated successfully.")
                .Dismiss().ByClicking()
                .Dismiss().After(TimeSpan.FromSeconds(3))
                .Queue();
        } catch (Exception ex) {
            Log.Error(ex, "Failed to update {Type}", _nodeToEdit.Type);
            MainWindow.ToastManager.CreateToast()
                .WithTitle("Error")
                .WithContent(ex.Message)
                .Dismiss().ByClicking()
                .Queue();
        }
    }

    private bool CanSave() {
        return !string.IsNullOrWhiteSpace(NodeName);
    }

    [RelayCommand]
    private void Cancel() {
        _onResult(null);
        MainWindow.DialogManager.DismissDialog();
    }
}
