using CommunityToolkit.Mvvm.ComponentModel;
using LightCrm.Models;
using LightCrm.Services;

namespace LightCrm.ViewModels;

public abstract partial class DialogViewModelBase : ViewModelBase {
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorText = "";

    public bool HasError {
        get
        {
            return !string.IsNullOrEmpty(ErrorText);
        }
    }

    protected bool Finish(List<string> errors) {
        ErrorText = string.Join(Environment.NewLine, errors);
        return errors.Count == 0;
    }

    public abstract bool Validate();
}

// ---------------------------------------------------------------- Контакт
public sealed partial class ContactDialogViewModel : DialogViewModelBase {
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _company = "";
    [ObservableProperty] private string _phone = "";
    [ObservableProperty] private string _email = "";

    public string Header { get; }

    public ContactDialogViewModel(Contact? existing) {
        if (existing == null) {
            Header = "Новий контакт";
            return;
        }

        Header = "Редагування контакту";
        Name = existing.Name;
        Company = existing.Company;
        Phone = existing.Phone;
        Email = existing.Email;
    }

    public override bool Validate() {
        List<string> errors = Validator.ValidateContact(Name, Company, Phone, Email);
        return Finish(errors);
    }

    public Contact ToContact() {
        Contact contact = new Contact();
        contact.Name = Name.Trim();
        contact.Company = Company.Trim();
        contact.Phone = Phone.Trim();
        contact.Email = Email.Trim();

        return contact;
    }
}

// ---------------------------------------------------------------- Угода
public sealed partial class DealDialogViewModel : DialogViewModelBase {
    public IReadOnlyList<Contact> Contacts { get; }
    public IReadOnlyList<StageOption> StageOptions { get; }

    [ObservableProperty] private Contact? _selectedContact;
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _amountText = "";
    [ObservableProperty] private StageOption? _selectedStage;
    [ObservableProperty] private DateTime? _dateValue = DateTime.Today;

    public string Header { get; }
    public bool IsContactEditable { get; }

    public DealDialogViewModel(IReadOnlyList<Contact> contacts, DealRow? existing) {
        Contacts = contacts;

        List<StageOption> stages = new List<StageOption>();
        foreach (DealStage stage in Enum.GetValues<DealStage>()) {
            stages.Add(new StageOption(stage, stage.ToTitle()));
        }
        StageOptions = stages;

        if (existing == null) {
            IsContactEditable = true;
            Header = "Нова угода";
        }
        else {
            IsContactEditable = false;
            Header = "Редагування угоди";
        }

        SelectedStage = StageOptions[0];

        if (existing is null) {
            SelectedContact = contacts.FirstOrDefault();
            return;
        }

        SelectedContact = existing.Contact;
        Title = existing.Title;
        AmountText = existing.Amount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        SelectedStage = StageOptions.First(o => o.Stage == existing.Stage);
        DateValue = existing.Date;
    }

    public decimal ParsedAmount { get; private set; }

    public override bool Validate() {
        decimal? amount = null;
        decimal parsedAmount;

        if (Validator.TryParseAmount(AmountText, out parsedAmount)) {
            amount = parsedAmount;
        }

        List<string> errors = Validator.ValidateDeal(Title, amount, DateValue);

        if (SelectedContact == null) {
            errors.Insert(0, "Оберіть контакт.");
        }

        if (amount != null) {
            ParsedAmount = amount.Value;
        }

        return Finish(errors);
    }

    public void ApplyTo(Deal deal) {
        deal.Title = Title.Trim();
        deal.Amount = ParsedAmount;

        if (SelectedStage != null && SelectedStage.Stage != null) {
            deal.Stage = SelectedStage.Stage.Value;
        }
        else {
            deal.Stage = DealStage.Lead;
        }

        DateTime? selectedDate = DateValue;
        if (selectedDate.HasValue) {
            deal.Date = selectedDate.Value.Date;
        }
        else {
            throw new InvalidOperationException("Не вказано дату угоди.");
        }
    }
}

// ---------------------------------------------------------------- Взаємодія
public enum InteractionKind { Call, Meeting, Email }

public sealed record KindOption(InteractionKind Kind, string Title);

public sealed partial class InteractionDialogViewModel : DialogViewModelBase {
    public IReadOnlyList<KindOption> KindOptions { get; } = new[] {
        new KindOption(InteractionKind.Call, "Дзвінок"),
        new KindOption(InteractionKind.Meeting, "Зустріч"),
        new KindOption(InteractionKind.Email, "Лист")
    };

