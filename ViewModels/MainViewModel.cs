using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LightCrm.Models;
using LightCrm.Services;
using Serilog;

namespace LightCrm.ViewModels;

public sealed partial class MainViewModel : ViewModelBase {
    private readonly IDialogService _dialogs;
    private readonly CrmRepository _repo = new();
    private CancellationTokenSource? _cts;

    public static string DefaultDataPath {
        get {
            return Path.Combine(AppContext.BaseDirectory, "Data", "crm.json");
        }
    }

    public MainViewModel(IDialogService dialogs) {
        _dialogs = dialogs;

        List<StageOption> stageOptions = new List<StageOption>();
        stageOptions.Add(new StageOption(null, "Усі етапи"));

        foreach (DealStage stage in Enum.GetValues<DealStage>()) {
            stageOptions.Add(new StageOption(stage, stage.ToTitle()));
        }

        StageOptions = stageOptions;
        _selectedStageFilter = StageOptions[0];
        _partsText = Environment.ProcessorCount.ToString();
    }

    // ------------------------------------------------------------ колекції для UI
    public ObservableCollection<Contact> Contacts { get; } = new();
    public ObservableCollection<DealRow> Deals { get; } = new();
    public ObservableCollection<Interaction> SelectedDealInteractions { get; } = new();
    public ObservableCollection<StaleDeal> StaleDeals { get; } = new();
    public ObservableCollection<FunnelStage> Funnel { get; } = new();
    public ObservableCollection<BenchmarkRow> BenchmarkRows { get; } = new();
    public IReadOnlyList<StageOption> StageOptions { get; }

    // ------------------------------------------------------------ стан
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private StageOption _selectedStageFilter;
    [ObservableProperty] private DateTime? _dateFrom;
    [ObservableProperty] private DateTime? _dateTo;

    [ObservableProperty] private Contact? _selectedContact;
    [ObservableProperty] private DealRow? _selectedDeal;
    [ObservableProperty] private Interaction? _selectedInteraction;

    [ObservableProperty] private string _statusText = "Готово.";
    [ObservableProperty] private double _progressPercent;
    [ObservableProperty] private bool _isBusy;

    [ObservableProperty] private string _staleDaysText = "30";
    [ObservableProperty] private string _partsText;
    [ObservableProperty] private string _staleSummary = "";

    [ObservableProperty] private string _benchDaysText = "30";
    [ObservableProperty] private string _benchMultiplierText = "100";
    [ObservableProperty] private string _benchRepeatsText = "20";
    [ObservableProperty] private string _benchSummary = "";

    partial void OnSearchTextChanged(string value) {
        RefreshContacts();
    }

    partial void OnSelectedStageFilterChanged(StageOption value) {
        RefreshDeals();
    }

    partial void OnDateFromChanged(DateTime? value) {
        RefreshDeals();
    }

    partial void OnDateToChanged(DateTime? value) {
        RefreshDeals();
    }

    partial void OnSelectedDealChanged(DealRow? value) {
        RefreshInteractions();
    }

    // ------------------------------------------------------------ оновлення відображення
    private void RefreshAll() {
        RefreshContacts();
        RefreshDeals();
        RefreshFunnel();
    }

