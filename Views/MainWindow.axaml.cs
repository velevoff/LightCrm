using Avalonia.Controls;
using Avalonia.Platform.Storage;
using LightCrm.Models;
using LightCrm.ViewModels;

namespace LightCrm.Views;

public partial class MainWindow : Window, IDialogService {
    private readonly MainViewModel _vm;

    public MainWindow() {
        InitializeComponent();

        _vm = new MainViewModel(this);
        DataContext = _vm;

        Opened += async (_, _) => {
            await _vm.InitializeAsync();
        };

        Closing += (_, _) => {
            _vm.CancelCommand.Execute(null);
        };
    }

    // ----------------------------------------------------------- файли
    public async Task<string?> PickOpenJsonAsync() {
        var options = new FilePickerOpenOptions();
        options.Title = "Імпорт бази з JSON";
        options.AllowMultiple = false;
        options.FileTypeFilter = new[] {
            new FilePickerFileType("JSON") {
                Patterns = new[] { "*.json" }
            }
        };

        var files = await StorageProvider.OpenFilePickerAsync(options);

        if (files.Count > 0) {
            return files[0].TryGetLocalPath();
        }

        return null;
    }

    public async Task<string?> PickSaveAsync(string title, string suggestedName, string extension) {
        var fileType = new FilePickerFileType(extension.ToUpperInvariant()) {
            Patterns = new[] { "*." + extension }
        };

        var options = new FilePickerSaveOptions();
        options.Title = title;
        options.SuggestedFileName = suggestedName;
        options.DefaultExtension = extension;
        options.ShowOverwritePrompt = true;
        options.FileTypeChoices = new[] { fileType };

        var file = await StorageProvider.SaveFilePickerAsync(options);

        if (file != null) {
            return file.TryGetLocalPath();
        }

        return null;
    }

    // ----------------------------------------------------------- діалоги редагування
    public async Task<ContactDialogViewModel?> EditContactAsync(Contact? existing) {
        var vm = new ContactDialogViewModel(existing);
        var dialog = new ContactDialog(vm);
        bool result = await dialog.ShowDialog<bool>(this);

        if (result) {
            return vm;
        }

        return null;
    }

    public async Task<DealDialogViewModel?> EditDealAsync(IReadOnlyList<Contact> contacts, DealRow? existing) {
        var vm = new DealDialogViewModel(contacts, existing);
        var dialog = new DealDialog(vm);
        bool result = await dialog.ShowDialog<bool>(this);

        if (result) {
            return vm;
        }

        return null;
    }

    public async Task<InteractionDialogViewModel?> EditInteractionAsync(Interaction? existing) {
        var vm = new InteractionDialogViewModel(existing);
        var dialog = new InteractionDialog(vm);
        bool result = await dialog.ShowDialog<bool>(this);

        if (result) {
            return vm;
        }

        return null;
    }

    // ----------------------------------------------------------- повідомлення
    public Task<bool> ConfirmAsync(string title, string message) {
        var dialog = new MessageDialog(title, message, true);
        return dialog.ShowDialog<bool>(this);
    }

    public Task ShowMessageAsync(string title, string message) {
        var dialog = new MessageDialog(title, message, false);
        return dialog.ShowDialog<bool>(this);
    }
}
