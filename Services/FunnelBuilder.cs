using System.Text;
using System.Xml;
using System.Xml.Serialization;
using LightCrm.Models;
using Serilog;

namespace LightCrm.Services;

// Побудова та XML-експорт «Воронки продажів».
public static class FunnelBuilder {
    public static FunnelReport Build(IEnumerable<Deal> deals) {
        DealStage[] stages = Enum.GetValues<DealStage>();
        int[] counts = new int[stages.Length];
        decimal[] sums = new decimal[stages.Length];

        // Рахуємо кількість угод і їх суми для кожного етапу.
        foreach (Deal deal in deals) {
            int index = (int)deal.Stage;
            counts[index] = counts[index] + 1;
            sums[index] = sums[index] + deal.Amount;
        }

        // До воронки входять етапи від Lead до Won.
        int pipeline = (int)DealStage.Won + 1;
        int[] reached = new int[pipeline];
        int running = 0;

        for (int i = pipeline - 1; i >= 0; i--) {
            running = running + counts[i];
            reached[i] = running;
        }

        FunnelReport report = new FunnelReport();
        report.GeneratedAt = DateTime.Now;

        for (int i = 0; i < counts.Length; i++) {
            report.TotalDeals = checked(report.TotalDeals + counts[i]);
            report.TotalAmount = report.TotalAmount + sums[i];
        }

        foreach (DealStage stage in stages) {
            int index = (int)stage;

            FunnelStage row = new FunnelStage();
            row.Code = stage.ToString();
            row.Title = stage.ToTitle();
            row.DealsCount = counts[index];
            row.TotalAmount = sums[index];

            if (index < pipeline) {
                row.Reached = reached[index];

                if (index == 0) {
                    if (reached[0] > 0) {
                        row.ConversionPercent = 100;
                    }
                    else {
                        row.ConversionPercent = 0;
                    }
                }
                else {
                    if (reached[index - 1] > 0) {
                        row.ConversionPercent = Math.Round(
                            reached[index] * 100.0 / reached[index - 1], 1);
                    }
                    else {
                        row.ConversionPercent = 0;
                    }
                }

                if (reached[0] > 0) {
                    row.ReachedPercent = Math.Round(reached[index] * 100.0 / reached[0], 1);
                }
                else {
                    row.ReachedPercent = 0;
                }
            }
            else {
                // «Програно» - окрема гілка.
                row.Reached = counts[index];
            }

            report.Stages.Add(row);
        }

        return report;
    }

    public static void SaveXml(FunnelReport report, string path) {
        XmlSerializer serializer = new XmlSerializer(typeof(FunnelReport));

        XmlWriterSettings settings = new XmlWriterSettings();
        settings.Indent = true;
        settings.Encoding = new UTF8Encoding(false);

        XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
        ns.Add("", "");

        using (XmlWriter writer = XmlWriter.Create(path, settings)) {
            serializer.Serialize(writer, report, ns);
        }

        Log.Information("Експорт воронки в XML: {Path}", path);
    }
}
