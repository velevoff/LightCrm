using System.Text.Json.Serialization;

namespace LightCrm.Models;

// Контакт. Володіє своїми угодами.
public sealed class Contact {
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Company { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public List<Deal> Deals { get; set; } = new();

    [JsonIgnore] public string DisplayName => $"{Name} ({Company})";
    [JsonIgnore] public int DealsCount => Deals.Count;
    [JsonIgnore] public decimal TotalAmount => Deals.Sum(d => d.Amount);

    public void AddDeal(Deal deal) => Deals.Add(deal);
    public bool RemoveDeal(Deal deal) => Deals.Remove(deal);
}
