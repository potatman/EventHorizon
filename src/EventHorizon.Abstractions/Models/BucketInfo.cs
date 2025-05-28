namespace EventHorizon.Abstractions.Models;

public class BucketInfo
{
    public string SnapshotType { get; set; }
    public string SnapshotBucketId { get; set; }
    public string ViewBucketId { get; set; }
}
