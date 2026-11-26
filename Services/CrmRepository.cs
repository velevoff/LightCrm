using LightCrm.Models;
using Serilog;

namespace LightCrm.Services;

// Сховище в пам'яті: контакти, угоди, взаємодії; контроль дублікатів.
public sealed class CrmRepository {
    public CrmDatabase Db { get; private set; } = new CrmDatabase();

    public IEnumerable<Deal> AllDeals() {
        return Db.Contacts.SelectMany(contact => contact.Deals);
    }

    public static bool IsDuplicate(Contact a, Contact b) {
        if (string.Equals(a.Email.Trim(), b.Email.Trim(), StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        if (string.Equals(a.Name.Trim(), b.Name.Trim(), StringComparison.OrdinalIgnoreCase) &&
            string.Equals(a.Company.Trim(), b.Company.Trim(), StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        return false;
    }

    public void AddContact(Contact contact) {
        Contact? duplicate = null;

        foreach (Contact current in Db.Contacts) {
            if (IsDuplicate(current, contact)) {
                duplicate = current;
                break;
            }
        }

        if (duplicate != null) {
            throw new DuplicateContactException(duplicate);
        }

        Db.Contacts.Add(contact);
    }

    public void UpdateContact(Contact target, string name, string company, string phone, string email) {
        Contact probe = new Contact();
        probe.Name = name;
        probe.Company = company;
        probe.Phone = phone;
        probe.Email = email;

        Contact? duplicate = null;

        foreach (Contact current in Db.Contacts) {
            if (!ReferenceEquals(current, target) && IsDuplicate(current, probe)) {
                duplicate = current;
                break;
            }
        }

        if (duplicate != null) {
            throw new DuplicateContactException(duplicate);
        }

        target.Name = name;
        target.Company = company;
        target.Phone = phone;
        target.Email = email;
    }

    public bool RemoveContact(Contact contact) {
        return Db.Contacts.Remove(contact);
    }

    // Замінює базу імпортованою; дублікати пропускає. Повертає кількість пропущених.
    public int ReplaceAll(CrmDatabase imported) {
        CrmDatabase result = new CrmDatabase();
        int skipped = 0;

        foreach (Contact contact in imported.Contacts) {
            bool duplicateFound = false;

            foreach (Contact savedContact in result.Contacts) {
                if (IsDuplicate(savedContact, contact)) {
                    duplicateFound = true;
                    break;
                }
            }

            if (duplicateFound) {
                skipped++;
                Log.Warning(
                    "Імпорт: пропущено дублікат контакту {Name} ({Company}, {Email})",
                    contact.Name, contact.Company, contact.Email);
            }
            else {
                result.Contacts.Add(contact);
            }
        }

        Db = result;
        return skipped;
    }
}
