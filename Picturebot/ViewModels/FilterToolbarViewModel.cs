using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Domain.Enums;

namespace Picturebot.ViewModels;

public partial class TagFilterItemViewModel : ViewModelBase
{
    private readonly Action _onToggled;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private int _count;

    [ObservableProperty]
    private bool _isSelected;

    public TagFilterItemViewModel(string name, int count, bool isSelected, Action onToggled)
    {
        _name = name;
        _count = count;
        _isSelected = isSelected;
        _onToggled = onToggled;
    }

    partial void OnIsSelectedChanged(bool value)
    {
        _onToggled();
    }
}

public partial class FilterToolbarViewModel : ViewModelBase
{
    private readonly Action? _onFilterChanged;
    private bool _isUpdating;

    public ObservableCollection<CurationStatus> FilterStatuses { get; } = new();
    public ObservableCollection<int> FilterRatings { get; } = new();
    public ObservableCollection<ColorLabel> FilterColors { get; } = new();

    public ObservableCollection<TagFilterItemViewModel> AllTags { get; } = new();
    public ObservableCollection<TagFilterItemViewModel> VisibleTags { get; } = new();

    [ObservableProperty]
    private string _tagSearchText = string.Empty;

    [ObservableProperty]
    private bool _isMatchAll;

    [ObservableProperty]
    private bool _isMatchAny = true;

    [ObservableProperty]
    private bool _isMatchNot;

    public FilterToolbarViewModel(Action? onFilterChanged = null)
    {
        _onFilterChanged = onFilterChanged;
    }

    [ObservableProperty]
    private bool _isFlaggedActive;

    [ObservableProperty]
    private bool _isNeutralActive;

    [ObservableProperty]
    private bool _isRejectedActive;

    [ObservableProperty]
    private bool _isGreenActive;

    [ObservableProperty]
    private bool _isBlueActive;

    [ObservableProperty]
    private bool _isYellowOrangeActive;

    [ObservableProperty]
    private bool _isRedActive;

    [ObservableProperty]
    private bool _isPurpleActive;

    [ObservableProperty]
    private bool _isNoneActive;

    [ObservableProperty]
    private bool _isStar0Active;

    [ObservableProperty]
    private bool _isStar1Active;

    [ObservableProperty]
    private bool _isStar2Active;

    [ObservableProperty]
    private bool _isStar3Active;

    [ObservableProperty]
    private bool _isStar4Active;

    [ObservableProperty]
    private bool _isStar5Active;

    public bool IsAnyFilterActive =>
        IsFlaggedActive || IsNeutralActive || IsRejectedActive ||
        IsGreenActive || IsBlueActive || IsYellowOrangeActive || IsRedActive || IsPurpleActive || IsNoneActive ||
        IsStar0Active || IsStar1Active || IsStar2Active || IsStar3Active || IsStar4Active || IsStar5Active ||
        IsTagFilterActive;

    partial void OnIsFlaggedActiveChanged(bool value) => UpdateCollectionsAndNotify();
    partial void OnIsNeutralActiveChanged(bool value) => UpdateCollectionsAndNotify();
    partial void OnIsRejectedActiveChanged(bool value) => UpdateCollectionsAndNotify();

    partial void OnIsGreenActiveChanged(bool value) => UpdateCollectionsAndNotify();
    partial void OnIsBlueActiveChanged(bool value) => UpdateCollectionsAndNotify();
    partial void OnIsYellowOrangeActiveChanged(bool value) => UpdateCollectionsAndNotify();
    partial void OnIsRedActiveChanged(bool value) => UpdateCollectionsAndNotify();
    partial void OnIsPurpleActiveChanged(bool value) => UpdateCollectionsAndNotify();
    partial void OnIsNoneActiveChanged(bool value) => UpdateCollectionsAndNotify();

    partial void OnIsStar0ActiveChanged(bool value) { if (value) ClearOtherStars(0); UpdateCollectionsAndNotify(); }
    partial void OnIsStar1ActiveChanged(bool value) { if (value) ClearOtherStars(1); UpdateCollectionsAndNotify(); }
    partial void OnIsStar2ActiveChanged(bool value) { if (value) ClearOtherStars(2); UpdateCollectionsAndNotify(); }
    partial void OnIsStar3ActiveChanged(bool value) { if (value) ClearOtherStars(3); UpdateCollectionsAndNotify(); }
    partial void OnIsStar4ActiveChanged(bool value) { if (value) ClearOtherStars(4); UpdateCollectionsAndNotify(); }
    partial void OnIsStar5ActiveChanged(bool value) { if (value) ClearOtherStars(5); UpdateCollectionsAndNotify(); }

