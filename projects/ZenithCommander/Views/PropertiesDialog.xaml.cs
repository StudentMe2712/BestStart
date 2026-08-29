using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using NexusCommander.Models;

namespace NexusCommander.Views;

public partial class PropertiesDialog : Window
{
    public PropertiesDialog(FileSystemItem item)
    {
        InitializeComponent();
        LoadItemProperties(item);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void LoadItemProperties(FileSystemItem item)
    {
        IconTextBlock.Text = item.IconGlyph;
        NameTextBlock.Text = item.Name;
        TypeTextBlock.Text = item.ItemType;
        LocationTextBox.Text = Path.GetDirectoryName(item.FullPath) ?? item.FullPath;

        if (item.IsDirectory)
        {
            try
            {
                var dir = new DirectoryInfo(item.FullPath);
                CreatedTextBlock.Text = dir.CreationTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                ModifiedTextBlock.Text = dir.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                AccessedTextBlock.Text = dir.LastAccessTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                AttributesTextBlock.Text = dir.Attributes.ToString();
                SizeTextBlock.Text = "Calculating...";
                ContainsTextBlock.Text = "Calculating...";

                // Async calculation of directory size & contents count
                _ = Task.Run(() =>
                {
                    long totalSize = 0;
                    int fileCount = 0;
                    int dirCount = 0;

                    try
                    {
                        var options = new EnumerationOptions
                        {
                            IgnoreInaccessible = true,
                            RecurseSubdirectories = true
                        };

                        foreach (var file in dir.EnumerateFiles("*", options))
                        {
                            fileCount++;
                            totalSize += file.Length;
                        }

                        foreach (var _ in dir.EnumerateDirectories("*", options))
                        {
                            dirCount++;
                        }

                        string sizeStr = $"{FileSystemItem.FormatFileSize(totalSize)} ({totalSize:N0} bytes)";
                        string containsStr = $"{fileCount:N0} Files, {dirCount:N0} Folders";

                        Dispatcher.Invoke(() =>
                        {
                            SizeTextBlock.Text = sizeStr;
                            ContainsTextBlock.Text = containsStr;
                        });
                    }
                    catch
                    {
                        Dispatcher.Invoke(() =>
                        {
                            SizeTextBlock.Text = "Unknown";
                            ContainsTextBlock.Text = "Access restricted";
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                SizeTextBlock.Text = "Error: " + ex.Message;
                ContainsLabel.Visibility = Visibility.Collapsed;
                ContainsTextBlock.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            ContainsLabel.Visibility = Visibility.Collapsed;
            ContainsTextBlock.Visibility = Visibility.Collapsed;

            try
            {
                var file = new FileInfo(item.FullPath);
                SizeTextBlock.Text = $"{FileSystemItem.FormatFileSize(file.Length)} ({file.Length:N0} bytes)";
                CreatedTextBlock.Text = file.CreationTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                ModifiedTextBlock.Text = file.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                AccessedTextBlock.Text = file.LastAccessTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                AttributesTextBlock.Text = file.Attributes.ToString();
            }
            catch (Exception ex)
            {
                SizeTextBlock.Text = "Error: " + ex.Message;
            }
        }
    }
}
