namespace Graph.Infrastructure.Utilities;

public static class SupportedImageExtensions {
    public static readonly string[] RawExtensions = [".CR2", ".NEF", ".ARW", ".DNG", ".ORF", ".RAF"];
    public static readonly string[] JpgExtensions = [".JPG", ".JPEG"];

    public static readonly string[] AllExtensions = RawExtensions.Concat(JpgExtensions).ToArray();
}
