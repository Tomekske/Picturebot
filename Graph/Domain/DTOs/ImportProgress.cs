namespace Graph.Domain.DTOs;

public record ImportProgress(int Processed, int Total, string CurrentFile) {
    public double Percentage => Total > 0 ? (double)Processed / Total * 100 : 0;
}
