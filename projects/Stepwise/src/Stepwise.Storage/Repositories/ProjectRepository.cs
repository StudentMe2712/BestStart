using System.Data;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Stepwise.Core.Interfaces;
using Stepwise.Core.Models;

namespace Stepwise.Storage.Repositories;

/// <summary>
/// Реализация SQLite-хранилища проектов и шагов инструкции.
/// Хранит базу данных в [ProjectRootPath]/project.db и организует привязку относительных путей скриншотов.
/// </summary>
public sealed class ProjectRepository : IProjectRepository
{
    private readonly SqliteConnection _connection;
    private readonly bool _ownsConnection;
    private readonly object _syncLock = new();
    private bool _isDisposed;

    public string ProjectRootPath { get; }

    /// <summary>
    /// Конструктор для работы с файлом проекта на диске.
    /// </summary>
    public ProjectRepository(string projectRootPath)
    {
        ProjectRootPath = projectRootPath ?? throw new ArgumentNullException(nameof(projectRootPath));
        Directory.CreateDirectory(ProjectRootPath);

        var dbPath = Path.Combine(ProjectRootPath, "project.db");
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString();

        _connection = new SqliteConnection(connectionString);
        _connection.Open();
        _ownsConnection = true;

        InitializeSchema();
    }

    /// <summary>
    /// Конструктор для модульного тестирования (поддерживает in-memory соединение).
    /// </summary>
    public ProjectRepository(SqliteConnection existingConnection, string projectRootPath)
    {
        _connection = existingConnection ?? throw new ArgumentNullException(nameof(existingConnection));
        ProjectRootPath = projectRootPath ?? string.Empty;
        _ownsConnection = false;

        if (_connection.State != ConnectionState.Open)
        {
            _connection.Open();
        }

        InitializeSchema();
    }

    private void InitializeSchema() => InitializeDatabase();

