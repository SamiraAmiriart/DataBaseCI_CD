using Microsoft.Extensions.Logging;
using System.Text;

namespace DatabaseBackupService.Services
{
    public class FileComparisonService : IFileComparisonService
    {
        private readonly ILogger<FileComparisonService> _logger;

        public FileComparisonService(ILogger<FileComparisonService> logger)
        {
            _logger = logger;
        }

        public async Task<bool> CompareFilesAndGenerateDiffAsync(string file1, string file2, string diffFile)
        {
            try
            {
                if (!File.Exists(file1) || !File.Exists(file2))
                {
                    _logger.LogError("One or both files do not exist: {File1}, {File2}", file1, file2);
                    return false;
                }

                var content1 = await File.ReadAllTextAsync(file1);
                var content2 = await File.ReadAllTextAsync(file2);

                if (string.Equals(content1, content2, StringComparison.Ordinal))
                {
                    _logger.LogInformation("Files are identical");
                    return false;
                }

                _logger.LogInformation("Files are different, generating diff");

                var lines1 = content1.Split('\n');
                var lines2 = content2.Split('\n');

                var diff = GenerateUnifiedDiff(lines1, lines2, file1, file2);
                await File.WriteAllTextAsync(diffFile, diff);

                _logger.LogInformation("Diff file created: {DiffFile}", diffFile);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error comparing files");
                return false;
            }
        }

        private string GenerateUnifiedDiff(string[] lines1, string[] lines2, string file1, string file2)
        {
            var diff = new StringBuilder();
            diff.AppendLine($"--- {file1}");
            diff.AppendLine($"+++ {file2}");

           
            var maxLines = Math.Max(lines1.Length, lines2.Length);
            
            for (int i = 0; i < maxLines; i++)
            {
                var line1 = i < lines1.Length ? lines1[i] : "";
                var line2 = i < lines2.Length ? lines2[i] : "";

                if (line1 != line2)
                {
                    if (!string.IsNullOrEmpty(line1))
                        diff.AppendLine($"- {line1}");
                    if (!string.IsNullOrEmpty(line2))
                        diff.AppendLine($"+ {line2}");
                }
            }

            return diff.ToString();
        }
    }
}