    [ObservableProperty] private KindOption _selectedKind;
    [ObservableProperty] private DateTime? _dateValue = DateTime.Today;
    [ObservableProperty] private string _timeText = DateTime.Now.ToString("HH:mm");
    [ObservableProperty] private string _description = "";
    [ObservableProperty] private string _durationText = "5";
    [ObservableProperty] private string _phone = "";
    [ObservableProperty] private string _location = "";
    [ObservableProperty] private string _subject = "";
    [ObservableProperty] private bool _isIncoming;
    [ObservableProperty] private bool _isCompleted = true;

    public string Header { get; }
    public bool IsKindEditable { get; }
    public bool IsCall {
        get {
            return SelectedKind.Kind == InteractionKind.Call;
        }
    }

    public bool IsMeeting {
        get {
            return SelectedKind.Kind == InteractionKind.Meeting;
        }
    }

    public bool IsEmail {
        get {
            return SelectedKind.Kind == InteractionKind.Email;
        }
    }

    public bool IsScheduled {
        get {
            return IsCall || IsMeeting;
        }
    }

    partial void OnSelectedKindChanged(KindOption value) {
        OnPropertyChanged(nameof(IsCall));
        OnPropertyChanged(nameof(IsMeeting));
        OnPropertyChanged(nameof(IsEmail));
        OnPropertyChanged(nameof(IsScheduled));
    }

    public InteractionDialogViewModel(Interaction? existing) {
        _selectedKind = KindOptions[0];

        if (existing == null) {
            IsKindEditable = true;
            Header = "Нова взаємодія";
            return;
        }

        IsKindEditable = false;
        Header = "Редагування взаємодії";

        DateValue = existing.Date.Date;
        TimeText = existing.Date.ToString("HH:mm");
        Description = existing.Description;

        switch (existing) {
            case Call c:
                SelectedKind = KindOptions[0];
                DurationText = c.DurationMinutes.ToString();
                Phone = c.PhoneNumber;
                IsCompleted = c.IsCompleted;
                break;
            case Meeting m:
                SelectedKind = KindOptions[1];
                Location = m.Location;
                IsCompleted = m.IsCompleted;
                break;
            case EmailMessage e:
                SelectedKind = KindOptions[2];
                Subject = e.Subject;
                IsIncoming = e.IsIncoming;
                break;
        }
    }

    private DateTime _combined;
    private int _duration;

    public override bool Validate() {
        var errors = Validator.ValidateInteraction(Description, DateValue);

        if (!Validator.TryParseTime(TimeText, out var time))
            errors.Add("Час має формат ГГ:хх (напр. 14:30).");
        else if (DateValue is not null)
            _combined = DateValue.Value.Date + time;

        switch (SelectedKind.Kind) {
            case InteractionKind.Call:
                if (!int.TryParse(DurationText, out _duration) || _duration < 0 || _duration > 1440)
                    errors.Add("Тривалість дзвінка — ціле число хвилин від 0 до 1440.");
                if (!string.IsNullOrWhiteSpace(Phone) && !Validator.IsValidPhone(Phone))
                    errors.Add("Телефон має неправильний формат.");
                break;
            case InteractionKind.Meeting:
                if (string.IsNullOrWhiteSpace(Location)) errors.Add("Місце зустрічі обов'язкове.");
                break;
            case InteractionKind.Email:
                if (string.IsNullOrWhiteSpace(Subject)) errors.Add("Тема листа обов'язкова.");
                break;
        }
        return Finish(errors);
    }

    public Interaction CreateInteraction() {
        Interaction interaction;

        if (SelectedKind.Kind == InteractionKind.Call) {
            interaction = new Call();
        }
        else if (SelectedKind.Kind == InteractionKind.Meeting) {
            interaction = new Meeting();
        }
        else {
            interaction = new EmailMessage();
        }

        ApplyTo(interaction);
        return interaction;
    }

    public void ApplyTo(Interaction i) {
        i.Date = _combined;
        i.Description = Description.Trim();
        switch (i) {
            case Call c:
                c.PhoneNumber = Phone.Trim();
                c.DurationMinutes = _duration;
                c.IsCompleted = IsCompleted;
                break;
            case Meeting m:
                m.Location = Location.Trim();
                m.IsCompleted = IsCompleted;
                break;
            case EmailMessage e:
                e.Subject = Subject.Trim();
                e.IsIncoming = IsIncoming;
                break;
        }
    }
}