    private void InitializeDatabase()
    {
        const string sql = @"
            CREATE TABLE IF NOT EXISTS Projects (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                RootPath TEXT NOT NULL,
                Description TEXT,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Steps (
                Id TEXT PRIMARY KEY,
                SequenceIndex INTEGER NOT NULL,
                Timestamp TEXT NOT NULL,
                Action TEXT NOT NULL,
                ClickX REAL NOT NULL,
                ClickY REAL NOT NULL,
                TargetName TEXT,
                TargetControlType TEXT,
                TargetAutomationId TEXT,
                TargetClassName TEXT,
                TargetProcessName TEXT,
                TargetProcessId INTEGER,
                TargetWindowTitle TEXT,
                TargetWindowHandle INTEGER,
                TargetBoundingBoxX REAL,
                TargetBoundingBoxY REAL,
                TargetBoundingBoxWidth REAL,
                TargetBoundingBoxHeight REAL,
                TargetIsPassword INTEGER,
                TargetFrameworkId TEXT,
                ScreenshotRelativePath TEXT,
                Title TEXT,
                Description TEXT,
                MetadataJson TEXT
            );

            CREATE INDEX IF NOT EXISTS IX_Steps_SequenceIndex ON Steps(SequenceIndex);
        ";

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();

        try
        {
            using var alterCmd = _connection.CreateCommand();
            alterCmd.CommandText = "ALTER TABLE Steps ADD COLUMN TargetIsPassword INTEGER;";
            alterCmd.ExecuteNonQuery();
        }
        catch (SqliteException)
        {
            // Колонка уже существует
        }

        try
        {
            using var alterCmd = _connection.CreateCommand();
            alterCmd.CommandText = "ALTER TABLE Steps ADD COLUMN TargetFrameworkId TEXT;";
            alterCmd.ExecuteNonQuery();
        }
        catch (SqliteException)
        {
            // Колонка уже существует
        }
    }

    public Project CreateProject(string projectName, string? description = null)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        lock (_syncLock)
        {
            var id = Guid.NewGuid();
            var now = DateTime.UtcNow;

            const string sql = @"
                INSERT INTO Projects (Id, Name, RootPath, Description, CreatedAt, UpdatedAt)
                VALUES ($id, $name, $rootPath, $description, $createdAt, $updatedAt);
            ";

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("$id", id.ToString());
            cmd.Parameters.AddWithValue("$name", projectName);
            cmd.Parameters.AddWithValue("$rootPath", ProjectRootPath);
            cmd.Parameters.AddWithValue("$description", (object?)description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$createdAt", now.ToString("O"));
            cmd.Parameters.AddWithValue("$updatedAt", now.ToString("O"));
            cmd.ExecuteNonQuery();

            return new Project(id, projectName, ProjectRootPath, now, now, new List<Step>(), description);
        }
    }

    public void SaveStep(Step step)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentNullException.ThrowIfNull(step);

        lock (_syncLock)
        {
            const string sql = @"
                INSERT OR REPLACE INTO Steps (
                    Id, SequenceIndex, Timestamp, Action, ClickX, ClickY,
                    TargetName, TargetControlType, TargetAutomationId, TargetClassName,
                    TargetProcessName, TargetProcessId, TargetWindowTitle, TargetWindowHandle,
                    TargetBoundingBoxX, TargetBoundingBoxY, TargetBoundingBoxWidth, TargetBoundingBoxHeight,
                    TargetIsPassword, TargetFrameworkId,
                    ScreenshotRelativePath, Title, Description, MetadataJson
                )
                VALUES (
                    $id, $seq, $timestamp, $action, $clickX, $clickY,
                    $targetName, $targetControlType, $targetAutomationId, $targetClassName,
                    $targetProcessName, $targetProcessId, $targetWindowTitle, $targetWindowHandle,
                    $targetBbX, $targetBbY, $targetBbW, $targetBbH,
                    $targetIsPassword, $targetFrameworkId,
                    $screenshotPath, $title, $description, $metadataJson
                );
            ";

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("$id", step.Id.ToString());
            cmd.Parameters.AddWithValue("$seq", step.SequenceIndex);
            cmd.Parameters.AddWithValue("$timestamp", step.Timestamp.ToString("O"));
            cmd.Parameters.AddWithValue("$action", step.Action.ToString());
            cmd.Parameters.AddWithValue("$clickX", step.ClickX);
            cmd.Parameters.AddWithValue("$clickY", step.ClickY);

            var el = step.TargetElement;
            cmd.Parameters.AddWithValue("$targetName", (object?)el.Name ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$targetControlType", (object?)el.ControlType ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$targetAutomationId", (object?)el.AutomationId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$targetClassName", (object?)el.ClassName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$targetProcessName", (object?)el.ProcessName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$targetProcessId", el.ProcessId);
            cmd.Parameters.AddWithValue("$targetWindowTitle", (object?)el.WindowTitle ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$targetWindowHandle", el.WindowHandle);

            var bb = el.BoundingRectangle;
            cmd.Parameters.AddWithValue("$targetBbX", bb.X);
            cmd.Parameters.AddWithValue("$targetBbY", bb.Y);
            cmd.Parameters.AddWithValue("$targetBbW", bb.Width);
            cmd.Parameters.AddWithValue("$targetBbH", bb.Height);

            cmd.Parameters.AddWithValue("$targetIsPassword", el.IsPassword ? 1 : 0);
            cmd.Parameters.AddWithValue("$targetFrameworkId", (object?)el.FrameworkId ?? DBNull.Value);

            cmd.Parameters.AddWithValue("$screenshotPath", (object?)step.ScreenshotPath ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$title", (object?)step.Title ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$description", (object?)step.Description ?? DBNull.Value);

            string? metadataJson = step.Metadata != null ? JsonSerializer.Serialize(step.Metadata) : null;
            cmd.Parameters.AddWithValue("$metadataJson", (object?)metadataJson ?? DBNull.Value);

            cmd.ExecuteNonQuery();
        }
    }

    public Project? LoadProject()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        lock (_syncLock)
        {
            const string sql = "SELECT Id, Name, RootPath, Description, CreatedAt, UpdatedAt FROM Projects LIMIT 1;";

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = sql;
            using var reader = cmd.ExecuteReader();

            if (!reader.Read())
            {
                return null;
            }

            var id = Guid.Parse(reader.GetString(0));
            var name = reader.GetString(1);
            var rootPath = reader.GetString(2);
            var desc = reader.IsDBNull(3) ? null : reader.GetString(3);
            var createdAt = DateTime.Parse(reader.GetString(4));
            var updatedAt = DateTime.Parse(reader.GetString(5));

            var steps = LoadStepsInternal();

            return new Project(id, name, rootPath, createdAt, updatedAt, steps, desc);
        }
    }

    public IReadOnlyList<Step> LoadSteps()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        lock (_syncLock)
        {
            return LoadStepsInternal();
        }
    }

    public void UpdateStepDetails(Guid stepId, string? title, string? description)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        lock (_syncLock)
        {
            const string sql = @"
                UPDATE Steps 
                SET Title = $title, 
                    Description = $description 
                WHERE Id = $id;
            ";

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("$id", stepId.ToString());
            cmd.Parameters.AddWithValue("$title", (object?)title ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$description", (object?)description ?? DBNull.Value);

            cmd.ExecuteNonQuery();
        }
    }

