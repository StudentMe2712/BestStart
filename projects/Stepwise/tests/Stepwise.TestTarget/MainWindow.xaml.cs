using System.Windows;
using System.Windows.Controls;

namespace Stepwise.TestTarget;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void BtnAction_Click(object sender, RoutedEventArgs e)
    {
        statusText.Text = "Action Submitted: " + (txtStandard.Text ?? string.Empty);
    }

    private void BtnSecondary_Click(object sender, RoutedEventArgs e)
    {
        statusText.Text = "Secondary Action Clicked";
    }

    private void LstItems_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (lstItems.SelectedItem is ListBoxItem selectedItem)
        {
            statusText.Text = "Selected: " + selectedItem.Content;
        }
    }
}
