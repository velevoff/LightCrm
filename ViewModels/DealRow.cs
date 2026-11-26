using LightCrm.Models;

namespace LightCrm.ViewModels;

// Рядок таблиці угод: угода + її контакт.
public sealed class DealRow
{
    public DealRow(Contact contact, Deal deal) { Contact = contact; Deal = deal; }

    public Contact Contact { get; }
    public Deal Deal { get; }

    public string ContactName => Contact.Name;
    public string Company => Contact.Company;
    public string Title => Deal.Title;
    public decimal Amount => Deal.Amount;
    public DealStage Stage => Deal.Stage;
    public string StageTitle => Deal.Stage.ToTitle();
    public DateTime Date => Deal.Date;
    public int InteractionsCount => Deal.Interactions.Count;
    public DateTime LastActivity => Deal.LastActivity;
}
