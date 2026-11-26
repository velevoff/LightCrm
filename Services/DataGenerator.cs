using LightCrm.Models;

namespace LightCrm.Services;

// Генератор тестових даних (контакти, угоди, взаємодії) з фіксованим seed.
public static class DataGenerator
{
    private static readonly (string Ua, string Lat)[] FirstNames =
    {
        ("Олександр","oleksandr"),("Марія","maria"),("Іван","ivan"),("Олена","olena"),("Андрій","andrii"),
        ("Наталія","natalia"),("Дмитро","dmytro"),("Ірина","iryna"),("Сергій","serhii"),("Тетяна","tetiana"),
        ("Василь","vasyl"),("Юлія","yulia"),("Богдан","bohdan"),("Оксана","oksana"),("Максим","maksym"),
        ("Анастасія","anastasia"),("Володимир","volodymyr"),("Катерина","kateryna"),("Роман","roman"),("Лариса","larysa")
    };

    private static readonly (string Ua, string Lat)[] LastNames =
    {
        ("Коваленко","kovalenko"),("Шевченко","shevchenko"),("Бондаренко","bondarenko"),("Ткаченко","tkachenko"),
        ("Мельник","melnyk"),("Кравчук","kravchuk"),("Олійник","oliinyk"),("Лисенко","lysenko"),
        ("Петренко","petrenko"),("Гончаренко","honcharenko"),("Савченко","savchenko"),("Руденко","rudenko"),
        ("Мороз","moroz"),("Яковенко","yakovenko"),("Поліщук","polishchuk"),("Клименко","klymenko"),
        ("Данилюк","danyliuk"),("Власенко","vlasenko"),("Грищенко","hryshchenko"),("Захарченко","zakharchenko")
    };

    private static readonly string[] Companies =
    {
        "Одеса-Логістик","Чорноморські Технології","Дельта Трейд","АгроСвіт","Південний Банк","Софт-Прайм","БудІнвест",
        "МедікаПлюс","ТехноМаркет","Порт-Сервіс","Адріатика","Едельвейс","Файн-Фуд","Нова Енергія","ІТ-Хаб Одеса",
        "ЮгТекстиль","Арка Груп","Стандарт Плюс","Балтик Трейдінг","Оптима","Ритм","Горизонт","Меридіан","Вектор","Пріоритет"
    };

    private static readonly string[] DealTitles =
    {
        "Впровадження CRM","Постачання обладнання","Річна підтримка","Розробка сайту","Аудит безпеки","Навчання персоналу",
        "Ліцензії на ПЗ","Інтеграція з 1С","Мобільний застосунок","Хмарна міграція","Консалтинг","Рекламна кампанія"
    };

    private static readonly string[] Notes =
    {
        "Обговорили умови та терміни","Надіслано комерційну пропозицію","Клієнт просить знижку","Уточнили технічні вимоги",
        "Домовились про наступний крок","Клієнт на нараді, передзвонити","Погоджено специфікацію","Очікуємо рішення керівництва"
    };

    private static readonly string[] Places = { "Офіс клієнта", "Наш офіс", "Zoom", "Кав'ярня на Дерибасівській", "Google Meet" };
    private static readonly string[] Operators = { "50", "67", "63", "93", "97", "99", "68", "96" };

    public static CrmDatabase Generate(int contactCount, int interactionCount, int seed = 42)
    {
        var rnd = new Random(seed);
        var now = DateTime.Now;
        var db = new CrmDatabase();

        for (int i = 0; i < contactCount; i++)
        {
            var f = FirstNames[i % FirstNames.Length];
            var l = LastNames[(i / FirstNames.Length + i * 3) % LastNames.Length];
            db.Contacts.Add(new Contact
            {
                Name = $"{f.Ua} {l.Ua}",
                Company = Companies[i % Companies.Length],
                Phone = $"+380{Operators[rnd.Next(Operators.Length)]}{rnd.Next(1_000_000, 10_000_000)}",
                Email = $"{f.Lat}.{l.Lat}{i}@example.ua"
            });
        }

        int dealCount = Math.Max(contactCount * 2, interactionCount / 5);
        var deals = new List<Deal>(dealCount);
        for (int d = 0; d < dealCount; d++)
        {
            var deal = new Deal
            {
                Title = DealTitles[rnd.Next(DealTitles.Length)],
                Amount = Math.Round((decimal)(rnd.Next(5, 2000) * 1000 + rnd.Next(0, 1000)), 0),
                Stage = PickStage(rnd),
                Date = now.Date.AddDays(-rnd.Next(5, 400))
            };
            db.Contacts[d % contactCount].AddDeal(deal);
            deals.Add(deal);
        }

        for (int k = 0; k < interactionCount; k++)
        {
            var deal = deals[rnd.Next(deals.Count)];
            int span = Math.Max(1, (int)(now - deal.Date).TotalDays);
            var date = deal.Date.AddDays(rnd.Next(0, span)).AddHours(rnd.Next(8, 19)).AddMinutes(rnd.Next(0, 60));
            if (date > now) date = now.AddMinutes(-rnd.Next(1, 600));

            Interaction it;
            int kind = rnd.Next(10);
            if (kind < 4)
                it = new Call { PhoneNumber = $"+380{Operators[rnd.Next(Operators.Length)]}{rnd.Next(1_000_000, 10_000_000)}", DurationMinutes = rnd.Next(1, 46) };
            else if (kind < 7)
                it = new Meeting { Location = Places[rnd.Next(Places.Length)] };
            else
                it = new EmailMessage { Subject = DealTitles[rnd.Next(DealTitles.Length)], IsIncoming = rnd.Next(2) == 0 };

            it.Date = date;
            it.Description = Notes[rnd.Next(Notes.Length)];
            deal.AddInteraction(it);
        }
        return db;
    }

    private static DealStage PickStage(Random rnd)
    {
        int r = rnd.Next(100);
        return r < 20 ? DealStage.Lead
             : r < 40 ? DealStage.Qualified
             : r < 60 ? DealStage.Proposal
             : r < 75 ? DealStage.Negotiation
             : r < 90 ? DealStage.Won
             : DealStage.Lost;
    }
}
