using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Domain.Models;

namespace Picturebot.ViewModels;

public partial class TagItemViewModel : ViewModelBase {
    private readonly Action<TagItemViewModel>? _onRequestDelete;
    private readonly Action<TagItemViewModel>? _onRenamed;

    public Tag Model { get; }

    public Guid Id => Model.Id;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _editingName = string.Empty;

    public TagItemViewModel(Tag model, Action<TagItemViewModel>? onRequestDelete = null, Action<TagItemViewModel>? onRenamed = null) {
        Model = model;
        _name = model.Name;
        _onRequestDelete = onRequestDelete;
        _onRenamed = onRenamed;
    }

    [RelayCommand]
    public void StartEdit() {
        EditingName = Name;
        IsEditing = true;
    }

    [RelayCommand]
    public void CommitEdit() {
        var trimmed = EditingName.Trim();
        if (!string.IsNullOrWhiteSpace(trimmed) && !string.Equals(trimmed, Name, StringComparison.Ordinal)) {
            Name = trimmed;
            Model.Name = trimmed;
            _onRenamed?.Invoke(this);
        }
        IsEditing = false;
    }

    [RelayCommand]
    public void CancelEdit() {
        EditingName = Name;
        IsEditing = false;
    }

    [RelayCommand]
    public void Delete() {
        _onRequestDelete?.Invoke(this);
    }
}
