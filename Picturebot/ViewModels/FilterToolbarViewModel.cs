using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Domain.Enums;

namespace Picturebot.ViewModels;

public partial class FilterToolbarViewModel : ViewModelBase
{
    private readonly Action? _onFilterChanged;
    private bool _isUpdating;

    public ObservableCollection<CurationStatus> FilterStatuses { get; } = new();
    public ObservableCollection<int> FilterRatings { get; } = new();
    public ObservableCollection<ColorLabel> FilterColors { get; } = new();

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
        IsStar0Active || IsStar1Active || IsStar2Active || IsStar3Active || IsStar4Active || IsStar5Active;

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
        }
        finally
        {
            _isUpdating = false;
            UpdateCollectionsAndNotify();
        }
    }
}
