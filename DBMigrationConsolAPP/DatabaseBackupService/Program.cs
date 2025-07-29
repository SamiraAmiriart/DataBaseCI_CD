using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DatabaseBackupService.Models;
using DatabaseBackupService.Services;

namespace DatabaseBackupService
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();

            try
            {
                var backupService = host.Services.GetRequiredService<IDatabaseBackupService>();
                var fileService = host.Services.GetRequiredService<IFileComparisonService>();
                var logger = host.Services.GetRequiredService<ILogger<Program>>();
                var configuration = host.Services.GetRequiredService<IConfiguration>();
                var backupSettings = host.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<BackupSettings>>().Value;

                logger.LogInformation("Database Backup Service Starting...");

                
                var prodConnectionString = configuration.GetConnectionString("Production");
                var devConnectionString = configuration.GetConnectionString("Development");
                var stageConnectionString = configuration.GetConnectionString("Staging");

                if (string.IsNullOrEmpty(prodConnectionString) || string.IsNullOrEmpty(devConnectionString))
                {
                    logger.LogError("Connection strings are not configured properly");
                    return;
                }

                
                var prodBackupFile = Path.Combine(backupSettings.BackupDirectory, backupSettings.ProdBackupFile);
                var devBackupFile = Path.Combine(backupSettings.BackupDirectory, backupSettings.DevBackupFile);
                var diffScriptFile = Path.Combine(backupSettings.BackupDirectory, backupSettings.DiffScriptFile);

               
                if (!Directory.Exists(backupSettings.BackupDirectory))
                {
                    Directory.CreateDirectory(backupSettings.BackupDirectory);
                }

                
                logger.LogInformation("Starting production backup...");
                var prodBackupSuccess = await backupService.BackupDatabaseAsync(prodConnectionString, prodBackupFile);
                if (!prodBackupSuccess)
                {
                    logger.LogError("Production backup failed");
                    return;
                }

               
                logger.LogInformation("Starting development backup...");
                var devBackupSuccess = await backupService.BackupDatabaseAsync(devConnectionString, devBackupFile);
                if (!devBackupSuccess)
                {
                    logger.LogError("Development backup failed");
                    return;
                }

                
                logger.LogInformation("Comparing backup files...");
                var hasDiff = await fileService.CompareFilesAndGenerateDiffAsync(prodBackupFile, devBackupFile, diffScriptFile);

                if (hasDiff)
                {
                    logger.LogInformation("Differences detected! Diff script created.");

                    
                    if (!string.IsNullOrEmpty(stageConnectionString))
                    {
                        logger.LogInformation("Running migration on staging...");
                        var migrationSuccess = await backupService.RunMigrationAsync(diffScriptFile, stageConnectionString);
                        
                        if (migrationSuccess)
                        {
                            logger.LogInformation("Migration successful. Running tests...");
                            var testsPassed = await backupService.RunPostMigrationTestsAsync(stageConnectionString);

                            if (!testsPassed)
                            {
                                logger.LogWarning("Tests failed after migration. Rolling back...");
                                await backupService.RollbackMigrationAsync(stageConnectionString);
                            }
                            else
                            {
                                logger.LogInformation("All tests passed after migration.");
                            }
                        }
                        else
                        {
                            logger.LogError("Migration failed. Rolling back...");
                            await backupService.RollbackMigrationAsync(stageConnectionString);
                        }
                    }
                    else
                    {
                        logger.LogWarning("Staging connection string not configured. Skipping migration test.");
                    }
                }
                else
                {
                    logger.LogInformation("No differences detected between databases.");
                }

                logger.LogInformation("Database Backup Service finished successfully.");
            }
            catch (Exception ex)
            {
                var logger = host.Services.GetRequiredService<ILogger<Program>>();
                logger.LogError(ex, "An error occurred during service execution");
            }
        }

        static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                })
                .ConfigureServices((context, services) =>
                {
                    services.Configure<BackupSettings>(context.Configuration.GetSection("BackupSettings"));
                    services.AddScoped<IDatabaseBackupService, Services.DatabaseBackupService>();
                    services.AddScoped<IFileComparisonService, FileComparisonService>();
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.AddFile("Logs/app-{Date}.txt");
                });
    }
}
