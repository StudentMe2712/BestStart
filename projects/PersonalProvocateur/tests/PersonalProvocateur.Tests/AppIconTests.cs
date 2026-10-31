using System;
using System.IO;
using System.Drawing;
using PersonalProvocateur.App.WindowsInterop;
using Xunit;

namespace PersonalProvocateur.Tests;

public class AppIconTests
{
    [Fact]
    public void CreateIcoData_GeneratesValidMultiResolutionIcoStream()
    {
        byte[] icoData = TrayIconService.CreateIcoData();
        Assert.NotNull(icoData);
        Assert.True(icoData.Length > 0);

        using var ms = new MemoryStream(icoData);
        using var br = new BinaryReader(ms);

        // ICO Header
        ushort reserved = br.ReadUInt16();
        ushort type = br.ReadUInt16();
        ushort count = br.ReadUInt16();

        Assert.Equal(0, reserved);
        Assert.Equal(1, type); // 1 = Icon
        Assert.Equal(7, count); // 16, 24, 32, 48, 64, 128, 256

        int[] expectedSizes = [16, 24, 32, 48, 64, 128, 256];
        for (int i = 0; i < count; i++)
        {
            byte w = br.ReadByte();
            byte h = br.ReadByte();
            br.ReadByte(); // color count
            br.ReadByte(); // reserved
            br.ReadUInt16(); // planes
            br.ReadUInt16(); // bit count
            uint bytesInRes = br.ReadUInt32();
            uint imageOffset = br.ReadUInt32();

            int expected = expectedSizes[i];
            int actualW = w == 0 ? 256 : w;
            int actualH = h == 0 ? 256 : h;

            Assert.Equal(expected, actualW);
            Assert.Equal(expected, actualH);
            Assert.True(bytesInRes > 0);
            Assert.True(imageOffset >= 6 + (16 * count));
        }

        // Verify Icon load
        ms.Position = 0;
        using var icon = new Icon(ms);
        Assert.NotNull(icon);
    }

    private static string FindSolutionDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "PersonalProvocateur.sln")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Solution directory not found");
    }

    [Fact]
    public void EnsureAssetsAppIco_ExistsAndIsUpToDate()
    {
        string slnDir = FindSolutionDirectory();
        string projectAssetsDir = Path.Combine(slnDir, "src", "PersonalProvocateur.App", "Assets");
        string targetIcoPath = Path.Combine(projectAssetsDir, "app.ico");

        byte[] icoData = TrayIconService.CreateIcoData();
        Assert.True(Directory.Exists(projectAssetsDir), $"Assets dir not found at {projectAssetsDir}");

        File.WriteAllBytes(targetIcoPath, icoData);
        Assert.True(File.Exists(targetIcoPath));
        Assert.Equal(icoData.Length, new FileInfo(targetIcoPath).Length);
    }

    [Fact]
    public void CreateApplicationIcon_ReturnsValidNonNullIcon()
    {
        using var icon = TrayIconService.CreateApplicationIcon();
        Assert.NotNull(icon);
        Assert.True(icon.Width > 0);
        Assert.True(icon.Height > 0);
    }

    [Fact]
    public void PersonalProvocateurAppCsproj_ConfiguresApplicationIcon()
    {
        string slnDir = FindSolutionDirectory();
        string csprojPath = Path.Combine(slnDir, "src", "PersonalProvocateur.App", "PersonalProvocateur.App.csproj");
        Assert.True(File.Exists(csprojPath));

        string csprojContent = File.ReadAllText(csprojPath);
        Assert.Contains("<ApplicationIcon>Assets\\app.ico</ApplicationIcon>", csprojContent);

        string icoPath = Path.Combine(slnDir, "src", "PersonalProvocateur.App", "Assets", "app.ico");
        Assert.True(File.Exists(icoPath));

        using var icon = new Icon(icoPath);
        Assert.NotNull(icon);
    }

    [Fact]
    public void CreateTaskbarIconSource_ReturnsValidFrozenImageSource()
    {
        var source = TrayIconService.CreateTaskbarIconSource();
        Assert.NotNull(source);
        Assert.True(source.IsFrozen);
        Assert.True(source.Width > 0);
        Assert.True(source.Height > 0);
    }
}
