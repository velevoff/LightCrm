using LightCrm.Models;

namespace LightCrm.ViewModels;

// Абстракція діалогів (реалізує MainWindow), щоб ViewModel не залежала від View.
public interface IDialogService
{
    Task<string?> PickOpenJsonAsync();
    Task<string?> PickSaveAsync(string title, string suggestedName, string extension);
    Task<ContactDialogViewModel?> EditContactAsync(Contact? existing);
    Task<DealDialogViewModel?> EditDealAsync(IReadOnlyList<Contact> contacts, DealRow? existing);
    Task<InteractionDialogViewModel?> EditInteractionAsync(Interaction? existing);
    Task<bool> ConfirmAsync(string title, string message);
    Task ShowMessageAsync(string title, string message);
}