    private List<Step> LoadStepsInternal()
    {
        const string sql = "SELECT * FROM Steps ORDER BY SequenceIndex ASC;";
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();

        var list = new List<Step>();

        while (reader.Read())
        {
            var id = Guid.Parse(reader.GetString(reader.GetOrdinal("Id")));
            var seq = reader.GetInt32(reader.GetOrdinal("SequenceIndex"));
            var timestamp = DateTime.Parse(reader.GetString(reader.GetOrdinal("Timestamp")));
            var action = Enum.Parse<ActionType>(reader.GetString(reader.GetOrdinal("Action")));
            var clickX = reader.GetDouble(reader.GetOrdinal("ClickX"));
            var clickY = reader.GetDouble(reader.GetOrdinal("ClickY"));

            var targetName = reader.IsDBNull(reader.GetOrdinal("TargetName")) ? string.Empty : reader.GetString(reader.GetOrdinal("TargetName"));
            var targetControlType = reader.IsDBNull(reader.GetOrdinal("TargetControlType")) ? "Unknown" : reader.GetString(reader.GetOrdinal("TargetControlType"));
            var targetAutomationId = reader.IsDBNull(reader.GetOrdinal("TargetAutomationId")) ? string.Empty : reader.GetString(reader.GetOrdinal("TargetAutomationId"));
            var targetClassName = reader.IsDBNull(reader.GetOrdinal("TargetClassName")) ? string.Empty : reader.GetString(reader.GetOrdinal("TargetClassName"));
            var targetProcessName = reader.IsDBNull(reader.GetOrdinal("TargetProcessName")) ? "Unknown" : reader.GetString(reader.GetOrdinal("TargetProcessName"));
            var targetProcessId = reader.GetInt32(reader.GetOrdinal("TargetProcessId"));
            var targetWindowTitle = reader.IsDBNull(reader.GetOrdinal("TargetWindowTitle")) ? string.Empty : reader.GetString(reader.GetOrdinal("TargetWindowTitle"));
            var targetWindowHandle = reader.GetInt64(reader.GetOrdinal("TargetWindowHandle"));

            var bbX = reader.GetDouble(reader.GetOrdinal("TargetBoundingBoxX"));
            var bbY = reader.GetDouble(reader.GetOrdinal("TargetBoundingBoxY"));
            var bbW = reader.GetDouble(reader.GetOrdinal("TargetBoundingBoxWidth"));
            var bbH = reader.GetDouble(reader.GetOrdinal("TargetBoundingBoxHeight"));

            bool isPassword = false;
            try
            {
                var isPasswordOrdinal = reader.GetOrdinal("TargetIsPassword");
                isPassword = !reader.IsDBNull(isPasswordOrdinal) && reader.GetInt32(isPasswordOrdinal) == 1;
            }
            catch (IndexOutOfRangeException) { }

            string frameworkId = "Unknown";
            try
            {
                var frameworkIdOrdinal = reader.GetOrdinal("TargetFrameworkId");
                frameworkId = reader.IsDBNull(frameworkIdOrdinal) ? "Unknown" : reader.GetString(frameworkIdOrdinal);
            }
            catch (IndexOutOfRangeException) { }

            var screenshotPath = reader.IsDBNull(reader.GetOrdinal("ScreenshotRelativePath")) ? null : reader.GetString(reader.GetOrdinal("ScreenshotRelativePath"));
            var title = reader.IsDBNull(reader.GetOrdinal("Title")) ? null : reader.GetString(reader.GetOrdinal("Title"));
            var description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description"));
            var metadataJson = reader.IsDBNull(reader.GetOrdinal("MetadataJson")) ? null : reader.GetString(reader.GetOrdinal("MetadataJson"));

            Dictionary<string, string>? metadata = null;
            if (!string.IsNullOrEmpty(metadataJson))
            {
                try { metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(metadataJson); } catch { }
            }

            var element = new ElementInfo(
                Name: targetName,
                ControlType: targetControlType,
                AutomationId: targetAutomationId,
                ClassName: targetClassName,
                ProcessName: targetProcessName,
                ProcessId: targetProcessId,
                WindowTitle: targetWindowTitle,
                WindowHandle: targetWindowHandle,
                BoundingRectangle: new BoundingBox(bbX, bbY, bbW, bbH),
                FrameworkId: frameworkId,
                IsPassword: isPassword
            );

            var step = new Step(
                Id: id,
                SequenceIndex: seq,
                Timestamp: timestamp,
                Action: action,
                ClickX: clickX,
                ClickY: clickY,
                TargetElement: element,
                ScreenshotPath: screenshotPath,
                Title: title,
                Description: description,
                Metadata: metadata
            );

            list.Add(step);
        }

        return list;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        if (_ownsConnection)
        {
            _connection.Dispose();
            SqliteConnection.ClearPool(_connection);
        }
    }
}
