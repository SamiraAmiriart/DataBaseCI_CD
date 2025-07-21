namespace DatabaseBackupService.Services
{
    public interface IDatabaseBackupService
    {
        Task<bool> BackupDatabaseAsync(string connectionString, string outputPath);
        Task<bool> RunMigrationAsync(string migrationFile, string connectionString);
        Task<bool> RunPostMigrationTestsAsync(string connectionString);
        Task RollbackMigrationAsync(string connectionString);
    }
}
