using System.Text.Json.Serialization;

namespace LightCrm.Models;

// Базовий клас взаємодії з клієнтом.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(Call), "call")]
[JsonDerivedType(typeof(Meeting), "meeting")]
[JsonDerivedType(typeof(EmailMessage), "email")]
public abstract class Interaction {
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Date { get; set; } = DateTime.Now;
    public string Description { get; set; } = "";

    // Назва типу для відображення в таблиці.
    [JsonIgnore] public abstract string TypeName { get; }

    // Короткий підсумок взаємодії; у кожному нащадку — свій.
    public abstract string GetSummary();

    [JsonIgnore] public string Summary => GetSummary();

    public override string ToString() => GetSummary();
}

public sealed class Call : Interaction, IScheduledAction {
    public string PhoneNumber { get; set; } = "";
    public int DurationMinutes { get; set; }
    public bool IsCompleted { get; set; } = true;

    [JsonIgnore]
    public DateTime ScheduledAt { get => Date; set => Date = value; }

    [JsonIgnore] public override string TypeName => "Дзвінок";

    public bool IsOverdue(DateTime now) => !IsCompleted && ScheduledAt < now;

    public void Reschedule(DateTime newTime) {
        ScheduledAt = newTime;
        IsCompleted = false;
    }

    public override string GetSummary() =>
        $"Дзвінок {Date:dd.MM.yyyy HH:mm}, {DurationMinutes} хв" +
        (string.IsNullOrWhiteSpace(PhoneNumber) ? "" : $" ({PhoneNumber})") +
        $": {Description}";
}

public sealed class Meeting : Interaction, IScheduledAction {
    public string Location { get; set; } = "";
    public bool IsCompleted { get; set; } = true;

    [JsonIgnore]
    public DateTime ScheduledAt { get => Date; set => Date = value; }

    [JsonIgnore] public override string TypeName => "Зустріч";

    public bool IsOverdue(DateTime now) => !IsCompleted && ScheduledAt < now;

    public void Reschedule(DateTime newTime) {
        ScheduledAt = newTime;
        IsCompleted = false;
    }

    public override string GetSummary() =>
        $"Зустріч {Date:dd.MM.yyyy HH:mm}, місце: {Location}: {Description}";
}

public sealed class EmailMessage : Interaction {
    public string Subject { get; set; } = "";
    public bool IsIncoming { get; set; }

    [JsonIgnore] public override string TypeName => "Лист";

    public override string GetSummary() =>
        $"{(IsIncoming ? "Вхідний" : "Вихідний")} лист {Date:dd.MM.yyyy HH:mm}, тема «{Subject}»: {Description}";
}
