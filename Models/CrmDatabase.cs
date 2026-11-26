namespace LightCrm.Models;

// Корінь файлу crm.json
public sealed class CrmDatabase {
    public List<Contact> Contacts { get; set; } = new();
}
