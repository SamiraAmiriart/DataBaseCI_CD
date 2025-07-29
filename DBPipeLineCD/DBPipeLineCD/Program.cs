using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DBMigrationConsolApp.ProductionDeploy
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Configure Serilog
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File("logs/cd-prod-deploy-.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            try
            {
                await CreateHostBuilder(args).Build().RunAsync();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "CD Worker Service failed to start.");
                throw;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSerilog()
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<ProductionDeployWorker>();
                    services.AddSingleton<IConfiguration>(hostContext.Configuration);
                });
    }

    public class ProductionDeployWorker : BackgroundService
    {
        private readonly ILogger<ProductionDeployWorker> _logger;
        private readonly IConfiguration _configuration;

        public ProductionDeployWorker(ILogger<ProductionDeployWorker> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Starting CD process to deploy migration script to production at {Time}", DateTime.UtcNow);

                    // Retrieve configuration settings
                    string prodConnectionString = _configuration["ConnectionStrings:Production"];
                    string migrationScriptPath = _configuration["Migration:ScriptPath"]; // e.g., /migrations/stage-to-prod.sql
                    string sqlCmdPath = _configuration["SqlCmd:Path"]; // Path to sqlcmd executable
                    string backupPath = _configuration["Backup:Path"]; // Path to store prod backup

                    if (string.IsNullOrEmpty(prodConnectionString) || string.IsNullOrEmpty(migrationScriptPath) || string.IsNullOrEmpty(sqlCmdPath))
                    {
                        _logger.LogError("Missing configuration settings. Aborting CD process.");
                        Environment.ExitCode = 1;
                        return;
                    }

                    // Step 1: Backup production database before applying migration
                    await BackupProductionDatabase(prodConnectionString, backupPath, sqlCmdPath);

                    // Step 2: Apply migration script to production
                    bool success = await ApplyMigrationScript(prodConnectionString, migrationScriptPath, sqlCmdPath);

                    if (success)
                    {
                        _logger.LogInformation("Migration script applied successfully to production.");
                        Environment.ExitCode = 200; // HTTP 200 equivalent for success
                    }
                    else
                    {
                        _logger.LogError("Failed to apply migration script to production. Initiating rollback.");
                        await RollbackProductionDatabase(prodConnectionString, backupPath, sqlCmdPath);
                        Environment.ExitCode = 1; // Error code
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred during the CD process. Aborting.");
                    Environment.ExitCode = 1;
                }

                // Run once for CD, then exit (can be modified to run periodically if needed)
                break;
            }
        }

        private async Task BackupProductionDatabase(string connectionString, string backupPath, string sqlCmdPath)
        {
            _logger.LogInformation("Backing up production database to {BackupPath}", backupPath);

            string backupScript = $"BACKUP DATABASE [YourDatabaseName] TO DISK = '{backupPath}/prod_backup_{DateTime.UtcNow:yyyyMMddHHmmss}.bak'";
            string arguments = $"-S {GetServerFromConnectionString(connectionString)} -Q \"{backupScript}\"";

            await ExecuteSqlCmd(sqlCmdPath, arguments, "Backup");
        }

        private async Task<bool> ApplyMigrationScript(string connectionString, string migrationScriptPath, string sqlCmdPath)
        {
            _logger.LogInformation("Applying migration script {ScriptPath} to production", migrationScriptPath);

            if (!File.Exists(migrationScriptPath))
            {
                _logger.LogError("Migration script not found at {ScriptPath}", migrationScriptPath);
                return false;
            }

            string arguments = $"-S {GetServerFromConnectionString(connectionString)} -i \"{migrationScriptPath}\"";
            return await ExecuteSqlCmd(sqlCmdPath, arguments, "Migration");
        }

        private async Task RollbackProductionDatabase(string connectionString, string backupPath, string sqlCmdPath)
        {
            _logger.LogInformation("Initiating rollback by restoring production database from {BackupPath}", backupPath);

            // Find the latest backup file
            string latestBackup = Directory.GetFiles(backupPath, "prod_backup_*.bak")
                .OrderByDescending(f => f)
                .FirstOrDefault();

            if (string.IsNullOrEmpty(latestBackup))
            {
                _logger.LogError("No backup file found for rollback.");
                return;
            }

            string restoreScript = $"RESTORE DATABASE [YourDatabaseName] FROM DISK = '{latestBackup}' WITH REPLACE";
            string arguments = $"-S {GetServerFromConnectionString(connectionString)} -Q \"{restoreScript}\"";

            await ExecuteSqlCmd(sqlCmdPath, arguments, "Rollback");
        }

        private async Task<bool> ExecuteSqlCmd(string sqlCmdPath, string arguments, string operation)
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = sqlCmdPath,
                        Arguments = arguments,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    _logger.LogInformation("{Operation} executed successfully. Output: {Output}", operation, output);
                    return true;
                }
                else
                {
                    _logger.LogError("{Operation} failed. Error: {Error}", operation, error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Operation} failed due to an exception.", operation);
                return false;
            }
        }

        private string GetServerFromConnectionString(string connectionString)
        {
            // Parse connection string to extract server name (simplified)
            var parts = connectionString.Split(';');
            foreach (var part in parts)
            {
                if (part.StartsWith("Server=", StringComparison.OrdinalIgnoreCase))
                {
                    return part.Substring(7);
                }
            }
            throw new ArgumentException("Invalid connection string: Server not found.");
        }
    }
}