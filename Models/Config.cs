namespace IceSpyTools.Models
{
    public class Config
    {
        public bool DeathSpy { get; set; }
        public bool TimedSpy { get; set; }
        public int TimedSpySeconds { get; set; }
        public bool KeepBackups { get; set; }
        public int BackupNumber { get; set; }
        public bool Debug { get; set; }
    }
}
