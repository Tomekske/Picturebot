using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;

namespace Picturebot.ViewModels;

public partial class KeywordGroupViewModel : ObservableObject {
    public string Title { get; }
    public string GroupKey { get; }
    public bool IsHierarchical { get; }
    public ObservableCollection<KeywordChipViewModel> Chips { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChevronIconKind))]
    private bool _isExpanded = true;

    public MaterialIconKind ChevronIconKind => IsExpanded ? MaterialIconKind.ChevronDown : MaterialIconKind.ChevronRight;

    public KeywordGroupViewModel(string title, string groupKey, bool isHierarchical, IEnumerable<KeywordChipViewModel> chips) {
        Title = title;
        GroupKey = groupKey;
        IsHierarchical = isHierarchical;
        foreach (var chip in chips) {
            Chips.Add(chip);
        }
    }

    [RelayCommand]
    private void ToggleExpand() {
        IsExpanded = !IsExpanded;
    }
}
