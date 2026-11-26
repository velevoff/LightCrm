using System.Collections.Concurrent;
using LightCrm.Models;

namespace LightCrm.Services;

// «Зависла» угода - результат пошуку.
public sealed class StaleDeal
{
    public StaleDeal(string contactName, string company, string dealTitle, decimal amount,
                     DealStage stage, DateTime lastActivity, int daysIdle) {
        ContactName = contactName;
        Company = company;
        DealTitle = dealTitle;
        Amount = amount;
        Stage = stage;
        LastActivity = lastActivity;
        DaysIdle = daysIdle;
    }

    public string ContactName { get; }
    public string Company { get; }
    public string DealTitle { get; }
    public decimal Amount { get; }
    public DealStage Stage { get; }
    public DateTime LastActivity { get; }
    public int DaysIdle { get; }
    public string StageTitle {
        get {
            return Stage.ToTitle();
        }
    }
}

// Пошук відкритих угод без взаємодій понад N днів: послідовно та паралельно.
public static class StaleDealSearcher {
    private static IEnumerable<StaleDeal> ScanContact(Contact c, DateTime now, int days) {
        foreach (var d in c.Deals) {
            if (d.IsStale(now, days)) {
                DateTime last = d.LastActivity;
                int daysIdle = (int)(now - last).TotalDays;

                yield return new StaleDeal(
                    c.Name,
                    c.Company,
                    d.Title,
                    d.Amount,
                    d.Stage,
                    last,
                    daysIdle);
            }
        }
    }

    // Однопотоковий варіант.
    public static List<StaleDeal> FindSequential(IReadOnlyList<Contact> contacts, DateTime now, int days,
        CancellationToken ct = default) {
        List<StaleDeal> result = new List<StaleDeal>();

        foreach (Contact contact in contacts) {
            ct.ThrowIfCancellationRequested();

            foreach (StaleDeal deal in ScanContact(contact, now, days)) {
                result.Add(deal);
            }
        }

        return result;
    }

    // Багатопотоковий варіант: список контактів ділиться на певну кількість частин,
    // кожну обробляє окремий Task.Run; результати надходять у ConcurrentQueue.
    public static async Task FindParallelAsync(
        IReadOnlyList<Contact> contacts, DateTime now, int days, int parts,
        ConcurrentQueue<StaleDeal> results,
        IProgress<int>? progress, Action? onPartCompleted,
        CancellationToken ct) {
        int total = contacts.Count;
        if (total == 0) {
            if (progress != null) {
                progress.Report(100);
            }
            return;
        }

        int n = parts;
        if (n < 1) {
            n = 1;
        }
        if (n > total) {
            n = total;
        }

        int chunk = (int)Math.Ceiling(total / (double)n);
        int processed = 0;
        List<Task> tasks = new List<Task>(n);

        for (int p = 0; p < n; p++) {
            int start = p * chunk;
            int end = Math.Min(start + chunk, total);
            if (start >= end) break;

            tasks.Add(Task.Run(() => {
                for (int i = start; i < end; i++) {
                    ct.ThrowIfCancellationRequested();

                    foreach (StaleDeal staleDeal in ScanContact(contacts[i], now, days)) {
                        results.Enqueue(staleDeal);
                    }

                    int done = Interlocked.Increment(ref processed);
                    if (progress != null) {
                        progress.Report(done * 100 / total);
                    }
                }

                if (onPartCompleted != null) {
                    onPartCompleted();
                }
            }, ct));
        }

        await Task.WhenAll(tasks);
    }
}
