using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Domain.Enums;
using Graph.Domain.Interfaces;
using Main.Messages;
using Serilog;

namespace Main.ViewModels;

public partial class DetailsInspectorViewModel : ViewModelBase, IRecipient<PictureSelectedMessage> {
    private readonly INodeService _nodeService;
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    private PictureItemViewModel? _selectedPicture;

    [ObservableProperty]
    private Bitmap? _previewImage;

    [ObservableProperty]
    private bool _isBusy;

    public DetailsInspectorViewModel(INodeService nodeService) {
        _nodeService = nodeService;
        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    public void Receive(PictureSelectedMessage message) {
        SelectedPicture = message.Value;
    }

    async partial void OnSelectedPictureChanged(PictureItemViewModel? value) {
        PreviewImage?.Dispose();
        PreviewImage = null;

        if (value == null) return;

        await LoadPreviewAsync(value);
    }

    private async Task LoadPreviewAsync(PictureItemViewModel picVm) {
        var previewPath = picVm.Picture.SubFolder?.Preview;
        if (string.IsNullOrEmpty(previewPath) || !File.Exists(previewPath)) {
            Log.Warning("No preview available for {Name}", picVm.Name);
            return;
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        try {
            var bitmap = await Task.Run(() => {
                using var stream = File.OpenRead(previewPath);
                // We decode to a reasonable width for the details pane, e.g., 600px
                return Bitmap.DecodeToWidth(stream, 600);
            }, _cts.Token);

            PreviewImage = bitmap;
        } catch (OperationCanceledException) {
            // Loading was cancelled
        } catch (Exception ex) {
            Log.Error(ex, "Failed to load preview for {Name} at {Path}", picVm.Name, previewPath);
        }
    }

    [RelayCommand]
    private async Task SetCurationStatus(CurationStatus status) {
        if (SelectedPicture == null) return;

        IsBusy = true;
        try {
            SelectedPicture.Picture.CurationStatus = status;
            SelectedPicture.CurationStatus = status; // Update the VM property to trigger UI update
            
            await _nodeService.UpdateNodeAsync(SelectedPicture.Picture);
            
            // Success indicator: Brief icon glow or toast notification?
            // For now, let's just assume the UI binding handles the "visual active state"
        } catch (Exception ex) {
            Log.Error(ex, "Failed to update curation status for {Name}", SelectedPicture.Name);
        } finally {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void EditMetadata() {
        // Trigger the metadata editing mode
        Log.Information("Edit metadata for {Name}", SelectedPicture?.Name);
    }

    [RelayCommand]
    private void DeleteAsset() {
        // Trigger the asset removal workflow
        Log.Information("Delete asset {Name}", SelectedPicture?.Name);
    }
}
