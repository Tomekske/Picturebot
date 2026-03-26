using Domain.Enums;

namespace Domain.Models;

public class PictureModel : NodeModel {
    public PictureModel() {
        Children = null;
    }

    public MetricsModel? Metrics { get; set; }

    public CurationStatus CurationStatus { get; set; } = CurationStatus.Unflagged;

    public SubFolderModel? SubFolder { get; set; }
}
