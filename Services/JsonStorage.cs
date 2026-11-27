using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using LightCrm.Models;
using Serilog;

namespace LightCrm.Services;

// ШІ: Чорнетка асинхронного імпорту/експорту бази у JSON.
public static class JsonStorage {
    public static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions() {
        JsonSerializerOptions options = new JsonSerializerOptions();
        options.WriteIndented = true;
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.PropertyNameCaseInsensitive = true;
        options.AllowOutOfOrderMetadataProperties = true;
        options.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;

        JsonStringEnumConverter converter = new JsonStringEnumConverter(JsonNamingPolicy.CamelCase);
        options.Converters.Add(converter);

        return options;
    }

    public static async Task SaveAsync(CrmDatabase db, string path, CancellationToken ct) {
        string tmp = path + ".tmp";

        await using (FileStream fs = new FileStream(
            tmp, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true)) {
            await JsonSerializer.SerializeAsync(fs, db, Options, ct);
        }

        File.Move(tmp, path, overwrite: true);
        Log.Information("Експорт JSON: {Path} ({Contacts} контактів)", path, db.Contacts.Count);
    }

    // Читаємо файл блоками, потім перетворюємо JSON і перевіряємо дані.
    public static async Task<CrmDatabase> LoadAsync(string path, IProgress<int>? progress, CancellationToken ct) {
        byte[] buffer;
        int length;

        await using (FileStream fs = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, useAsync: true)) {
            long size = fs.Length;

            if (size > int.MaxValue) {
                throw new CorruptedDataException("Файл завеликий для імпорту (понад 2 ГБ).");
            }

            buffer = new byte[(int)size];
            int read = 0;

            while (read < size) {
                int bytesToRead = (int)Math.Min(65536, size - read);
                int bytesRead = await fs.ReadAsync(buffer, read, bytesToRead, ct);

                if (bytesRead == 0) {
                    break;
                }

                read = read + bytesRead;

                if (progress != null) {
                    progress.Report((int)(read * 80L / Math.Max(size, 1)));
                }
            }

            length = read;
        }

        if (progress != null) {
            progress.Report(80);
        }

        ct.ThrowIfCancellationRequested();

        CrmDatabase? db;

        try {
            db = await Task.Run(
                () => JsonSerializer.Deserialize<CrmDatabase>(buffer.AsSpan(0, length), Options), ct);
        }
        catch (JsonException ex) {
            throw new CorruptedDataException(
                $"Файл «{Path.GetFileName(path)}» пошкоджений або має неправильний формат JSON: {ex.Message}", ex);
        }
        catch (NotSupportedException ex) {
            throw new CorruptedDataException(
                $"Файл «{Path.GetFileName(path)}» пошкоджений або має неправильний формат JSON: {ex.Message}", ex);
        }

        if (db == null || db.Contacts == null) {
            throw new CorruptedDataException(
                $"Файл «{Path.GetFileName(path)}» не містить списку контактів.");
        }

        if (progress != null) {
            progress.Report(95);
        }

        // Додаємо порожні списки, якщо їх не було у JSON.
        foreach (Contact contact in db.Contacts) {
            if (contact.Deals == null) {
                contact.Deals = new List<Deal>();
            }

            foreach (Deal deal in contact.Deals) {
                if (deal.Interactions == null) {
                    deal.Interactions = new List<Interaction>();
                }
            }
        }

        var errors = await Task.Run(() => Validator.ValidateDatabase(db), ct);

        if (errors.Count > 0) {
            throw new CorruptedDataException(
                $"Файл «{Path.GetFileName(path)}» містить некоректні дані:\n- " +
                string.Join("\n- ", errors));
        }

        if (progress != null) {
            progress.Report(100);
        }

        Log.Information("Імпорт JSON: {Path}, контактів {Contacts}", path, db.Contacts.Count);
        return db;
    }
}
