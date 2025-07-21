namespace DatabaseBackupService.Services
{
    public interface IFileComparisonService
    {
        Task<bool> CompareFilesAndGenerateDiffAsync(string file1, string file2, string diffFile);
    }
}
