namespace DatabaseBackupService.Models
{
    public class BackupSettings
    {
        public string BackupDirectory { get; set; } = string.Empty;
        public string ProdBackupFile { get; set; } = string.Empty;
        public string DevBackupFile { get; set; } = string.Empty;
        public string DiffScriptFile { get; set; } = string.Empty;
        public string SqlCmdPath { get; set; } = "sqlcmd";
        public int SqlCmdTimeout { get; set; } = 300;
    }
}
