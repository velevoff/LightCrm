using LightCrm.Models;

namespace LightCrm.Services;

// Файл JSON пошкоджений, має неправильну структуру або недопустимі дані.
public sealed class CorruptedDataException : Exception {
    public CorruptedDataException(string message, Exception? inner = null) : base(message, inner) { }
}

// Спроба додати/змінити контакт так, що він збігається з уже існуючим.
public sealed class DuplicateContactException : Exception {
    public Contact Existing { get; }

    public DuplicateContactException(Contact existing)
        : base($"Контакт уже існує: {existing.Name} ({existing.Company}), {existing.Email}. " +
               "Контакти вважаються дублікатами, якщо збігається e-mail або пара «ім'я + компанія».") {
        Existing = existing;
    }
}
