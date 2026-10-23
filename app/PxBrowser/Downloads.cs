namespace PxBrowser;

internal sealed class DownloadItem
{
    public string Url { get; init; } = "";
    public string FilePath { get; set; } = "";
    public string State { get; set; } = "開始";
    public long Received { get; set; }
    public long Total { get; set; }
}
