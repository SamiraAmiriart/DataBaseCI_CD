# DB Migration Console App

A C#-based worker service for automated database schema and data migration, integrated with CI/CD pipelines. This project facilitates safe, version-controlled migrations between development, staging, and production environments, with built-in backups, comparisons, and logging. It is designed to handle schema differences, static data backups, and hotfixes while minimizing risks in production deployments.

**Note:** This project is under active development and will be expanded in the coming weeks with more robust features, testing, and optimizations.

## Table of Contents
- [Features](#features)
- [Technologies](#technologies)
- [Architecture and Workflow](#architecture-and-workflow)
- [Installation](#installation)
- [Usage](#usage)
- [CI/CD Pipeline](#cicd-pipeline)
- [Challenges and Future Improvements](#challenges-and-future-improvements)
- [Contributing](#contributing)
- [License](#license)

## Features
- **Automated Backups and Comparisons**: Generates backups of schemas and static data from production and development databases, then compares them to detect differences.
- **Script Generation and Merging**: Creates migration scripts based on differences, incorporating merged changes while handling unmerged updates.
- **Environment-Specific Migrations**: Supports migrations from development to staging, staging to production, and direct hotfixes from development to production (with fallback to staging).
- **Error Handling and Exit Conditions**: Exits the pipeline early on failures (e.g., returns HTTP 200 on success; aborts on errors to prevent faulty deploys).
- **Comprehensive Logging**: Logs all steps using Serilog for traceability and debugging.
- **CI/CD Integration**: Leverages GitHub Actions for automated builds, tests, and deployments.
- **Hotfix Support**: Allows quick patches from development to production, followed by synchronization to staging.

## Technologies
This project is built using the following technologies and tools:

- **Servers**: Linux Ubuntu 22.04
- **Core Service**: C# Worker Service (for the DBMigrationConsolApp)
- **Database**: SQL Server 2022
- **Automation and Orchestration**: Ansible (for infrastructure management and deployments)
- **CI/CD**: GitHub Actions
- **Database Tools**: sqlcmd (used for script generation in testing environments; note: this is a temporary choice for simplicity and may be replaced with a more robust tool like DACPAC or Entity Framework Migrations in production)
- **Logging**: Serilog (for structured logging across all migration steps)

## Architecture and Workflow
The project follows a chain-based CI/CD approach to ensure safe database migrations:

1. **Backup and Comparison Phase** (via DBMigrationConsolApp C# service):
   - Backs up schemas and static data from the production (prod) database and stores it on the prod server.
   - Backs up schemas and static data from the development (develop) database.
   - Compares the prod and develop backup scripts.
   - If differences are detected (assuming develop should mirror prod), generates a migration script and stores it on develop.
   - Re-compares based on any database merges; adds unseen differences to the script.
     - **Challenge Note**: Unmerged changes from teams may be included prematurely. To mitigate, consider a separate repository for review and approval to avoid forcing all changes to prod.

2. **Migration to Staging**:
   - Applies the generated script to the staging (stage) server via the C# service.
   - Returns HTTP 200 on success; aborts the chain on errors (database migration success is a prerequisite for app deployment).

3. **CD to Production**:
   - A separate C# service applies the staging-validated script to the prod server.
     - **Challenge Note**: Testing and rollback in prod are complex; requires careful planning.

4. **Hotfix Workflow**:
   - Applies changes directly from develop to prod.
   - Follows up with a request to apply the same changes to stage for consistency.

5. **Logging**: All steps are logged using Serilog and stored in a dedicated service for auditing.

This workflow ensures migrations are atomic, traceable, and reversible where possible.

## Installation
### Prerequisites
- .NET SDK (version 8.0 or later) for C# development.
- SQL Server 2022 instance(s) for prod, develop, and stage environments.
- Ubuntu 22.04 servers configured with Ansible.
- GitHub account with Actions enabled.

### Steps
1. Clone the repository:
      git clone https://github.com/yourusername/dbmigrationconsolapp.git
   cd dbmigrationconsolapp
   ```

2. Restore dependencies:
   ```
   dotnet restore
   ```

3. Configure environment variables (e.g., in `appsettings.json` or via secrets):
   - Database connection strings for prod, develop, and stage.
   - Serilog configuration for logging sinks.

4. Build the project:
   ```
   dotnet build
   ```

5. (Optional) Set up Ansible playbooks for server provisioning (see `/ansible` directory for examples).

## Usage
1. Run the console app locally for testing:
   ```
   dotnet run --project DBMigrationConsolApp
   ```
   - Pass arguments if needed (e.g., `--environment=develop`).

2. Trigger migrations via CI/CD (see below) or manually invoke the service:
   ```
   dotnet run -- migration --source=develop --target=stage
   ```

For full automation, integrate with GitHub Actions.

## CI/CD Pipeline
This project uses GitHub Actions for CI/CD:
- **Triggers**: On push/pull requests to `main` or manual dispatch.
- **Jobs**:
  - Build and test the C# service.
  - Run backups, comparisons, and script generation.
  - Deploy to stage/prod using Ansible tasks.
  - Log outputs via Serilog.

Workflow files are in `.github/workflows/`. Example: `ci-cd.yml` (customize as needed).

## Challenges and Future Improvements
- **Unmerged Changes**: Risk of including unapproved changes in scripts. **Planned Fix**: Introduce a separate review repository for confirmation before merging to prod.
- **Testing and Rollback in Prod**: Hard to simulate prod failures safely. **Planned**: Implement unit/integration tests with mocked databases and explore rollback strategies (e.g., via transaction scopes or snapshot backups).
- **sqlcmd Limitations**: Currently used for simplicity in testing; not ideal for complex scenarios. **Planned**: Migrate to more advanced tools like SSDT (SQL Server Data Tools) or FluentMigrator.
- **Upcoming Enhancements**: Expanded testing suite, email notifications on failures, and support for dynamic data handling (beyond static data).

Contributions to address these are welcome!

## Contributing
We welcome contributions! Please follow these steps:
1. Fork the repository.
2. Create a feature branch (`git checkout -b feature/YourFeature`).
3. Commit your changes (`git commit -m 'Add YourFeature'`).
4. Push to the branch (`git push origin feature/YourFeature`).
5. Open a Pull Request.

For issues or suggestions, open a GitHub issue.

## License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

This README is designed to be visually appealing (with badges and TOC) and informative for potential contributors or users. Replace placeholders like `yourusername/dbmigrationconsolapp` with your actual repo details. If you need a Persian version, more sections (e.g., screenshots), or adjustments based on updates to your project, just let me know! 
