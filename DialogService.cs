using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace OfflineMinecraftLauncher;

internal static class DialogService
{
    public static async Task ShowInfoAsync(Window owner, string title, string message)
    {
        var dialog = CreateDialog(title, message, includeCancel: false, out _, out var okButton);
        okButton.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(owner);
    }

    public static async Task<bool> ShowConfirmAsync(Window owner, string title, string message)
    {
        var dialog = CreateDialog(title, message, includeCancel: true, out var cancelButton, out var okButton);
        bool result = false;

        okButton.Click += (_, _) =>
        {
            result = true;
            dialog.Close();
        };
        cancelButton!.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(owner);
        return result;
    }

    private static Window CreateDialog(string title, string message, bool includeCancel, out Button? cancelButton, out Button okButton)
    {
        cancelButton = null;
        okButton = new Button
        {
            Content = includeCancel ? "Continue" : "OK",
            MinWidth = 96,
            Background = new SolidColorBrush(Color.Parse("#3ED6B4")),
            Foreground = Brushes.Black
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 12
        };

        if (includeCancel)
        {
            cancelButton = new Button
            {
                Content = "Cancel",
                MinWidth = 96
            };
            buttons.Children.Add(cancelButton);
        }

        buttons.Children.Add(okButton);

        return new Window
        {
            Title = title,
            Width = 460,
            Height = 220,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.Parse("#121623")),
            Content = new Border
            {
                Padding = new Thickness(20),
                Child = new StackPanel
                {
                    Spacing = 18,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = title,
                            FontSize = 20,
                            FontWeight = FontWeight.SemiBold,
                            Foreground = Brushes.White
                        },
                        new TextBlock
                        {
                            Text = message,
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = new SolidColorBrush(Color.Parse("#CDD5E4"))
                        },
                        buttons
                    }
                }
            }
        };
    }
}
