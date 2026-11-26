using Avalonia.Controls;
using Avalonia.Interactivity;
using LightCrm.ViewModels;

namespace LightCrm.Views;

public partial class ContactDialog : Window {
    public ContactDialog() {
        InitializeComponent();
    }

    public ContactDialog(ContactDialogViewModel vm) {
        InitializeComponent();
        DataContext = vm;
    }

    private void OnOk(object? sender, RoutedEventArgs e) {
        DialogViewModelBase? vm = DataContext as DialogViewModelBase;

        if (vm != null) {
            if (vm.Validate()) {
                Close(true);
            }
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) {
        Close(false);
    }
}
