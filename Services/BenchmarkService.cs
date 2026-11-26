using System.Collections.Concurrent;
using System.Diagnostics;
using LightCrm.Models;

namespace LightCrm.Services;

public sealed record BenchmarkRow(int Threads, double AvgMs, double Speedup, double Efficiency);

public sealed record BenchmarkResult(
    IReadOnlyList<BenchmarkRow> Rows, int Contacts, int Deals, int Interactions, int StaleFound);

// Порівняння одно- та багатопотокового пошуку: S = T1 / Tn, E = S / n.
public static class BenchmarkService {
    public static async Task<BenchmarkResult> RunAsync(
        IReadOnlyList<Contact> contacts, int days, int multiplier, int repeats,
        IProgress<int>? progress, CancellationToken ct) {
        // Множник повторює посилання на ті самі контакти, а не копіює їх.
        IReadOnlyList<Contact> data = contacts;

        if (multiplier > 1) {
            List<Contact> newData = new List<Contact>();

            for (int i = 0; i < multiplier; i++) {
                foreach (Contact contact in contacts) {
                    newData.Add(contact);
                }
            }

            data = newData;
        }

        DateTime now = DateTime.Now;
        int deals = 0;
        int interactions = 0;

        foreach (Contact contact in data) {
            deals = checked(deals + contact.Deals.Count);

            foreach (Deal deal in contact.Deals) {
                interactions = checked(interactions + deal.Interactions.Count);
            }
        }

        // Кількість потоків для перевірки.
        int[] possibleThreadCounts = { 2, 4, 8, Environment.ProcessorCount };
        List<int> threadCounts = new List<int>();

        foreach (int threadCount in possibleThreadCounts) {
            if (threadCount > 1 && !threadCounts.Contains(threadCount)) {
                threadCounts.Add(threadCount);
            }
        }

        threadCounts.Sort();

        int steps = 1 + threadCounts.Count;
        int step = 0;

        // Прогрів JIT.
        int found = StaleDealSearcher.FindSequential(data, now, days, ct).Count;

        double t1 = await Task.Run(() => {
            Stopwatch sw = Stopwatch.StartNew();

            for (int r = 0; r < repeats; r++) {
                StaleDealSearcher.FindSequential(data, now, days, ct);
            }

            sw.Stop();
            return sw.Elapsed.TotalMilliseconds / repeats;
        }, ct);

        List<BenchmarkRow> rows = new List<BenchmarkRow>();
        rows.Add(new BenchmarkRow(1, t1, 1.0, 1.0));

        step++;
        if (progress != null) {
            progress.Report(step * 100 / steps);
        }

        foreach (int n in threadCounts) {
            ct.ThrowIfCancellationRequested();

            // Один запуск перед вимірюванням потрібен для прогріву.
            await StaleDealSearcher.FindParallelAsync(
                data, now, days, n, new ConcurrentQueue<StaleDeal>(), null, null, ct);

            Stopwatch sw = Stopwatch.StartNew();

            for (int r = 0; r < repeats; r++) {
                await StaleDealSearcher.FindParallelAsync(
                    data, now, days, n, new ConcurrentQueue<StaleDeal>(), null, null, ct);
            }

            sw.Stop();

            double tn = sw.Elapsed.TotalMilliseconds / repeats;
            double s = 0;

            if (tn > 0) {
                s = t1 / tn;
            }

            rows.Add(new BenchmarkRow(n, tn, s, s / n));

            step++;
            if (progress != null) {
                progress.Report(step * 100 / steps);
            }
        }

        return new BenchmarkResult(rows, data.Count, deals, interactions, found);
    }
}
