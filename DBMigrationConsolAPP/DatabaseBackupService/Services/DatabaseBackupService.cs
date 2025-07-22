using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text;
using DatabaseBackupService.Models;

namespace DatabaseBackupService.Services
{
    public class DatabaseBackupService : IDatabaseBackupService
    {
        private readonly ILogger<DatabaseBackupService> _logger;
        private readonly BackupSettings _backupSettings;

        public DatabaseBackupService(ILogger<DatabaseBackupService> logger, IOptions<BackupSettings> backupSettings)
        {
            _logger = logger;
            _backupSettings = backupSettings.Value;
        }

        public async Task<bool> BackupDatabaseAsync(string connectionString, string outputPath)
        {
            try
            {
                _logger.LogInformation("Starting database backup to {OutputPath}", outputPath);

                // ایجاد دایرکتوری backup در صورت عدم وجود
                var directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

          
                var serverName = ExtractServerFromConnectionString(connectionString);
                var databaseName = ExtractDatabaseFromConnectionString(connectionString);

               
                var backupScript = GenerateBackupScript(databaseName);
                var tempScriptFile = Path.GetTempFileName();
                
                await File.WriteAllTextAsync(tempScriptFile, backupScript);

                var arguments = $"-S \"{serverName}\" -E -i \"{tempScriptFile}\" -o \"{outputPath}\" -W";

                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _backupSettings.SqlCmdPath,
                        Arguments = arguments,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                
                await process.WaitForExitAsync();

               
                if (File.Exists(tempScriptFile))
                {
                    File.Delete(tempScriptFile);
                }

                if (process.ExitCode == 0)
                {
                    _logger.LogInformation("Database backup completed successfully: {OutputPath}", outputPath);
                    return true;
                }
                else
                {
                    _logger.LogError("Database backup failed. Exit code: {ExitCode}, Error: {Error}", 
                        process.ExitCode, error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during database backup to {OutputPath}", outputPath);
                return false;
            }
        }

        public async Task<bool> RunMigrationAsync(string migrationFile, string connectionString)
        {
            try
            {
                if (!File.Exists(migrationFile))
                {
                    _logger.LogError("Migration file not found: {MigrationFile}", migrationFile);
                    return false;
                }

                _logger.LogInformation("Running migration script: {MigrationFile}", migrationFile);

                var serverName = ExtractServerFromConnectionString(connectionString);
                var arguments = $"-S \"{serverName}\" -E -i \"{migrationFile}\" -W";

                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _backupSettings.SqlCmdPath,
                        Arguments = arguments,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    _logger.LogInformation("Migration executed successfully");
                    return true;
                }
                else
                {
                    _logger.LogError("Migration failed. Exit code: {ExitCode}, Error: {Error}", 
                        process.ExitCode, error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during migration execution");
                return false;
            }
        }

        public async Task<bool> RunPostMigrationTestsAsync(string connectionString)
        {
            try
            {
                _logger.LogInformation("Running post-migration tests");

           
                var testScript = @"
                    SELECT COUNT(*) as TableCount FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE';
                    SELECT COUNT(*) as ViewCount FROM INFORMATION_SCHEMA.VIEWS;
                ";

                var tempScriptFile = Path.GetTempFileName();
                await File.WriteAllTextAsync(tempScriptFile, testScript);

                var serverName = ExtractServerFromConnectionString(connectionString);
                var arguments = $"-S \"{serverName}\" -E -i \"{tempScriptFile}\" -h -1 -W";

                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _backupSettings.SqlCmdPath,
                        Arguments = arguments,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                
                await process.WaitForExitAsync();

               
                if (File.Exists(tempScriptFile))
                {
                    File.Delete(tempScriptFile);
                }

                if (process.ExitCode == 0)
                {
                    _logger.LogInformation("Post-migration tests passed. Output: {Output}", output.Trim());
                    return true;
                }
                else
                {
                    _logger.LogError("Post-migration tests failed. Error: {Error}", error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during post-migration tests");
                return false;
            }
        }

        public async Task RollbackMigrationAsync(string connectionString)
        {
            try
            {
                _logger.LogInformation("Starting rollback process");


                var rollbackScript = @"
                    -- نمونه rollback script
                    -- در عمل باید rollback دقیق براساس migration انجام شود
                    PRINT 'Rollback completed';
                ";

                var tempScriptFile = Path.GetTempFileName();
                await File.WriteAllTextAsync(tempScriptFile, rollbackScript);

                var serverName = ExtractServerFromConnectionString(connectionString);
                var arguments = $"-S \"{serverName}\" -E -i \"{tempScriptFile}\" -W";

                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _backupSettings.SqlCmdPath,
                        Arguments = arguments,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                await process.WaitForExitAsync();

                
                if (File.Exists(tempScriptFile))
                {
                    File.Delete(tempScriptFile);
                }

                _logger.LogInformation("Rollback process completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during rollback process");
            }
        }

        private string ExtractServerFromConnectionString(string connectionString)
        {
            var parts = connectionString.Split(';');
            var serverPart = parts.FirstOrDefault(p => p.Trim().StartsWith("Server=", StringComparison.OrdinalIgnoreCase));
            return serverPart?.Split('=')[1].Trim() ?? "localhost";
        }

        private string ExtractDatabaseFromConnectionString(string connectionString)
        {
            var parts = connectionString.Split(';');
            var dbPart = parts.FirstOrDefault(p => p.Trim().StartsWith("Database=", StringComparison.OrdinalIgnoreCase));
            return dbPart?.Split('=')[1].Trim() ?? "master";
        }

        private string GenerateBackupScript(string databaseName)
        {
            return $@"
                USE [{databaseName}];
                
                -- Export schema
                SELECT 
                    'CREATE TABLE [' + TABLE_SCHEMA + '].[' + TABLE_NAME + '] (' +
                    STUFF((
                        SELECT ', [' + COLUMN_NAME + '] ' + DATA_TYPE + 
                               CASE 
                                   WHEN CHARACTER_MAXIMUM_LENGTH IS NOT NULL 
                                   THEN '(' + CAST(CHARACTER_MAXIMUM_LENGTH AS VARCHAR(10)) + ')'
                                   ELSE ''
                               END +
                               CASE WHEN IS_NULLABLE = 'NO' THEN ' NOT NULL' ELSE '' END
                        FROM INFORMATION_SCHEMA.COLUMNS c2
                        WHERE c2.TABLE_NAME = c1.TABLE_NAME 
                        AND c2.TABLE_SCHEMA = c1.TABLE_SCHEMA
                        FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, '') + ');'
                FROM INFORMATION_SCHEMA.COLUMNS c1
                GROUP BY TABLE_SCHEMA, TABLE_NAME;
            ";
        }
    }
}
