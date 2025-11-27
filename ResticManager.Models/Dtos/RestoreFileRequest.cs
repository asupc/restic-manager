namespace ResticManager.Models.Dtos
{
    public class RestoreFileRequest
    {
        public string SnapshotId { get; set; }
        public string FilePath { get; set; }
        public string TargetPath { get; set; }
    }
}
