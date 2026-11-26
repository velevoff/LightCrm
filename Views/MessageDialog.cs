using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace LightCrm.Views;

// Просте модальне вікно повідомлення / підтвердження.
public sealed class MessageDialog : Window {
    public MessageDialog(string title, string message, bool confirm) {
        Title = title;
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        MaxWidth = 560;

        var text = new TextBlock();
        text.Text = message;
        text.TextWrapping = TextWrapping.Wrap;
        text.MaxWidth = 520;

        var ok = new Button();
        if (confirm) {
            ok.Content = "Так";
        }
        else {
            ok.Content = "OK";
        }
        ok.MinWidth = 80;
        ok.IsDefault = true;
        ok.Click += (_, _) => {
            Close(true);
        };

        var buttons = new StackPanel();
        buttons.Orientation = Orientation.Horizontal;
        buttons.HorizontalAlignment = HorizontalAlignment.Right;
        buttons.Spacing = 8;
        buttons.Children.Add(ok);

        if (confirm) {
            var no = new Button();
            no.Content = "Ні";
            no.MinWidth = 80;
            no.IsCancel = true;
            no.Click += (_, _) => {
                Close(false);
            };

            buttons.Children.Add(no);
        }

        var mainPanel = new StackPanel();
        mainPanel.Margin = new Thickness(20);
        mainPanel.Spacing = 18;
        mainPanel.Children.Add(text);
        mainPanel.Children.Add(buttons);

        Content = mainPanel;
    }
}
