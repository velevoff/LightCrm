using System.Globalization;
using System.Text.RegularExpressions;
using LightCrm.Models;

namespace LightCrm.Services;

// Валідація даних.
public static class Validator
{
    public const decimal MaxAmount = 999_999_999_999m;

    private static readonly Regex EmailRx =
        new(@"^[A-Za-z0-9._%+\-]+@[A-Za-z0-9\-]+(\.[A-Za-z0-9\-]+)*\.[A-Za-z]{2,}$", RegexOptions.Compiled);

    private static readonly Regex PhoneCharsRx = new(@"^\+?[0-9\s\-()]+$", RegexOptions.Compiled);

    public static bool IsValidEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email) && email.Length <= 254 && EmailRx.IsMatch(email.Trim());

    // Телефон: необов'язковий «+», далі цифри/пробіли/дужки/дефіси; цифр від 7 до 15.
    public static bool IsValidPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return false;
        var p = phone.Trim();
        if (!PhoneCharsRx.IsMatch(p)) return false;
        int digits = p.Count(char.IsDigit);
        return digits is >= 7 and <= 15;
    }

    public static List<string> ValidateContact(string? name, string? company, string? phone, string? email)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(name)) errors.Add("Ім'я обов'язкове.");
        if (string.IsNullOrWhiteSpace(company)) errors.Add("Компанія обов'язкова.");
        if (string.IsNullOrWhiteSpace(phone)) errors.Add("Телефон обов'язковий.");
        else if (!IsValidPhone(phone)) errors.Add("Телефон має неправильний формат (7–15 цифр, напр. +380501234567).");
        if (string.IsNullOrWhiteSpace(email)) errors.Add("E-mail обов'язковий.");
        else if (!IsValidEmail(email)) errors.Add("E-mail має неправильний формат (напр. name@company.ua).");
        return errors;
    }

    public static List<string> ValidateDeal(string? title, decimal? amount, DateTime? date)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(title)) errors.Add("Назва угоди обов'язкова.");
        if (amount is null) errors.Add("Сума має бути числом.");
        else if (amount <= 0) errors.Add("Сума угоди має бути більшою за 0.");
        else if (amount > MaxAmount) errors.Add($"Сума завелика (максимум {MaxAmount:N0}).");
        if (date is null) errors.Add("Дата угоди обов'язкова.");
        return errors;
    }

    public static List<string> ValidateInteraction(string? description, DateTime? date)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(description)) errors.Add("Опис взаємодії обов'язковий.");
        if (date is null) errors.Add("Дата взаємодії обов'язкова.");
        return errors;
    }

    public static bool TryParseAmount(string? text, out decimal amount)
    {
        amount = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var normalized = text.Replace(" ", "").Replace('\u00A0'.ToString(), "").Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
    }

    public static bool TryParseTime(string? text, out TimeSpan time)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(text) || !Regex.IsMatch(text.Trim(), @"^\d{1,2}:\d{2}$")) return false;
        return TimeSpan.TryParse(text.Trim(), CultureInfo.InvariantCulture, out time)
               && time >= TimeSpan.Zero && time < TimeSpan.FromHours(24);
    }

    // Перевірка цілісності бази після імпорту.
    public static List<string> ValidateDatabase(CrmDatabase db, int max = 5)
    {
        var errors = new List<string>();
        void Add(string s) { if (errors.Count < max) errors.Add(s); }

        int ci = 0;
        foreach (var c in db.Contacts)
        {
            ci++;
            foreach (var e in ValidateContact(c.Name, c.Company, c.Phone, c.Email))
                Add($"Контакт №{ci}: {e}");
            foreach (var d in c.Deals)
            {
                foreach (var e in ValidateDeal(d.Title, d.Amount, d.Date))
                    Add($"Контакт №{ci}, угода «{d.Title}»: {e}");
                foreach (var i in d.Interactions)
                    foreach (var e in ValidateInteraction(i.Description, i.Date))
                        Add($"Контакт №{ci}, угода «{d.Title}», взаємодія: {e}");
            }
            if (errors.Count >= max) break;
        }
        return errors;
    }
}
