using System.IO.Compression;
using System.Text.RegularExpressions;

namespace CodeDbConnectionsMetricCollector.Api.Services;

public interface IRepositoryAnalyzerService
{
    Task<int> CountDatabaseConnectionsAsync(string repositoryUrl, CancellationToken cancellationToken = default);
    Task<int> CountDockerComposeDatabasesAsync(string repositoryUrl, CancellationToken cancellationToken = default);
}

public class RepositoryAnalyzerService : IRepositoryAnalyzerService
{
    // Patterns that indicate a database connection being opened/created
    private static readonly Regex[] ConnectionPatterns =
    [
        // ADO.NET / ODBC / OLEDB
        new Regex(@"new\s+SqlConnection\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"new\s+NpgsqlConnection\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"new\s+MySqlConnection\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"new\s+OracleConnection\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"new\s+SQLiteConnection\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"new\s+OdbcConnection\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"new\s+OleDbConnection\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"new\s+Db2Connection\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        // Entity Framework / Dapper context creation
        new Regex(@"new\s+\w*DbContext\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"DbContext\s*\.\s*Database\s*\.\s*GetDbConnection\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"UseSqlServer\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"UseNpgsql\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"UseMySql\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"UseSqlite\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"UseOracle\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        // Python: SQLAlchemy, psycopg2, pymysql, sqlite3, pyodbc
        new Regex(@"create_engine\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"psycopg2\s*\.\s*connect\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"pymysql\s*\.\s*connect\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"sqlite3\s*\.\s*connect\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"pyodbc\s*\.\s*connect\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"cx_Oracle\s*\.\s*connect\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"mysql\.connector\s*\.\s*connect\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        // Java: JDBC, Hibernate, Spring DataSource
        new Regex(@"DriverManager\s*\.\s*getConnection\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"dataSource\s*\.\s*getConnection\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"new\s+\w*DataSource\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        // Node.js: mysql, pg, mongodb, mongoose, sequelize
        new Regex(@"mysql\s*\.\s*createConnection\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"mysql\s*\.\s*createPool\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"new\s+Client\s*\(\s*\{[^}]*(?:host|connectionString)[^}]*\}\s*\)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"mongoose\s*\.\s*connect\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"new\s+Sequelize\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"knex\s*\(\s*\{", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        // Ruby: ActiveRecord, pg, mysql2
        new Regex(@"ActiveRecord::Base\s*\.\s*establish_connection", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"PG\s*\.\s*connect\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"Mysql2::Client\s*\.new\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        // PHP: PDO, mysqli
        new Regex(@"new\s+PDO\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"new\s+mysqli\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"mysqli_connect\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        // Go: sql.Open
        new Regex(@"sql\s*\.\s*Open\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"sqlx\s*\.\s*Open\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"gorm\s*\.\s*Open\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    ];

    // Docker image names that indicate a database service.
    // (?:\S+/)* handles multi-segment registry paths like mcr.microsoft.com/mssql/server:...
    // (?:[:/\s]|$) allows the db keyword to be followed by a tag (:), sub-path (/), whitespace, or EOL
    private static readonly Regex[] DockerDatabaseImagePatterns =
    [
        new Regex(@"image\s*:\s*(?:\S+/)*(?:mysql|mariadb|postgres|postgis|mongodb|mongo|redis|cassandra|mssql|sqlserver|oracle|couchdb|cockroachdb|elasticsearch|opensearch|neo4j|influxdb|timescaledb|citus|db2|firebird|hana)(?:[:/\s]|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    ];

    private static readonly Regex[] SpringAppConfigPatterns =
    [
        // application.properties
        new Regex(@"^\s*spring\.datasource\.(?:url|jdbc-url)\s*[:=]", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"^\s*spring\.r2dbc\.url\s*[:=]", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        // application.yml / application.yaml (nested or flattened keys)
        new Regex(@"^\s*(?:spring\.datasource\.(?:url|jdbc-url)|spring\.r2dbc\.url)\s*:\s*", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"^\s*(?:url|jdbc-url)\s*:\s*(?:jdbc|r2dbc):", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    ];

    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", ".git", "bin", "obj", "dist", "build", ".gradle", "target", "vendor", "__pycache__"
    };

    private static readonly HashSet<string> SourceExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".java", ".py", ".js", ".ts", ".jsx", ".tsx", ".rb", ".php", ".go",
        ".kt", ".scala", ".cpp", ".c", ".h", ".rs", ".swift", ".properties", ".yml", ".yaml"
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RepositoryAnalyzerService> _logger;

    public RepositoryAnalyzerService(IHttpClientFactory httpClientFactory, ILogger<RepositoryAnalyzerService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<int> CountDatabaseConnectionsAsync(string repositoryUrl, CancellationToken cancellationToken = default)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "repo-analyzer-" + Guid.NewGuid().ToString("N"));
        try
        {
            await DownloadRepositoryAsync(repositoryUrl, tempDir, cancellationToken);
            return CountConnectionsInDirectory(tempDir);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    public async Task<int> CountDockerComposeDatabasesAsync(string repositoryUrl, CancellationToken cancellationToken = default)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "repo-analyzer-" + Guid.NewGuid().ToString("N"));
        try
        {
            await DownloadRepositoryAsync(repositoryUrl, tempDir, cancellationToken);
            return CountDatabasesInDockerCompose(tempDir);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    private async Task DownloadRepositoryAsync(string repositoryUrl, string targetDir, CancellationToken cancellationToken)
    {
        var zipUrl = BuildZipUrl(repositoryUrl);
        var client = _httpClientFactory.CreateClient("github");

        _logger.LogInformation("Downloading repository from {Url}", zipUrl);

        var response = await client.GetAsync(zipUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        var zipPath = targetDir + ".zip";
        try
        {
            await using var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fs, cancellationToken);

            await fs.FlushAsync(cancellationToken);
        }
        finally
        {
            if (File.Exists(zipPath) && File.GetAttributes(zipPath).HasFlag(FileAttributes.ReadOnly))
                File.SetAttributes(zipPath, FileAttributes.Normal);
        }

        try
        {
            ZipFile.ExtractToDirectory(zipPath, targetDir);
        }
        finally
        {
            if (File.Exists(zipPath))
                File.Delete(zipPath);
        }
    }

    private static string BuildZipUrl(string repositoryUrl)
    {
        // Normalize: strip trailing slash and .git suffix
        var url = repositoryUrl.TrimEnd('/');
        if (url.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            url = url[..^4];

        // GitHub: https://github.com/owner/repo → zip of default branch
        if (url.Contains("github.com", StringComparison.OrdinalIgnoreCase))
            return url + "/archive/refs/heads/main.zip";

        // GitLab: https://gitlab.com/owner/repo → zip
        if (url.Contains("gitlab.com", StringComparison.OrdinalIgnoreCase))
            return url + "/-/archive/main/repo-main.zip";

        // Bitbucket: https://bitbucket.org/owner/repo → zip
        if (url.Contains("bitbucket.org", StringComparison.OrdinalIgnoreCase))
            return url + "/get/main.zip";

        // Fallback: assume GitHub-style
        return url + "/archive/refs/heads/main.zip";
    }

    private int CountConnectionsInDirectory(string directory)
    {
        int total = 0;
        foreach (var file in EnumerateSourceFiles(directory))
        {
            try
            {
                var patterns = IsSpringApplicationConfig(file) ? SpringAppConfigPatterns : ConnectionPatterns;

                foreach (var line in File.ReadLines(file))
                {
                    // Count at most one match per line to avoid inflated counts
                    // when multiple equivalent patterns match the same statement.
                    if (patterns.Any(pattern => pattern.IsMatch(line)))
                        total++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not read file {File}", file);
            }
        }
        return total;
    }

    private int CountDatabasesInDockerCompose(string directory)
    {
        var composeFileNames = new[] { "docker-compose.yml", "docker-compose.yaml", "compose.yml", "compose.yaml" };

        var composeFiles = Directory
            .EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Where(f => composeFileNames.Contains(Path.GetFileName(f), StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (composeFiles.Count == 0)
        {
            _logger.LogWarning("No docker-compose file found in repository");
            return 0;
        }

        int total = 0;
        foreach (var file in composeFiles)
        {
            try
            {
                foreach (var line in File.ReadLines(file))
                {
                    if (DockerDatabaseImagePatterns.Any(p => p.IsMatch(line)))
                        total++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not read docker-compose file {File}", file);
            }
        }
        return total;
    }

    private static bool IsSpringApplicationConfig(string file)
    {
        var fileName = Path.GetFileName(file);
        return fileName.Equals("application.properties", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("application.yml", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("application.yaml", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> EnumerateSourceFiles(string root)
    {
        var stack = new Stack<string>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            var dirName = Path.GetFileName(dir);

            if (IgnoredDirectories.Contains(dirName))
                continue;

            foreach (var file in Directory.EnumerateFiles(dir))
            {
                if (SourceExtensions.Contains(Path.GetExtension(file)))
                    yield return file;
            }

            foreach (var subDir in Directory.EnumerateDirectories(dir))
                stack.Push(subDir);
        }
    }
}
