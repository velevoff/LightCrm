using System.Xml.Serialization;

namespace LightCrm.Models;

// Звіт "Воронка продажів" (серіалізується в XML).
[XmlRoot("SalesFunnel")]
public sealed class FunnelReport {
    [XmlAttribute("title")] public string Title { get; set; } = "Воронка продажів";
    [XmlAttribute("generatedAt")] public DateTime GeneratedAt { get; set; }
    public int TotalDeals { get; set; }
    public decimal TotalAmount { get; set; }

    [XmlArray("Stages")]
    [XmlArrayItem("Stage")]
    public List<FunnelStage> Stages { get; set; } = new();
}

public sealed class FunnelStage {
    [XmlAttribute("code")] public string Code { get; set; } = "";
    [XmlAttribute("title")] public string Title { get; set; } = "";

    // Скільки угод зараз перебуває саме на цьому етапі.
    public int DealsCount { get; set; }
    public decimal TotalAmount { get; set; }

    // Скільки угод дійшло щонайменше до цього етапу (кумулятивно, без «Програно»).
    public int Reached { get; set; }

    // Конверсія відносно попереднього етапу.
    public double ConversionPercent { get; set; }

    // Частка від усіх угод воронки (для смужки в UI).
    [XmlIgnore] public double ReachedPercent { get; set; }
}