    private void ClearOtherStars(int activeRating)
    {
        _isUpdating = true;
        try
        {
            if (activeRating != 0) IsStar0Active = false;
            if (activeRating != 1) IsStar1Active = false;
            if (activeRating != 2) IsStar2Active = false;
            if (activeRating != 3) IsStar3Active = false;
            if (activeRating != 4) IsStar4Active = false;
            if (activeRating != 5) IsStar5Active = false;
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void UpdateCollectionsAndNotify()
    {
        if (_isUpdating) return;

        // Curation Statuses
        FilterStatuses.Clear();
        if (IsFlaggedActive) FilterStatuses.Add(CurationStatus.Flagged);
        if (IsNeutralActive) FilterStatuses.Add(CurationStatus.Unflagged);
        if (IsRejectedActive) FilterStatuses.Add(CurationStatus.Rejected);

        // Ratings
        FilterRatings.Clear();
        if (IsStar0Active) FilterRatings.Add(0);
        if (IsStar1Active) FilterRatings.Add(1);
        if (IsStar2Active) FilterRatings.Add(2);
        if (IsStar3Active) FilterRatings.Add(3);
        if (IsStar4Active) FilterRatings.Add(4);
        if (IsStar5Active) FilterRatings.Add(5);

        // Colors
        FilterColors.Clear();
        if (IsGreenActive) FilterColors.Add(ColorLabel.Green);
        if (IsBlueActive) FilterColors.Add(ColorLabel.Blue);
        if (IsYellowOrangeActive)
        {
            FilterColors.Add(ColorLabel.Yellow);
            FilterColors.Add(ColorLabel.Orange);
        }
        if (IsRedActive) FilterColors.Add(ColorLabel.Red);
        if (IsPurpleActive) FilterColors.Add(ColorLabel.Purple);
        if (IsNoneActive) FilterColors.Add(ColorLabel.None);

        OnPropertyChanged(nameof(IsTagFilterActive));
        OnPropertyChanged(nameof(ActiveTagFiltersCountText));
        OnPropertyChanged(nameof(IsAnyFilterActive));
        _onFilterChanged?.Invoke();
    }

    public void SetFlaggedOnly()
    {
        _isUpdating = true;
        try
        {
            IsFlaggedActive = true;
            IsNeutralActive = false;
            IsRejectedActive = false;

            IsGreenActive = false;
            IsBlueActive = false;
            IsYellowOrangeActive = false;
            IsRedActive = false;
            IsPurpleActive = false;
            IsNoneActive = false;

            IsStar0Active = false;
            IsStar1Active = false;
            IsStar2Active = false;
            IsStar3Active = false;
            IsStar4Active = false;
            IsStar5Active = false;
        }
        finally
        {
            _isUpdating = false;
            UpdateCollectionsAndNotify();
        }
    }

    [RelayCommand]
    public void ClearAll()
    {
        _isUpdating = true;
        try
        {
            IsFlaggedActive = false;
            IsNeutralActive = false;
            IsRejectedActive = false;

            IsGreenActive = false;
            IsBlueActive = false;
            IsYellowOrangeActive = false;
            IsRedActive = false;
            IsPurpleActive = false;
            IsNoneActive = false;

            IsStar0Active = false;
            IsStar1Active = false;
            IsStar2Active = false;
            IsStar3Active = false;
            IsStar4Active = false;
            IsStar5Active = false;

            foreach (var tag in AllTags)
            {
                tag.IsSelected = false;
            }
        }
        finally
        {
            _isUpdating = false;
            UpdateCollectionsAndNotify();
        }
    }

    public bool IsTagFilterActive => AllTags.Any(t => t.IsSelected);

    public string ActiveTagFiltersCountText =>
        AllTags.Any(t => t.IsSelected) ? $" ({AllTags.Count(t => t.IsSelected)})" : "";

    partial void OnTagSearchTextChanged(string value) => RefreshVisibleTags();

    partial void OnIsMatchAllChanged(bool value)
    {
        if (value && !_isUpdating)
        {
            _isUpdating = true;
            IsMatchAny = false;
            IsMatchNot = false;
            _isUpdating = false;
        }
        UpdateCollectionsAndNotify();
    }

    partial void OnIsMatchAnyChanged(bool value)
    {
        if (value && !_isUpdating)
        {
            _isUpdating = true;
            IsMatchAll = false;
            IsMatchNot = false;
            _isUpdating = false;
        }
        UpdateCollectionsAndNotify();
    }

    partial void OnIsMatchNotChanged(bool value)
    {
        if (value && !_isUpdating)
        {
            _isUpdating = true;
            IsMatchAll = false;
            IsMatchAny = false;
            _isUpdating = false;
        }
        UpdateCollectionsAndNotify();
    }

    public void UpdateAvailableTags(IEnumerable<PictureItemViewModel> pictures)
    {
        var tagCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var pic in pictures)
        {
            if (pic.Keywords != null)
            {
                foreach (var tag in pic.Keywords)
                {
                    if (tagCounts.ContainsKey(tag))
                        tagCounts[tag]++;
                    else
                        tagCounts[tag] = 1;
                }
            }
        }

        var selectedTags = AllTags.Where(t => t.IsSelected).Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        _isUpdating = true;
        try
        {
            AllTags.Clear();
            foreach (var kvp in tagCounts.OrderBy(k => k.Key))
            {
                var isSel = selectedTags.Contains(kvp.Key);
                AllTags.Add(new TagFilterItemViewModel(kvp.Key, kvp.Value, isSel, UpdateCollectionsAndNotify));
            }
        }
        finally
        {
            _isUpdating = false;
        }

        RefreshVisibleTags();
        OnPropertyChanged(nameof(IsTagFilterActive));
        OnPropertyChanged(nameof(ActiveTagFiltersCountText));
    }

    public void RefreshVisibleTags()
    {
        VisibleTags.Clear();
        var search = TagSearchText?.Trim();
        foreach (var tag in AllTags)
        {
            if (string.IsNullOrEmpty(search) || tag.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                VisibleTags.Add(tag);
            }
        }
    }

    [RelayCommand]
    public void ClearTagFilters()
    {
        _isUpdating = true;
        try
        {
            foreach (var tag in AllTags)
            {
                tag.IsSelected = false;
            }
        }
        finally
        {
            _isUpdating = false;
            UpdateCollectionsAndNotify();
        }
    }
}
