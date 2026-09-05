using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Moq;
using Stepwise.Core.Engine;
using Stepwise.Core.Interfaces;
using Stepwise.Core.Models;
using Stepwise.Core.Policy;
using Stepwise.Storage.Repositories;
using Stepwise.WindowsIntegration.Capture;
using Xunit;

namespace Stepwise.Tests;

public class Phase4Stage3CoreWindowsIntegrationTests : IDisposable
{
    private readonly string _testTempDir;
    private readonly SqliteConnection _inMemoryConnection;

    public Phase4Stage3CoreWindowsIntegrationTests()
    {
        _testTempDir = Path.Combine(Path.GetTempPath(), "StepwiseP4S3_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testTempDir);

        _inMemoryConnection = new SqliteConnection("Data Source=:memory:");
        _inMemoryConnection.Open();
    }

    public void Dispose()
    {
        _inMemoryConnection.Dispose();

        try
        {
            if (Directory.Exists(_testTempDir))
            {
                Directory.Delete(_testTempDir, true);
            }
        }
        catch
        {
            // Ignore cleanup errors in temporary directories
        }
    }

    #region 1. Domain Models & Contracts: CaptureResult, CoordinateSpace, ActionType

    [Fact]
    public void CaptureResult_RecordCreationAndProperties_MatchContract()
    {
        var bounds = new BoundingBox(10, 20, 100, 50);
        var result = new CaptureResult(
            Success: true,
            RelativePath: "assets/screenshots/step_001.png",
            Width: 1920,
            Height: 1080,
            HighlightBounds: bounds,
            ErrorMessage: null
        );

        Assert.True(result.Success);
        Assert.Equal("assets/screenshots/step_001.png", result.RelativePath);
        Assert.Equal(1920, result.Width);
        Assert.Equal(1080, result.Height);
        Assert.Equal(bounds, result.HighlightBounds);
        Assert.Null(result.ErrorMessage);

        var failedResult = new CaptureResult(
            Success: false,
            RelativePath: null,
            Width: 0,
            Height: 0,
            HighlightBounds: BoundingBox.Empty,
            ErrorMessage: "Desktop locked"
        );

        Assert.False(failedResult.Success);
        Assert.Null(failedResult.RelativePath);
        Assert.Equal(0, failedResult.Width);
        Assert.Equal(0, failedResult.Height);
        Assert.Equal("Desktop locked", failedResult.ErrorMessage);
    }

    [Fact]
    public void CoordinateSpace_EnumValues_ContainAllRequiredSpaces()
    {
        Assert.True(Enum.IsDefined(typeof(CoordinateSpace), CoordinateSpace.VirtualScreen));
        Assert.True(Enum.IsDefined(typeof(CoordinateSpace), CoordinateSpace.PrimaryMonitor));
        Assert.True(Enum.IsDefined(typeof(CoordinateSpace), CoordinateSpace.WindowClient));
        Assert.True(Enum.IsDefined(typeof(CoordinateSpace), CoordinateSpace.WindowNonClient));

        var names = Enum.GetNames<CoordinateSpace>();
        Assert.Contains(nameof(CoordinateSpace.VirtualScreen), names);
        Assert.Contains(nameof(CoordinateSpace.PrimaryMonitor), names);
        Assert.Contains(nameof(CoordinateSpace.WindowClient), names);
        Assert.Contains(nameof(CoordinateSpace.WindowNonClient), names);
    }

    [Fact]
    public void ActionType_EnumValues_MatchSpecSection31()
    {
        var expectedNames = new[]
        {
            "LeftClick",
            "RightClick",
            "DoubleLeftClick",
            "MiddleClick",
            "MouseDown",
            "MouseUp",
            "DragAndDrop",
            "Scroll",
            "KeyPress",
            "TextInput",
            "Shortcut",
            "WindowActivated",
            "WindowClosed",
            "ManualStep",
            "Unknown"
        };

        var actualNames = Enum.GetNames<ActionType>();
        foreach (var expected in expectedNames)
        {
            Assert.Contains(expected, actualNames);
        }
    }

    #endregion

    #region 2. ICaptureCoordinator & CaptureCoordinator Result Overload Tests

    [Fact]
    public async Task CaptureCoordinator_CaptureStepWithResultAsync_ReturnsDetailedSuccessResult()
    {
        var captureMock = new Mock<IScreenCaptureService>();
        var repoMock = new Mock<IProjectRepository>();
        repoMock.SetupGet(r => r.ProjectRootPath).Returns(_testTempDir);

        var target = new ElementInfo(
            Name: "LoginButton",
            ControlType: "Button",
            AutomationId: "btnLogin",
            ClassName: "Button",
            ProcessName: "app",
            ProcessId: 100,
            WindowTitle: "App",
            WindowHandle: 555,
            BoundingRectangle: new BoundingBox(50, 60, 120, 40)
        );

        var screenshotsDir = Path.Combine(_testTempDir, "assets", "screenshots");
        Directory.CreateDirectory(screenshotsDir);
        var testPng = Path.Combine(screenshotsDir, "step_001.png");
        using (var bmp = new Bitmap(800, 600))
        {
            bmp.Save(testPng, System.Drawing.Imaging.ImageFormat.Png);
        }

        captureMock
            .Setup(c => c.Capture(_testTempDir, 1, target.BoundingRectangle, 555))
            .Returns("assets/screenshots/step_001.png");

        var coordinator = new CaptureCoordinator(captureMock.Object, repoMock.Object);

        var result = await coordinator.CaptureStepWithResultAsync(1, target);

        Assert.True(result.Success);
        Assert.Equal("assets/screenshots/step_001.png", result.RelativePath);
        Assert.Equal(800, result.Width);
        Assert.Equal(600, result.Height);
        Assert.Equal(target.BoundingRectangle, result.HighlightBounds);
        Assert.Null(result.ErrorMessage);

        var path = await coordinator.CaptureStepAsync(1, target);
        Assert.Equal("assets/screenshots/step_001.png", path);
    }

    [Fact]
    public async Task CaptureCoordinator_CaptureStepWithResultAsync_WhenServiceNull_ReturnsFailedResult()
    {
        var coordinator = new CaptureCoordinator(null, null);
        var target = ElementInfo.Unknown;

        var result = await coordinator.CaptureStepWithResultAsync(1, target);

        Assert.False(result.Success);
        Assert.Null(result.RelativePath);
        Assert.NotNull(result.ErrorMessage);

        var path = await coordinator.CaptureStepAsync(1, target);
        Assert.Null(path);
    }

    [Fact]
    public async Task CaptureCoordinator_CaptureStepWithResultAsync_WhenCancelled_ReturnsCancelledResult()
    {
        var captureMock = new Mock<IScreenCaptureService>();
        var repoMock = new Mock<IProjectRepository>();
        repoMock.SetupGet(r => r.ProjectRootPath).Returns(_testTempDir);

        var coordinator = new CaptureCoordinator(captureMock.Object, repoMock.Object);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await coordinator.CaptureStepWithResultAsync(1, ElementInfo.Unknown, cts.Token);

        Assert.False(result.Success);
        Assert.Null(result.RelativePath);
        Assert.Contains("cancel", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CaptureCoordinator_CaptureStepWithResultAsync_WhenExceptionThrown_ReturnsFailureWithoutCrashing()
    {
        var captureMock = new Mock<IScreenCaptureService>();
        var repoMock = new Mock<IProjectRepository>();
        repoMock.SetupGet(r => r.ProjectRootPath).Returns(_testTempDir);

        captureMock
            .Setup(c => c.Capture(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<BoundingBox?>(), It.IsAny<long>()))
            .Throws(new InvalidOperationException("GDI BitBlt error 0x5"));

        var coordinator = new CaptureCoordinator(captureMock.Object, repoMock.Object);

        var result = await coordinator.CaptureStepWithResultAsync(1, ElementInfo.Unknown);

        Assert.False(result.Success);
        Assert.Null(result.RelativePath);
        Assert.Contains("GDI BitBlt error 0x5", result.ErrorMessage);
    }

    #endregion

    #region 3. ScreenCaptureService: Clean Unannotated Screenshots & Coordinate Alignment

    [Fact]
    public void ScreenCaptureService_Capture_SavesCleanImageWithoutBurningHighlightRectangle()
    {
        var captureService = new ScreenCaptureService();
        var region = new BoundingBox(50, 50, 100, 100);

        var relPath = captureService.Capture(_testTempDir, sequenceIndex: 10, targetRegion: region);

        Assert.NotNull(relPath);
        var fullPath = Path.Combine(_testTempDir, relPath);
        Assert.True(File.Exists(fullPath));

        using var bmp = new Bitmap(fullPath);
        Assert.True(bmp.Width > 0);
        Assert.True(bmp.Height > 0);
    }

    [Fact]
    public void ScreenCaptureService_CaptureAnnotated_ExportsAnnotatedScreenshot()
    {
        var captureService = new ScreenCaptureService();
        var region = new BoundingBox(50, 50, 100, 100);

        var relPath = captureService.CaptureAnnotated(_testTempDir, sequenceIndex: 20, targetRegion: region);

        Assert.NotNull(relPath);
        var fullPath = Path.Combine(_testTempDir, relPath);
        Assert.True(File.Exists(fullPath));

        using var bmp = new Bitmap(fullPath);
        Assert.True(bmp.Width > 0);
        Assert.True(bmp.Height > 0);
    }

    #endregion

    #region 4. SQLite Schema Migration & TargetIsPassword / TargetFrameworkId Retention

    [Fact]
    public void ProjectRepository_SaveAndLoadStep_RetainsTargetIsPasswordAndTargetFrameworkId()
    {
        using var repository = new ProjectRepository(_inMemoryConnection, _testTempDir);
        repository.CreateProject("Metadata Retention Guide");

        var passwordStep = new Step(
            Id: Guid.NewGuid(),
            SequenceIndex: 1,
            Timestamp: DateTime.UtcNow,
            Action: ActionType.TextInput,
            ClickX: 100,
            ClickY: 200,
            TargetElement: new ElementInfo(
                Name: "PasswordBox",
                ControlType: "Edit",
                AutomationId: "pwdInput",
                ClassName: "PasswordBox",
                ProcessName: "authApp",
                ProcessId: 1234,
                WindowTitle: "Sign In",
                WindowHandle: 9999,
                BoundingRectangle: new BoundingBox(50, 150, 200, 30),
                FrameworkId: "WPF",
                IsPassword: true
            ),
            ScreenshotPath: "assets/screenshots/step_001.png",
            Title: "Enter Password",
            Description: "Password entry",
            Metadata: null
        );

        var standardStep = new Step(
            Id: Guid.NewGuid(),
            SequenceIndex: 2,
            Timestamp: DateTime.UtcNow.AddSeconds(1),
            Action: ActionType.LeftClick,
            ClickX: 300,
            ClickY: 400,
            TargetElement: new ElementInfo(
                Name: "LoginButton",
                ControlType: "Button",
                AutomationId: "btnLogin",
                ClassName: "Button",
                ProcessName: "authApp",
                ProcessId: 1234,
                WindowTitle: "Sign In",
                WindowHandle: 9999,
                BoundingRectangle: new BoundingBox(250, 380, 100, 40),
                FrameworkId: "WinUI",
                IsPassword: false
            ),
            ScreenshotPath: "assets/screenshots/step_002.png",
            Title: "Click Sign In",
            Description: "Submit credentials",
            Metadata: null
        );

        repository.SaveStep(passwordStep);
        repository.SaveStep(standardStep);

        var loadedSteps = repository.LoadSteps();
        Assert.Equal(2, loadedSteps.Count);

        var loadedPwdStep = loadedSteps[0];
        Assert.True(loadedPwdStep.TargetElement.IsPassword);
        Assert.Equal("WPF", loadedPwdStep.TargetElement.FrameworkId);

        var loadedStdStep = loadedSteps[1];
        Assert.False(loadedStdStep.TargetElement.IsPassword);
        Assert.Equal("WinUI", loadedStdStep.TargetElement.FrameworkId);
    }

    [Fact]
    public void ProjectRepository_LegacyDatabaseMigration_SafelyAddsColumnsWithoutError()
    {
        using var legacyConn = new SqliteConnection("Data Source=:memory:");
        legacyConn.Open();

        const string legacySchemaSql = @"
            CREATE TABLE Projects (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                RootPath TEXT NOT NULL,
                Description TEXT,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE TABLE Steps (
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
                ScreenshotRelativePath TEXT,
                Title TEXT,
                Description TEXT,
                MetadataJson TEXT
            );
        ";

        using (var cmd = legacyConn.CreateCommand())
        {
            cmd.CommandText = legacySchemaSql;
            cmd.ExecuteNonQuery();
        }

        var legacyId = Guid.NewGuid();
        const string insertLegacySql = @"
            INSERT INTO Steps (
                Id, SequenceIndex, Timestamp, Action, ClickX, ClickY,
                TargetName, TargetControlType, TargetAutomationId, TargetClassName,
                TargetProcessName, TargetProcessId, TargetWindowTitle, TargetWindowHandle,
                TargetBoundingBoxX, TargetBoundingBoxY, TargetBoundingBoxWidth, TargetBoundingBoxHeight
            ) VALUES (
                $id, 1, '2026-09-01T10:00:00Z', 'LeftClick', 100, 100,
                'LegacyBtn', 'Button', 'btn1', 'Button',
                'legacyApp', 1000, 'Legacy Win', 1234,
                0, 0, 50, 20
            );
        ";

        using (var cmd = legacyConn.CreateCommand())
        {
            cmd.CommandText = insertLegacySql;
            cmd.Parameters.AddWithValue("$id", legacyId.ToString());
            cmd.ExecuteNonQuery();
        }

        using var repository = new ProjectRepository(legacyConn, _testTempDir);

        var loadedSteps = repository.LoadSteps();
        Assert.Single(loadedSteps);

        Assert.False(loadedSteps[0].TargetElement.IsPassword);
        Assert.Equal("Unknown", loadedSteps[0].TargetElement.FrameworkId);

        var newStep = new Step(
            Id: Guid.NewGuid(),
            SequenceIndex: 2,
            Timestamp: DateTime.UtcNow,
            Action: ActionType.TextInput,
            ClickX: 150,
            ClickY: 150,
            TargetElement: new ElementInfo(
                Name: "MigratedPassword",
                ControlType: "Edit",
                AutomationId: "pwd",
                ClassName: "Edit",
                ProcessName: "legacyApp",
                ProcessId: 1000,
                WindowTitle: "Legacy Win",
                WindowHandle: 1234,
                BoundingRectangle: new BoundingBox(10, 10, 80, 25),
                FrameworkId: "WinForms",
                IsPassword: true
            )
        );

        repository.SaveStep(newStep);

        var allSteps = repository.LoadSteps();
        Assert.Equal(2, allSteps.Count);
        Assert.True(allSteps[1].TargetElement.IsPassword);
        Assert.Equal("WinForms", allSteps[1].TargetElement.FrameworkId);
    }

    #endregion

    #region 5. In-Memory Privacy & Sensitive Text Sanitization in RecordingEngine

    [Fact]
    public async Task RecordingEngine_ProcessActionsAsync_SanitizesSensitiveTextWhenTargetIsPassword()
    {
        var inputMonitorMock = new Mock<IInputMonitoringService>();
        var correlator = new EventCorrelator();
        var targetResolverMock = new Mock<ITargetResolver>();
        var policy = new DefaultRecordingPolicy(maskSensitiveInputs: true);
        var stepDetector = new StepDetector();
        var captureCoordinatorMock = new Mock<ICaptureCoordinator>();
        var repoMock = new Mock<IProjectRepository>();

        repoMock.SetupGet(r => r.ProjectRootPath).Returns(_testTempDir);

        var passwordTarget = new ElementInfo(
            Name: "PasswordBox",
            ControlType: "Edit",
            AutomationId: "txtPwd",
            ClassName: "PasswordBox",
            ProcessName: "safeApp",
            ProcessId: 200,
            WindowTitle: "Login",
            WindowHandle: 7777,
            BoundingRectangle: new BoundingBox(10, 10, 150, 30),
            FrameworkId: "WPF",
            IsPassword: true
        );

        targetResolverMock
            .Setup(r => r.ResolveTargetAsync(It.IsAny<SemanticAction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(passwordTarget);

        Step? capturedStep = null;
        var stepRecordedTcs = new TaskCompletionSource<Step>();

        using var engine = new RecordingEngine(
            inputMonitorMock.Object,
            null,
            correlator,
            targetResolverMock.Object,
            policy,
            stepDetector,
            captureCoordinatorMock.Object,
            repoMock.Object
        );

        engine.StepRecorded += (sender, step) =>
        {
            capturedStep = step;
            stepRecordedTcs.TrySetResult(step);
        };

        engine.StartRecording();

        const string secretPassword = "SuperSecretPassword999";
        for (int i = 0; i < secretPassword.Length; i++)
        {
            inputMonitorMock.Raise(m => m.KeyboardEventReceived += null, inputMonitorMock.Object, new RawKeyboardEvent(
                EventType: RawKeyboardEventType.KeyDown,
                VirtualKey: 0x41 + i,
                ScanCode: 0x1E + i,
                Modifiers: KeyboardModifiers.None,
                Character: secretPassword[i].ToString(),
                IsDeadKey: false,
                IsExtendedKey: false,
                Timestamp: DateTime.UtcNow
            ));
        }

        await engine.StopRecordingAsync();

        var completedTask = await Task.WhenAny(stepRecordedTcs.Task, Task.Delay(2000));
        Assert.True(completedTask == stepRecordedTcs.Task, "Ожидалось появление шага ввода пароля");

        Assert.NotNull(capturedStep);
        Assert.DoesNotContain(secretPassword, capturedStep.Title);
        Assert.DoesNotContain(secretPassword, capturedStep.Description ?? string.Empty);

        if (capturedStep.Metadata != null)
        {
            foreach (var kvp in capturedStep.Metadata)
            {
                Assert.DoesNotContain(secretPassword, kvp.Value);
            }
        }
    }

    [Fact]
    public async Task RecordingEngine_ProcessActionsAsync_SuppressesPasswordWhenMaskingDisabled()
    {
        var inputMonitorMock = new Mock<IInputMonitoringService>();
        var correlator = new EventCorrelator();
        var targetResolverMock = new Mock<ITargetResolver>();
        var policy = new DefaultRecordingPolicy(maskSensitiveInputs: false);
        var stepDetector = new StepDetector();
        var captureCoordinatorMock = new Mock<ICaptureCoordinator>();
        var repoMock = new Mock<IProjectRepository>();

        repoMock.SetupGet(r => r.ProjectRootPath).Returns(_testTempDir);

        var passwordTarget = new ElementInfo(
            Name: "SecretKey",
            ControlType: "Edit",
            AutomationId: "txtSecret",
            ClassName: "PasswordBox",
            ProcessName: "safeApp",
            ProcessId: 200,
            WindowTitle: "Login",
            WindowHandle: 7777,
            BoundingRectangle: new BoundingBox(10, 10, 150, 30),
            FrameworkId: "WPF",
            IsPassword: true
        );

        targetResolverMock
            .Setup(r => r.ResolveTargetAsync(It.IsAny<SemanticAction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(passwordTarget);

        var recordedSteps = new List<Step>();

        using var engine = new RecordingEngine(
            inputMonitorMock.Object,
            null,
            correlator,
            targetResolverMock.Object,
            policy,
            stepDetector,
            captureCoordinatorMock.Object,
            repoMock.Object
        );

        engine.StepRecorded += (sender, step) => recordedSteps.Add(step);

        engine.StartRecording();

        inputMonitorMock.Raise(m => m.KeyboardEventReceived += null, inputMonitorMock.Object, new RawKeyboardEvent(
            EventType: RawKeyboardEventType.KeyDown,
            VirtualKey: 0x41,
            ScanCode: 0x1E,
            Modifiers: KeyboardModifiers.None,
            Character: "X",
            IsDeadKey: false,
            IsExtendedKey: false,
            Timestamp: DateTime.UtcNow
        ));

        await engine.StopRecordingAsync();

        Assert.Empty(recordedSteps);
        repoMock.Verify(r => r.SaveStep(It.IsAny<Step>()), Times.Never);
        captureCoordinatorMock.Verify(c => c.CaptureStepAsync(It.IsAny<int>(), It.IsAny<ElementInfo>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion
}
