using System.Text.Json.Serialization;

namespace LightCrm.Models;

// Угода. Володіє історією взаємодій.
public sealed class Deal {
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public decimal Amount { get; set; }
    public DealStage Stage { get; set; } = DealStage.Lead;
    public DateTime Date { get; set; } = DateTime.Today;
    public List<Interaction> Interactions { get; set; } = new();

    [JsonIgnore] public bool IsOpen => Stage is not (DealStage.Won or DealStage.Lost);

    // Дата останньої взаємодії.
    [JsonIgnore]
    public DateTime LastActivity {
        get {
            var last = Date;
            foreach (var i in Interactions)
                if (i.Date > last) last = i.Date;
            return last;
        }
    }

    // «Зависла» угода: відкрита і без взаємодій понад декілька днів.
    public bool IsStale(DateTime now, int days) => IsOpen && (now - LastActivity).TotalDays > days;

    public void AddInteraction(Interaction interaction) => Interactions.Add(interaction);
    public bool RemoveInteraction(Interaction interaction) => Interactions.Remove(interaction);
}
