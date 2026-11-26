# Легкий CRM: контакти, угоди, історія взаємодій

Курсова робота з дисципліни «Об'єктно-орієнтоване програмування».
C# | .NET 10 | Avalonia UI 12 | MVVM | System.Text.Json | XmlSerializer | Serilog.

## Запуск

```bash
dotnet restore
dotnet run
```

Потрібен .NET 10 SDK. При старті автоматично імпортується `Data/crm.json`
(55 контактів, 120 угод, 600 взаємодій). Журнал: `logs/crm-YYYYMMDD.log` поруч із програмою.

## Структура

```
LightCrm/
  Models/        Interaction (abstract) → Call, Meeting, EmailMessage; IScheduledAction; Contact, Deal; DealStage; FunnelReport
  Services/      Validator, CrmRepository, JsonStorage, FunnelBuilder (XML), StaleDealSearcher, BenchmarkService, DataGenerator
  ViewModels/    MainViewModel, DialogViewModels (контакт / угода / взаємодія), DealRow, IDialogService
  Views/         MainWindow, ContactDialog, DealDialog, InteractionDialog, MessageDialog
  Data/          crm.json, crm_large.json (10 500 взаємодій), tests/ (пошкоджені файли для тестів)
```

## Відповідність завданню

| П. завдання | Де реалізовано |
| 1. C#, .NET 10, Avalonia 12 | `LightCrm.csproj` |
| 2. crm.json, 50+ контактів, 10 000+ взаємодій, XML | `Data/crm.json`, `Data/crm_large.json`, експорт воронки в XML |
| 3. ООП, ієрархія, IScheduledAction, композиція, MVVM | `Models/`, `ViewModels/`, `Views/` |
| 4.1 CRUD + валідація | `Validator`, діалоги, `MainViewModel` |
| 4.2 Пошук, фільтри, сортування DataGrid | вкладки «Контакти» і «Угоди та взаємодії» |
| 4.3 JSON / XML | `JsonStorage`, `FunnelBuilder.SaveXml` |
| 4.4 Фоновий імпорт, паралельний пошук | `JsonStorage.LoadAsync`, `StaleDealSearcher.FindParallelAsync` (Task.Run, CancellationToken, ConcurrentQueue, Dispatcher.UIThread.Post, IProgress, ProgressBar) |
| 4.5 Винятки, Serilog | `CorruptedDataException`, `DuplicateContactException`, `Program.ConfigureLogging` |
| 6. Тестування, S = T1/Tn | вкладка «Одно- та багатопотоковість» |