    private void RefreshContacts() {
        // Один рядок пошуку: збіг у імені АБО в назві компанії (без урахування регістру)
        string searchText = SearchText.Trim();
        Contacts.Clear();

        foreach (Contact contact in _repo.Db.Contacts) {
            bool matches = false;

            if (searchText.Length == 0) {
                matches = true;
            }
            else if (contact.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase)) {
                matches = true;
            }
            else if (contact.Company.Contains(searchText, StringComparison.OrdinalIgnoreCase)) {
                matches = true;
            }

            if (matches) {
                Contacts.Add(contact);
            }
        }
    }

    private void RefreshDeals() {
        DealStage? stage = SelectedStageFilter?.Stage;
        DateTime? dateFrom = DateFrom?.Date;
        DateTime? dateTo = DateTo?.Date;

        Deals.Clear();

        foreach (Contact contact in _repo.Db.Contacts) {
            foreach (Deal deal in contact.Deals) {
                bool stageMatches = stage == null || deal.Stage == stage.Value;
                bool dateFromMatches = dateFrom == null || deal.Date.Date >= dateFrom.Value;
                bool dateToMatches = dateTo == null || deal.Date.Date <= dateTo.Value;

                if (stageMatches && dateFromMatches && dateToMatches) {
                    Deals.Add(new DealRow(contact, deal));
                }
            }
        }

        RefreshInteractions();
    }

    private void RefreshInteractions() {
        SelectedDealInteractions.Clear();
        if (SelectedDeal is null) return;
        List<Interaction> interactions = SelectedDeal.Deal.Interactions
            .OrderByDescending(item => item.Date)
            .ToList();

        foreach (Interaction interaction in interactions) {
            SelectedDealInteractions.Add(interaction);
        }
    }

    private void RefreshFunnel() {
        var report = FunnelBuilder.Build(_repo.AllDeals());
        Funnel.Clear();
        foreach (var s in report.Stages) Funnel.Add(s);
    }

    private void SetStatus(string text) {
        StatusText = text;
    }

    // ------------------------------------------------------------ виконання фонових операцій
    private async Task RunBusyAsync(string name, Func<CancellationToken, Task> action) {
        if (IsBusy) return;
        IsBusy = true;
        ProgressPercent = 0;
        _cts = new CancellationTokenSource();
        try {
            await action(_cts.Token);
        }
        catch (OperationCanceledException) {
            Log.Information("Операцію «{Name}» скасовано користувачем", name);
            SetStatus($"{name}: скасовано.");
        }
        catch (Exception ex) {
            Log.Error(ex, "Помилка під час операції «{Name}»", name);
            SetStatus($"{name}: помилка — {ex.Message}");
            await _dialogs.ShowMessageAsync("Помилка", $"{name}: {ex.Message}");
        }
        finally {
            _cts.Dispose();
            _cts = null;
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel() {
        if (_cts != null) {
            _cts.Cancel();
        }
    }

    public async Task InitializeAsync() {
        if (File.Exists(DefaultDataPath)) {
            await ImportFileAsync(DefaultDataPath);
        }
        else {
            _repo.ReplaceAll(DataGenerator.Generate(55, 600));
            RefreshAll();
            SetStatus("Файл Data/crm.json не знайдено — створено тестові дані в пам'яті.");
            Log.Warning("crm.json не знайдено, згенеровано тестові дані");
        }
    }

    // ------------------------------------------------------------ імпорт / експорт
    [RelayCommand]
    private async Task ImportAsync() {
        if (IsBusy) return;
        var path = await _dialogs.PickOpenJsonAsync();
        if (path != null) {
            await ImportFileAsync(path);
        }
    }

    private Task ImportFileAsync(string path) {
        return RunBusyAsync("Імпорт JSON", async ct => {
            var progress = new Progress<int>(p => ProgressPercent = p);
            var sw = Stopwatch.StartNew();
            try {
                var db = await JsonStorage.LoadAsync(path, progress, ct);
                int skipped = _repo.ReplaceAll(db);
                RefreshAll();
                sw.Stop();

                int interactions = _repo.AllDeals().Sum(d => d.Interactions.Count);
                string message = $"Імпортовано: {_repo.Db.Contacts.Count} контактів, {_repo.AllDeals().Count()} угод, " +
                                 $"{interactions} взаємодій за {sw.ElapsedMilliseconds} мс";

                if (skipped > 0) {
                    message = message + $"; пропущено дублікатів: {skipped}";
                }
                else {
                    message = message + ".";
                }

                SetStatus(message);
            }
            catch (CorruptedDataException ex) {
                Log.Error(ex, "Пошкоджений файл імпорту {Path}", path);
                SetStatus("Імпорт не виконано: файл пошкоджено. Поточні дані не змінено.");
                await _dialogs.ShowMessageAsync("Пошкоджений файл", ex.Message);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) {
                Log.Error(ex, "Не вдалося прочитати файл {Path}", path);
                SetStatus("Не вдалося прочитати файл.");
                await _dialogs.ShowMessageAsync("Помилка читання файлу", ex.Message);
            }
        });
    }

    [RelayCommand]
    private async Task ExportJsonAsync() {
        if (IsBusy) return;
        var path = await _dialogs.PickSaveAsync("Експорт бази у JSON", "crm-export", "json");
        if (path is null) return;

        await RunBusyAsync("Експорт JSON", async ct => {
            await JsonStorage.SaveAsync(_repo.Db, path, ct);
            SetStatus($"Базу збережено: {path}");
        });
    }

    [RelayCommand]
    private async Task ExportFunnelXmlAsync() {
        if (IsBusy) return;
        var path = await _dialogs.PickSaveAsync("Експорт воронки продажів у XML", "sales-funnel", "xml");
        if (path is null) return;

        try {
            FunnelBuilder.SaveXml(FunnelBuilder.Build(_repo.AllDeals()), path);
            SetStatus($"Воронку продажів збережено: {path}");
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException) {
            Log.Error(ex, "Помилка експорту XML {Path}", path);
            await _dialogs.ShowMessageAsync("Помилка експорту XML", ex.Message);
        }
    }

    [RelayCommand]
    private async Task GenerateLargeFileAsync() {
        if (IsBusy) return;
        var path = await _dialogs.PickSaveAsync("Збереження згенерованого файлу (10 500 взаємодій)", "crm_large", "json");
        if (path is null) return;

        await RunBusyAsync("Генерація даних", async ct => {
            var db = await Task.Run(() => DataGenerator.Generate(60, 10_500), ct);
            await JsonStorage.SaveAsync(db, path, ct);
            SetStatus($"Згенеровано файл з 60 контактами та 10 500 взаємодіями: {path}. Імпортуйте його кнопкою «Імпорт JSON».");
        });
    }

    // ------------------------------------------------------------ контакти
    [RelayCommand]
    private async Task AddContactAsync() {
        if (IsBusy) return;
        var dlg = await _dialogs.EditContactAsync(null);
        if (dlg is null) return;
        try {
            var contact = dlg.ToContact();
            _repo.AddContact(contact);
            Log.Information("Створено контакт {Name} ({Company})", contact.Name, contact.Company);
            RefreshAll();
            SetStatus($"Контакт «{contact.Name}» додано.");
        }
        catch (DuplicateContactException ex) {
            Log.Warning(ex, "Спроба додати дублікат контакту");
            await _dialogs.ShowMessageAsync("Дублікат контакту", ex.Message);
        }
    }

    [RelayCommand]
    private async Task EditContactAsync() {
        if (IsBusy || SelectedContact is null) return;
        var target = SelectedContact;
        var dlg = await _dialogs.EditContactAsync(target);
        if (dlg is null) return;
        try {
            var c = dlg.ToContact();
            _repo.UpdateContact(target, c.Name, c.Company, c.Phone, c.Email);
            Log.Information("Змінено контакт {Id}", target.Id);
            RefreshAll();
            SetStatus($"Контакт «{c.Name}» оновлено.");
        }
        catch (DuplicateContactException ex) {
            Log.Warning(ex, "Спроба змінити контакт на дублікат");
            await _dialogs.ShowMessageAsync("Дублікат контакту", ex.Message);
        }
    }

    [RelayCommand]
    private async Task DeleteContactAsync() {
        if (IsBusy || SelectedContact is null) return;
        var c = SelectedContact;
        if (!await _dialogs.ConfirmAsync("Видалення контакту",
                $"Видалити контакт «{c.Name}» разом з усіма його угодами ({c.Deals.Count}) та взаємодіями?")) return;

        _repo.RemoveContact(c);
        Log.Information("Видалено контакт {Name} ({Id}) з {Deals} угодами", c.Name, c.Id, c.Deals.Count);
        SelectedDeal = null;
        RefreshAll();
        SetStatus($"Контакт «{c.Name}» видалено.");
    }

    // ------------------------------------------------------------ угоди
    [RelayCommand]
    private void ClearDealFilters() {
        SelectedStageFilter = StageOptions[0];
        DateFrom = null;
        DateTo = null;
    }

    [RelayCommand]
    private async Task AddDealAsync() {
        if (IsBusy) return;
        if (_repo.Db.Contacts.Count == 0) {
            await _dialogs.ShowMessageAsync("Немає контактів", "Спочатку створіть контакт, до якого належатиме угода.");
            return;
        }
        var dlg = await _dialogs.EditDealAsync(_repo.Db.Contacts, null);
        if (dlg is null) return;

        var deal = new Deal();
        dlg.ApplyTo(deal);
        dlg.SelectedContact!.AddDeal(deal);
        Log.Information("Створено угоду «{Title}» ({Amount}) для {Contact}", deal.Title, deal.Amount, dlg.SelectedContact.Name);
        RefreshAll();
        SetStatus($"Угоду «{deal.Title}» додано.");
    }

    [RelayCommand]
    private async Task EditDealAsync() {
        if (IsBusy || SelectedDeal is null) return;
        var row = SelectedDeal;
        var dlg = await _dialogs.EditDealAsync(_repo.Db.Contacts, row);
        if (dlg is null) return;

        dlg.ApplyTo(row.Deal);
        Log.Information("Змінено угоду {Id}", row.Deal.Id);
        RefreshAll();
        SetStatus($"Угоду «{row.Deal.Title}» оновлено.");
    }

    [RelayCommand]
    private async Task DeleteDealAsync() {
        if (IsBusy || SelectedDeal is null) return;
        var row = SelectedDeal;
        if (!await _dialogs.ConfirmAsync("Видалення угоди",
                $"Видалити угоду «{row.Title}» разом з {row.InteractionsCount} взаємодіями?")) return;

        row.Contact.RemoveDeal(row.Deal);
        Log.Information("Видалено угоду {Id}", row.Deal.Id);
        SelectedDeal = null;
        RefreshAll();
        SetStatus("Угоду видалено.");
    }

    // ------------------------------------------------------------ взаємодії
    [RelayCommand]
    private async Task AddInteractionAsync() {
        if (IsBusy) return;
        if (SelectedDeal is null) {
            await _dialogs.ShowMessageAsync("Оберіть угоду", "Виберіть угоду в таблиці, до якої додається взаємодія.");
            return;
        }
        var deal = SelectedDeal.Deal;
        var dlg = await _dialogs.EditInteractionAsync(null);
        if (dlg is null) return;

        var interaction = dlg.CreateInteraction();
        deal.AddInteraction(interaction);
        Log.Information("Додано взаємодію {Type} до угоди {Deal}", interaction.TypeName, deal.Id);
        RefreshInteractions();
        RefreshDealsKeepingSelection();
        SetStatus("Взаємодію додано.");
    }

    [RelayCommand]
    private async Task EditInteractionAsync() {
        if (IsBusy || SelectedInteraction is null) return;
        var target = SelectedInteraction;
        var dlg = await _dialogs.EditInteractionAsync(target);
        if (dlg is null) return;

        dlg.ApplyTo(target);
        Log.Information("Змінено взаємодію {Id}", target.Id);
        RefreshInteractions();
        RefreshDealsKeepingSelection();
        SetStatus("Взаємодію оновлено.");
    }

    [RelayCommand]
    private async Task DeleteInteractionAsync() {
        if (IsBusy || SelectedDeal is null || SelectedInteraction is null) return;
        if (!await _dialogs.ConfirmAsync("Видалення взаємодії", "Видалити вибрану взаємодію?")) return;

        var target = SelectedInteraction;
        SelectedDeal.Deal.RemoveInteraction(target);
        Log.Information("Видалено взаємодію {Id}", target.Id);
        RefreshInteractions();
        RefreshDealsKeepingSelection();
        SetStatus("Взаємодію видалено.");
    }

    /// <summary>Оновлює таблицю угод (кількість взаємодій), зберігаючи вибрану угоду.</summary>
    private void RefreshDealsKeepingSelection() {
        var keep = SelectedDeal?.Deal;
        RefreshDeals();
        if (keep == null) {
            SelectedDeal = null;
        }
        else {
            SelectedDeal = Deals.FirstOrDefault(row => ReferenceEquals(row.Deal, keep));
        }
    }

    // ------------------------------------------------------------ пошук «завислих» угод
    private bool TryReadInt(string text, int min, int max, string what, out int value) {
        bool isNumber = int.TryParse(text, out value);

        if (isNumber) {
            if (value >= min && value <= max) {
                return true;
            }
        }

        SetStatus($"{what}: введіть ціле число від {min} до {max}.");
        return false;
    }

    private void DrainStale(ConcurrentQueue<StaleDeal> queue) {
        while (queue.TryDequeue(out StaleDeal? deal)) {
            if (deal != null) {
                StaleDeals.Add(deal);
            }
        }

        StaleSummary = $"Знайдено: {StaleDeals.Count}";
    }

    [RelayCommand]
    private async Task FindStaleParallelAsync() {
        if (IsBusy) return;
        if (!TryReadInt(StaleDaysText, 1, 3650, "Кількість днів", out int days)) return;
        if (!TryReadInt(PartsText, 1, 64, "Кількість частин", out int parts)) return;

        StaleDeals.Clear();
        var snapshot = _repo.Db.Contacts.ToList();

        await RunBusyAsync("Паралельний пошук", async ct => {
            var progress = new Progress<int>(p => ProgressPercent = p);
            var queue = new ConcurrentQueue<StaleDeal>();
            var sw = Stopwatch.StartNew();

            await StaleDealSearcher.FindParallelAsync(
                snapshot, DateTime.Now, days, parts, queue, progress,
                onPartCompleted: () => Dispatcher.UIThread.Post(() => DrainStale(queue)),
                ct);

            sw.Stop();
            DrainStale(queue);
            SortStale();
            ProgressPercent = 100;
            StaleSummary = $"Знайдено: {StaleDeals.Count} угод (>{days} днів без взаємодій), " +
                           $"{parts} частин(и), {sw.Elapsed.TotalMilliseconds:F2} мс";
            SetStatus("Паралельний пошук завершено.");
            Log.Information("Паралельний пошук: {Count} угод, {Parts} частин, {Ms} мс", StaleDeals.Count, parts, sw.Elapsed.TotalMilliseconds);
        });
    }

    [RelayCommand]
    private async Task FindStaleSequentialAsync() {
        if (IsBusy) return;
        if (!TryReadInt(StaleDaysText, 1, 3650, "Кількість днів", out int days)) return;

        StaleDeals.Clear();
        var snapshot = _repo.Db.Contacts.ToList();

        await RunBusyAsync("Послідовний пошук", async ct => {
            var sw = Stopwatch.StartNew();
            var found = await Task.Run(() => StaleDealSearcher.FindSequential(snapshot, DateTime.Now, days, ct), ct);
            sw.Stop();

            foreach (var s in found) StaleDeals.Add(s);
            SortStale();
            ProgressPercent = 100;
            StaleSummary = $"Знайдено: {StaleDeals.Count} угод (>{days} днів без взаємодій), 1 потік, {sw.Elapsed.TotalMilliseconds:F2} мс";
            SetStatus("Послідовний пошук завершено.");
        });
    }

    private void SortStale() {
        var sorted = StaleDeals.OrderByDescending(s => s.DaysIdle).ToList();
        StaleDeals.Clear();
        foreach (var s in sorted) StaleDeals.Add(s);
    }

    // ------------------------------------------------------------ порівняння одно- та багатопотокового пошуку
    [RelayCommand]
    private async Task RunBenchmarkAsync() {
        if (IsBusy) return;
        if (!TryReadInt(BenchDaysText, 1, 3650, "Кількість днів", out int days)) return;
        if (!TryReadInt(BenchMultiplierText, 1, 10_000, "Множник набору", out int mult)) return;
        if (!TryReadInt(BenchRepeatsText, 1, 1000, "Кількість повторів", out int repeats)) return;

        var snapshot = _repo.Db.Contacts.ToList();
        BenchmarkRows.Clear();

        await RunBusyAsync("Порівняння продуктивності", async ct => {
            var progress = new Progress<int>(p => ProgressPercent = p);
            var result = await BenchmarkService.RunAsync(snapshot, days, mult, repeats, progress, ct);

            foreach (var r in result.Rows) BenchmarkRows.Add(r);
            BenchSummary = $"Набір: {result.Contacts} контактів, {result.Deals} угод, {result.Interactions} взаємодій; " +
                           $"знайдено завислих: {result.StaleFound}; повторів: {repeats}; логічних ядер: {Environment.ProcessorCount}. " +
                           "S = T1/Tn, E = S/n.";
            SetStatus("Порівняння завершено.");
            Log.Information("Бенчмарк: {Summary}", string.Join("; ",
                result.Rows.Select(r => $"n={r.Threads}: {r.AvgMs:F3} мс, S={r.Speedup:F2}")));
        });
    }
}
