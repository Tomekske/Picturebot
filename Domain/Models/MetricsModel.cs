namespace Domain.Models;

public class MetricsModel
{
    public int PictureId { get; set; }

    public int? Sharpness { get; set; }

    public ulong? PHash { get; set; }

    public PictureModel? Picture { get; set; }
}
