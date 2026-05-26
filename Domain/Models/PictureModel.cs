using Domain.Enums;

namespace Domain.Models;

public class PictureModel : NodeModel {
    public PictureModel() {
        Children = null;
    }

    public MetricsModel? Metrics { get; set; }

    public CurationStatus CurationStatus { get; set; } = CurationStatus.Unflagged;

    public ColorLabel ColorLabel { get; set; } = ColorLabel.None;

    public int Rating { get; set; } = 0;

    public SubFolderModel? SubFolder { get; set; }
}
