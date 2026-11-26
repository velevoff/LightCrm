namespace LightCrm.Models;

// Етапи воронки продажів
public enum DealStage { Lead, Qualified, Proposal, Negotiation, Won, Lost }

public static class DealStageExtensions {
    public static string ToTitle(this DealStage stage) => stage switch {
        DealStage.Lead => "Лід",
        DealStage.Qualified => "Кваліфікація",
        DealStage.Proposal => "Пропозиція",
        DealStage.Negotiation => "Переговори",
        DealStage.Won => "Виграно",
        DealStage.Lost => "Програно",
        _ => stage.ToString()
    };
}

// Елемент випадного списку етапів
public sealed record StageOption(DealStage? Stage, string Title);
