using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using DormitoryPAT.Models;
using Microsoft.EntityFrameworkCore;
using DormitoryPAT.Context;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;

class Program
{
    private static ITelegramBotClient _botClient;
    private static ReceiverOptions _receiverOptions;
    private static Dictionary<long, UserSession> _userSessions = new();
    private static System.Timers.Timer _notificationTimer;

    // Класс для хранения состояния пользователя
    class UserSession
    {
        public Students Student { get; set; }
        public AuthState AuthState { get; set; } = AuthState.None;
        public string CurrentCommand { get; set; }
        public Dictionary<string, object> TempData { get; set; } = new();
    }

    enum AuthState
    {
        None,
        AwaitingPhone
    }

    static async Task Main(string[] args)
    {
        Console.WriteLine($"[{DateTime.Now}] Запуск бота...");

        _botClient = new TelegramBotClient("7681929292:AAELFhLTiH3c4KZtnRrPY9aGD6gYyLWVo5E");
        _receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new[] { UpdateType.Message, UpdateType.CallbackQuery },
            DropPendingUpdates = true,
        };

        using var cts = new CancellationTokenSource();
        _botClient.StartReceiving(UpdateHandler, ErrorHandler, _receiverOptions, cts.Token);

        // Настройка таймера для уведомлений в 13:00
        _notificationTimer = new System.Timers.Timer(TimeSpan.FromHours(1).TotalMilliseconds);
        _notificationTimer.Elapsed += async (s, e) => await SendDutyNotifications();
        _notificationTimer.Start();

        var me = await _botClient.GetMe();
        Console.WriteLine($"[{DateTime.Now}] {me.FirstName} запущен! @{me.Username}");
        Console.WriteLine($"[{DateTime.Now}] Ожидание сообщений...");

        await Task.Delay(-1);
    }

    private static async Task UpdateHandler(ITelegramBotClient client, Update update, CancellationToken token)
    {
        try
        {
            Console.WriteLine($"[{DateTime.Now}] Получено обновление типа: {update.Type}");

            switch (update.Type)
            {
                case UpdateType.Message when update.Message?.Text != null:
                    Console.WriteLine($"[{DateTime.Now}] Сообщение от {update.Message.From.Id}: {update.Message.Text}");
                    await HandleMessage(client, update.Message);
                    break;

                case UpdateType.Message when update.Message?.Contact != null:
                    Console.WriteLine($"[{DateTime.Now}] Получен контакт от {update.Message.From.Id}");
                    await HandleMessage(client, update.Message);
                    break;

                case UpdateType.CallbackQuery:
                    Console.WriteLine($"[{DateTime.Now}] Callback от {update.CallbackQuery.From.Id}: {update.CallbackQuery.Data}");
                    await HandleCallbackQuery(client, update.CallbackQuery);
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now}] Ошибка обработки обновления: {ex}");
        }
    }

    private static async Task HandleMessage(ITelegramBotClient client, Message message)
    {
        var chatId = message.Chat.Id;
        Console.WriteLine($"[{DateTime.Now}] Обработка сообщения в чате {chatId}");

        if (!_userSessions.TryGetValue(chatId, out var session))
        {
            Console.WriteLine($"[{DateTime.Now}] Создана новая сессия для чата {chatId}");
            session = new UserSession();
            _userSessions[chatId] = session;
        }

        if (message.Text == "/start")
        {
            Console.WriteLine($"[{DateTime.Now}] Пользователь {chatId} начал процесс авторизации");
            await StartAuthProcess(client, chatId);
            return;
        }

        if (session.AuthState == AuthState.AwaitingPhone && message.Contact != null)
        {
            Console.WriteLine($"[{DateTime.Now}] Пользователь {chatId} предоставил номер телефона");
            await ProcessPhoneNumber(client, message, session);
            return;
        }

        if (session.Student == null)
        {
            Console.WriteLine($"[{DateTime.Now}] Пользователь {chatId} не авторизован");
            await client.SendMessage(chatId, "Пожалуйста, начните с команды /start");
            return;
        }

        if (session.TempData.ContainsKey("repairState"))
        {
            Console.WriteLine($"[{DateTime.Now}] Пользователь {chatId} на этапе создания заявки: {session.TempData["repairState"]}");
            await HandleRepairRequestCreation(client, message, session);
            return;
        }

        if (session.TempData.ContainsKey("complaintState") && session.TempData["complaintState"].ToString() == "awaitingText")
        {
            Console.WriteLine($"[{DateTime.Now}] Пользователь {chatId} отправил текст жалобы");
            await ProcessComplaintText(client, message, session);
            return;
        }

        if (session.TempData.ContainsKey("dutyState"))
        {
            Console.WriteLine($"[{DateTime.Now}] Пользователь {chatId} на этапе создания/редактирования дежурства: {session.TempData["dutyState"]}");
            await HandleDutyProcess(client, message, session);
            return;
        }

        Console.WriteLine($"[{DateTime.Now}] Обработка команды от авторизованного пользователя {session.Student.StudentId}");
        await ProcessAuthorizedCommand(client, message, session);
    }

    #region Авторизация
    private static async Task StartAuthProcess(ITelegramBotClient client, long chatId)
    {
        var requestButton = new KeyboardButton("Предоставить номер телефона") { RequestContact = true };
        var keyboard = new ReplyKeyboardMarkup(requestButton) { ResizeKeyboard = true, OneTimeKeyboard = true };

        await client.SendMessage(
            chatId: chatId,
            text: "Для авторизации предоставьте номер телефона:",
            replyMarkup: keyboard);

        _userSessions[chatId].AuthState = AuthState.AwaitingPhone;
    }

    private static async Task ProcessPhoneNumber(ITelegramBotClient client, Message message, UserSession session)
    {
        try
        {
            var phoneNumber = message.Contact.PhoneNumber;
            var cleanPhone = phoneNumber.Replace("+", "").Replace(" ", "").Replace("-", "");

            using var db = new StudentsContext();
            var student = await db.Students
                .FirstOrDefaultAsync(s => s.PhoneNumber.Replace("+", "").Replace(" ", "").Replace("-", "") == cleanPhone);

            if (student == null || student.StudentId == 0)
            {
                await client.SendMessage(
                    chatId: message.Chat.Id,
                    text: "❌ Студент не найден или данные некорректны. Обратитесь к администратору.",
                    replyMarkup: new ReplyKeyboardRemove());
                return;
            }

            if (student.TelegramId == null || student.TelegramId != message.Chat.Id)
            {
                student.TelegramId = message.Chat.Id;
                await db.SaveChangesAsync();
            }

            session.Student = student;
            await CompleteAuth(client, message.Chat.Id, student);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now}] Ошибка обработки номера: {ex}");
            await client.SendMessage(
                chatId: message.Chat.Id,
                text: "⚠ Ошибка обработки номера. Попробуйте снова.");
        }
    }

    private static async Task CompleteAuth(ITelegramBotClient client, long chatId, Students student)
    {
        await client.SendMessage(
            chatId: chatId,
            text: $"✅ Вы успешно авторизованы как {student.StudentsRoleDisplay} {student.FIO}",
            replyMarkup: new ReplyKeyboardRemove());

        await UpdateMenuCommands(client, chatId);
    }
    #endregion

    #region Меню команд
    private static async Task UpdateMenuCommands(ITelegramBotClient client, long chatId)
    {
        var commands = new List<BotCommand>
        {
            new BotCommand { Command = "menu", Description = "Главное меню" },
            new BotCommand { Command = "repair", Description = "Заявка на ремонт" },
            new BotCommand { Command = "complaint", Description = "Жалоба" },
            new BotCommand { Command = "payment", Description = "Плата за общежитие" },
            new BotCommand { Command = "phonebook", Description = "Телефонный справочник" },
            new BotCommand { Command = "duty", Description = "График дежурств" }
        };

        await client.SetMyCommands(commands, scope: new BotCommandScopeAllPrivateChats(), languageCode: "ru");
        await ShowMainMenu(client, chatId);
    }

    private static async Task ShowMainMenu(ITelegramBotClient client, long chatId)
    {
        var message = new StringBuilder("Главное меню:\nдоступные команды:\n");
        message.AppendLine("/menu - Главное меню");
        message.AppendLine("/repair - Заявка на ремонт");
        message.AppendLine("/complaint - Жалоба");
        message.AppendLine("/payment - Плата за общежитие");
        message.AppendLine("/phonebook - Телефонный справочник");
        message.AppendLine("/duty - График дежурств");

        await client.SendMessage(chatId, message.ToString());
    }
    #endregion

    #region Обработка команд после авторизации
    private static async Task ProcessAuthorizedCommand(ITelegramBotClient client, Message message, UserSession session)
    {
        var command = message.Text?.Split(' ')[0].ToLower();
        session.CurrentCommand = command;

        switch (command)
        {
            case "/start":
                await client.SendMessage(message.Chat.Id, "Вы уже авторизованы. Используйте доступные команды.");
                break;

            case "/menu":
                await ShowMainMenu(client, message.Chat.Id);
                break;

            case "/repair":
                await HandleRepairCommand(client, message, session);
                break;

            case "/complaint":
                await HandleComplaintCommand(client, message, session);
                break;

            case "/phonebook":
                await HandlePhonebookCommand(client, message.Chat.Id);
                break;

            case "/payment":
                await HandleDormPaymentCommand(client, message.Chat.Id);
                break;

            case "/duty":
                await HandleDutyCommand(client, message, session);
                break;

            default:
                await client.SendMessage(message.Chat.Id, "Неизвестная команда. Используйте /menu для списка команд.");
                break;
        }
    }
    #endregion

    #region Модуль заявок на ремонт
    private static async Task HandleRepairCommand(ITelegramBotClient client, Message message, UserSession session)
    {
        Console.WriteLine($"[{DateTime.Now}] Обработка команды repair для студента {session.Student.StudentId}");
        await StartRepairRequestCreation(client, message.Chat.Id);
    }

    private static async Task StartRepairRequestCreation(ITelegramBotClient client, long chatId)
    {
        Console.WriteLine($"[{DateTime.Now}] Начало создания заявки на ремонт для {chatId}");

        var keyboard = new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton("Электрика"),
            new KeyboardButton("Сантехника"),
            new KeyboardButton("Мебель")
        })
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = true
        };

        await client.SendMessage(
            chatId: chatId,
            text: "Выберите тип проблемы:",
            replyMarkup: keyboard);

        if (!_userSessions.TryGetValue(chatId, out var session))
        {
            session = new UserSession();
            _userSessions[chatId] = session;
        }

        session.TempData["repairState"] = "awaitingProblemType";
        Console.WriteLine($"[{DateTime.Now}] Установлено состояние awaitingProblemType для {chatId}");
    }

    private static async Task HandleRepairRequestCreation(ITelegramBotClient client, Message message, UserSession session)
    {
        var chatId = message.Chat.Id;
        Console.WriteLine($"[{DateTime.Now}] Обработка создания заявки, этап: {session.TempData["repairState"]}");

        if (session.TempData["repairState"].ToString() == "awaitingProblemType")
        {
            if (!Enum.TryParse<ProblemType>(message.Text, out var problemType) || !Enum.IsDefined(typeof(ProblemType), problemType))
            {
                await client.SendMessage(
                    chatId: chatId,
                    text: "Пожалуйста, выберите тип проблемы из предложенных: Электрика, Сантехника, Мебель.");
                return;
            }

            session.TempData["problemType"] = problemType;
            Console.WriteLine($"[{DateTime.Now}] Пользователь выбрал тип проблемы: {message.Text}");

            await client.SendMessage(
                chatId: chatId,
                text: "Укажите место, где требуется ремонт (например, комната 312):",
                replyMarkup: new ReplyKeyboardRemove());

            session.TempData["repairState"] = "awaitingLocation";
        }
        else if (session.TempData["repairState"].ToString() == "awaitingLocation")
        {
            if (string.IsNullOrWhiteSpace(message.Text) || message.Text.Length > 255)
            {
                await client.SendMessage(
                    chatId: chatId,
                    text: "Место должно быть указано и не превышать 255 символов. Попробуйте снова.");
                return;
            }

            session.TempData["location"] = message.Text;
            Console.WriteLine($"[{DateTime.Now}] Пользователь указал место: {message.Text}");

            await client.SendMessage(
                chatId: chatId,
                text: "Опишите проблему подробнее:");

            session.TempData["repairState"] = "awaitingComment";
        }
        else if (session.TempData["repairState"].ToString() == "awaitingComment")
        {
            var comment = message.Text ?? "Нет комментария";
            var problemType = (ProblemType)session.TempData["problemType"];
            var location = session.TempData["location"].ToString();

            Console.WriteLine($"[{DateTime.Now}] Создание заявки с параметрами: " +
                             $"Тип: {problemType}, Место: {location}, Комментарий: {comment}");

            using var db = new RepairRequestsContext();
            var request = new RepairRequests
            {
                StudentId = session.Student.StudentId,
                Location = location,
                Problem = problemType,
                UserComment = comment,
                Status = RequestStatus.Создана,
                RequestDate = DateTime.Now,
                LastStatusChange = DateTime.Now
            };

            await db.RepairRequests.AddAsync(request);
            await db.SaveChangesAsync();
            Console.WriteLine($"[{DateTime.Now}] Заявка #{request.RequestId} сохранена в БД");

            await client.SendMessage(
                chatId: chatId,
                text: $"✅ Заявка на ремонт создана!\n\n" +
                     $"Тип: {problemType}\n" +
                     $"Место: {location}\n" +
                     $"Комментарий: {comment}\n\n" +
                     $"Номер заявки: #{request.RequestId}");

            session.TempData.Remove("repairState");
            session.TempData.Remove("problemType");
            session.TempData.Remove("location");
        }
    }
    #endregion

    #region Модуль жалоб
    private static async Task HandleComplaintCommand(ITelegramBotClient client, Message message, UserSession session)
    {
        await StartComplaintProcess(client, message.Chat.Id);
    }

    private static async Task StartComplaintProcess(ITelegramBotClient client, long chatId)
    {
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("Анонимно", "complaint_anonymous"), InlineKeyboardButton.WithCallbackData("Не анонимно", "complaint_public") }
        });

        await client.SendMessage(chatId, "Выберите, как отправить жалобу:", replyMarkup: keyboard);

        if (!_userSessions.TryGetValue(chatId, out var session))
        {
            session = new UserSession();
            _userSessions[chatId] = session;
        }
        session.TempData["complaintState"] = "awaitingType";
    }

    private static async Task HandleComplaintCallback(ITelegramBotClient client, CallbackQuery callbackQuery, UserSession session)
    {
        var chatId = callbackQuery.Message.Chat.Id;
        var data = callbackQuery.Data;

        if (data == "complaint_anonymous" || data == "complaint_public")
        {
            var isAnonymous = data == "complaint_anonymous";
            session.TempData["complaintState"] = "awaitingText";
            session.TempData["isAnonymous"] = isAnonymous;

            await client.EditMessageText(chatId: chatId, messageId: callbackQuery.Message.MessageId, text: "✍ Напишите текст вашей жалобы:", replyMarkup: null);
            await client.AnswerCallbackQuery(callbackQuery.Id);
        }
    }

    private static async Task ProcessComplaintText(ITelegramBotClient client, Message message, UserSession session)
    {
        var chatId = message.Chat.Id;
        var complaintText = message.Text;
        var isAnonymous = (bool)session.TempData["isAnonymous"];

        if (string.IsNullOrWhiteSpace(complaintText))
        {
            await client.SendMessage(chatId, "Текст жалобы не может быть пустым. Попробуйте снова:");
            return;
        }

        using var db = new ComplaintsContext();
        var complaint = new Complaints
        {
            StudentId = isAnonymous ? null : (long?)session.Student.StudentId,
            ComplaintText = complaintText,
            Status = ComplaintStatus.Создана,
            SubmissionDate = DateTime.Now,
            LastStatusChange = DateTime.Now
        };

        await db.Complaints.AddAsync(complaint);
        await db.SaveChangesAsync();

        session.TempData.Remove("complaintState");
        session.TempData.Remove("isAnonymous");

        await client.SendMessage(chatId, $"✅ Жалоба #{complaint.ComplaintId} успешно создана!\nТип: {(isAnonymous ? "Анонимно" : "Не анонимно")}");
    }
    #endregion

    #region Модуль телефонного справочника
    private static async Task HandlePhonebookCommand(ITelegramBotClient client, long chatId)
    {
        var message = new StringBuilder("📞 Телефоны:\n\n");
        message.AppendLine("8 (342) 206-02-54 - пост охраны\n");
        message.AppendLine("8 (342) 206-03-40 (доб. 904) - и.о. заведующего общежитием Козлов Иван Валерьевич\n");
        message.AppendLine("8 (342) 206-03-40 (доб. 424) - воспитатель Габдулова Ильсина Наилевна (2,3,4 этажи; дневное время), каб. 424\n");
        message.AppendLine("8 (342) 206-03-40 (доб. 524) - воспитатель Микова Елена Александровна (5,6,7 этажи; дневное время), каб. 524\n");
        message.AppendLine("8 (342) 206-03-40 (доб. 324) - дежурный по общежитию (2,3,4 этажи; вечернее и ночное время), каб. 324\n");
        message.AppendLine("8 (342) 206-03-40 (доб. 624) - дежурный по общежитию (5,6,7 этажи; вечернее и ночное время), каб. 624");

        await client.SendMessage(chatId, message.ToString());
    }
    #endregion

    #region Модуль платы за общежитие
    private static async Task HandleDormPaymentCommand(ITelegramBotClient client, long chatId)
    {
        var image = GeneratePaymentTableImage();
        await using var stream = new MemoryStream(image);
        await client.SendPhoto(chatId, InputFile.FromStream(stream, "payment_table.png"), caption: "💰 Плата за общежитие");
    }

    private static byte[] GeneratePaymentTableImage()
    {
        var headers = new[] { "Категория студентов", "Плата за наём, руб.", "Плата за коммун. услуги, руб.", "ИТОГО в месяц, руб." };
        var rows = new[]
        {
            new[] { "Без категории", "9,60", "1137,92", "1147,52" },
            new[] { "Дети-сироты и оставшиеся без попечения", "-", "-", "Бесплатно" },
            new[] { "Потерявшие обоих родителей", "-", "-", "Бесплатно" },
            new[] { "Участники СВО и их дети", "-", "-", "Бесплатно" },
            new[] { "Инвалиды", "-", "568,96", "568,96" },
            new[] { "Получатели соц. помощи", "-", "796,54", "796,54" }
        };

        int cellPadding = 15, rowHeight = 40, headerHeight = 65, imageWidth = 1100;
        int[] columnWidths = { 450, 200, 250, 200 };
        int imageHeight = headerHeight + rows.Length * rowHeight + 50;

        using var bitmap = new Bitmap(imageWidth, imageHeight);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(System.Drawing.Color.White);

        var headerFont = new System.Drawing.Font("Arial", 14, FontStyle.Bold);
        var rowFont = new System.Drawing.Font("Arial", 12);
        var brush = new SolidBrush(System.Drawing.Color.Black);

        int x = 0;
        for (int i = 0; i < headers.Length; i++)
        {
            graphics.DrawRectangle(Pens.Gray, x, 0, columnWidths[i], headerHeight);
            graphics.DrawString(headers[i], headerFont, brush, new RectangleF(x + cellPadding, 15, columnWidths[i] - 2 * cellPadding, headerHeight), new StringFormat { Alignment = StringAlignment.Center });
            x += columnWidths[i];
        }

        int y = headerHeight;
        foreach (var row in rows)
        {
            x = 0;
            for (int i = 0; i < row.Length; i++)
            {
                graphics.DrawRectangle(Pens.LightGray, x, y, columnWidths[i], rowHeight);
                graphics.DrawString(row[i], rowFont, brush, new RectangleF(x + cellPadding, y + 10, columnWidths[i] - 2 * cellPadding, rowHeight), new StringFormat { Alignment = StringAlignment.Center });
                x += columnWidths[i];
            }
            y += rowHeight;
        }

        var footerFont = new System.Drawing.Font("Arial", 10, FontStyle.Italic);
        graphics.DrawString("Актуально на " + DateTime.Now.ToString("dd.MM.yyyy"), footerFont, Brushes.Gray, 20, imageHeight - 30);

        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }
    #endregion
        
    #region Модуль дежурств на кухне
    private static async Task HandleDutyCommand(ITelegramBotClient client, Message message, UserSession session)
    {
        var chatId = message.Chat.Id;
        var role = session.Student.StudentRole;

        if (role == StudentRole.Студент)
        {
            await ShowDutyScheduleForStudent(client, chatId, session.Student.Floor);
        }
        else if (role == StudentRole.Староста_этажа)
        {
            session.TempData["dutyFloor"] = session.Student.Floor;
            var keyboard = new InlineKeyboardMarkup(new[]
            {
            new[] { InlineKeyboardButton.WithCallbackData("Посмотреть", "duty_view"), InlineKeyboardButton.WithCallbackData("Создать", "duty_create") }
        });
            await client.SendMessage(chatId, "Выберите действие:", replyMarkup: keyboard);
        }
        else if (role == StudentRole.Председатель_общежития)
        {
            var keyboard = new InlineKeyboardMarkup(Enumerable.Range(2, 6).Select(f => new[] { InlineKeyboardButton.WithCallbackData($"Этаж {f}", $"duty_floor_{f}") }).ToArray());
            await client.SendMessage(chatId, "Выберите этаж:", replyMarkup: keyboard);
        }
    }

    private static async Task ShowDutyScheduleForStudent(ITelegramBotClient client, long chatId, int floor)
    {
        var now = DateTime.UtcNow; // 01:22 AM CEST = 11:22 PM UTC (05.06.2025)
        var currentMonth = new DateTime(now.Year, now.Month, 1);
        var nextMonth = currentMonth.AddMonths(1);

        var currentSchedule = GenerateDutyImage(currentMonth, floor);
        await using var currentStream = new MemoryStream(currentSchedule);
        await client.SendPhoto(chatId, InputFile.FromStream(currentStream, $"duty_{currentMonth:yyyy-MM}.png"), caption: $"График дежурств на кухне - {currentMonth:MMMM yyyy}");

        using var db = new DutyScheduleContext();
        if (db.DutySchedule.Any(ds => ds.Date.Month == nextMonth.Month && ds.Date.Year == nextMonth.Year && ds.Floor == floor))
        {
            var nextSchedule = GenerateDutyImage(nextMonth, floor);
            await using var nextStream = new MemoryStream(nextSchedule);
            await client.SendPhoto(chatId, InputFile.FromStream(nextStream, $"duty_{nextMonth:yyyy-MM}.png"), caption: $"График дежурств на кухне - {nextMonth:MMMM yyyy}");
        }
    }

    private static async Task HandleDutyProcess(ITelegramBotClient client, Message message, UserSession session)
    {
        var chatId = message.Chat.Id;
        var state = session.TempData["dutyState"].ToString();
        var floor = (int)session.TempData["dutyFloor"];
        var expectedRoomStart = floor * 100 + 1;
        var expectedRoomEnd = floor * 100 + 22;

        if (state == "awaitingMonthCreate")
        {
            if (!int.TryParse(message.Text, out var month) || month < 1 || month > 12)
            {
                await client.SendMessage(chatId, "Введите корректный номер месяца (1-12):");
                return;
            }
            var year = DateTime.UtcNow.Year;
            var selectedDate = new DateTime(year, month, 1);
            await CreateDutySchedule(client, chatId, session, selectedDate);
        }
        else if (state == "awaitingRoomCreate")
        {
            if (!int.TryParse(message.Text, out var room) || room < expectedRoomStart || room > expectedRoomEnd)
            {
                await client.SendMessage(chatId, $"Введите корректный номер комнаты для этажа {floor} ({expectedRoomStart}-{expectedRoomEnd}):");
                return;
            }
            var day = (int)session.TempData["dutyDay"];
            var baseDate = (DateTime)session.TempData["dutyDate"];
            var date = baseDate.AddDays(day - 1);
            using var db = new DutyScheduleContext();
            var existing = await db.DutySchedule.FirstOrDefaultAsync(ds => ds.Floor == floor && ds.Date.Date == date.Date);
            if (existing != null)
            {
                existing.Room = room;
            }
            else
            {
                await db.DutySchedule.AddAsync(new DutySchedule { Floor = floor, Date = date, Room = room });
            }
            await db.SaveChangesAsync();
            Console.WriteLine($"[{DateTime.Now}] Сохранено в БД: Этаж {floor}, Дата {date:yyyy-MM-dd}, Комната {room}");
            session.TempData["dutyDay"] = day + 1;
            await NextDutyDay(client, chatId, session);
        }
        else if (state == "awaitingDateEdit")
        {
            if (!DateTime.TryParseExact(message.Text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                await client.SendMessage(chatId, "Введите дату в формате yyyy-MM-dd (например, 2025-06-06):");
                return;
            }
            session.TempData["dutyEditDate"] = date;
            session.TempData["dutyState"] = "awaitingRoomEdit";
            await client.SendMessage(chatId, $"Введите номер комнаты для {date:dd.MM.yyyy} (например, {expectedRoomStart}-{expectedRoomEnd}):");
        }
        else if (state == "awaitingRoomEdit")
        {
            if (!int.TryParse(message.Text, out var room) || room < expectedRoomStart || room > expectedRoomEnd)
            {
                await client.SendMessage(chatId, $"Введите корректный номер комнаты для этажа {floor} ({expectedRoomStart}-{expectedRoomEnd}):");
                return;
            }
            var date = (DateTime)session.TempData["dutyEditDate"];
            using var db = new DutyScheduleContext();
            var duty = await db.DutySchedule.FirstOrDefaultAsync(ds => ds.Floor == floor && ds.Date.Date == date.Date);
            if (duty != null)
            {
                duty.Room = room;
            }
            else
            {
                await db.DutySchedule.AddAsync(new DutySchedule { Floor = floor, Date = date, Room = room });
            }
            await db.SaveChangesAsync();
            Console.WriteLine($"[{DateTime.Now}] Обновлено в БД: Этаж {floor}, Дата {date:yyyy-MM-dd}, Комната {room}");
            var keyboard = new InlineKeyboardMarkup(new[]
            {
            new[] { InlineKeyboardButton.WithCallbackData("Редактировать дальше", "duty_edit_continue"), InlineKeyboardButton.WithCallbackData("Сохранить и закончить", "duty_edit_save") }
        });
            await client.SendMessage(chatId, $"Комната для {date:dd.MM.yyyy} обновлена на {room}. Хотите редактировать дальше или сохранить изменения?", replyMarkup: keyboard);
            session.TempData["dutyState"] = "awaitingEditChoice";
        }
    }

    private static async Task HandleDutyCallback(ITelegramBotClient client, CallbackQuery callbackQuery, UserSession session)
    {
        var chatId = callbackQuery.Message.Chat.Id;
        var data = callbackQuery.Data;
        var floor = session.TempData.ContainsKey("dutyFloor") ? (int)session.TempData["dutyFloor"] : session.Student.Floor;

        if (data.StartsWith("duty_floor_"))
        {
            floor = int.Parse(data.Split('_')[2]);
            session.TempData["dutyFloor"] = floor;
            var keyboard = new InlineKeyboardMarkup(new[]
            {
            new[] { InlineKeyboardButton.WithCallbackData("Посмотреть", "duty_view"), InlineKeyboardButton.WithCallbackData("Создать", "duty_create") }
        });
            await client.EditMessageText(chatId: chatId, messageId: callbackQuery.Message.MessageId, text: $"Выбран этаж {floor}. Выберите действие:", replyMarkup: keyboard);
            await client.AnswerCallbackQuery(callbackQuery.Id);
        }
        else if (data == "duty_view")
        {
            session.TempData["dutyFloor"] = floor;
            var keyboard = new InlineKeyboardMarkup(new[]
            {
            new[] { InlineKeyboardButton.WithCallbackData("Прошлый месяц", "duty_view_prev"), InlineKeyboardButton.WithCallbackData("Текущий месяц", "duty_view_current") },
            new[] { InlineKeyboardButton.WithCallbackData("Следующий месяц", "duty_view_next") }
        });
            await client.EditMessageText(chatId: chatId, messageId: callbackQuery.Message.MessageId, text: "Выберите месяц:", replyMarkup: keyboard);
            await client.AnswerCallbackQuery(callbackQuery.Id);
        }
        else if (data == "duty_create")
        {
            if (!session.TempData.ContainsKey("dutyFloor"))
            {
                await client.EditMessageText(chatId: chatId, messageId: callbackQuery.Message.MessageId, text: "Сначала выберите этаж.", replyMarkup: null);
                return;
            }
            var now = DateTime.UtcNow;
            using var db = new DutyScheduleContext();
            var current = new DateTime(now.Year, now.Month, 1);
            var next = current.AddMonths(1);
            if (db.DutySchedule.Any(ds => ds.Floor == floor && ds.Date.Month == current.Month && ds.Date.Year == current.Year))
            {
                if (db.DutySchedule.Any(ds => ds.Floor == floor && ds.Date.Month == next.Month && ds.Date.Year == next.Year))
                {
                    await client.EditMessageText(chatId: chatId, messageId: callbackQuery.Message.MessageId, text: "Графики на текущий и следующий месяцы уже существуют. Вы можете отредактировать их.", replyMarkup: null);
                }
                else
                {
                    await CreateDutySchedule(client, chatId, session, next);
                }
            }
            else
            {
                await CreateDutySchedule(client, chatId, session, current);
            }
            await client.AnswerCallbackQuery(callbackQuery.Id);
        }
        else if (data.StartsWith("duty_view_"))
        {
            if (!session.TempData.ContainsKey("dutyFloor"))
            {
                await client.EditMessageText(chatId: chatId, messageId: callbackQuery.Message.MessageId, text: "Сначала выберите этаж.", replyMarkup: null);
                return;
            }
            var now = DateTime.UtcNow;
            var selectedMonth = data switch
            {
                "duty_view_prev" => now.AddMonths(-1),
                "duty_view_current" => now,
                "duty_view_next" => now.AddMonths(1),
                _ => now
            };
            selectedMonth = new DateTime(selectedMonth.Year, selectedMonth.Month, 1);
            var image = GenerateDutyImage(selectedMonth, floor);
            await using var stream = new MemoryStream(image);
            await client.SendPhoto(chatId, InputFile.FromStream(stream, $"duty_{selectedMonth:yyyy-MM}.png"), caption: $"График дежурств на кухне - {selectedMonth:MMMM yyyy}");
            session.TempData["dutyMonth"] = selectedMonth;
            var keyboard = new InlineKeyboardMarkup(new[]
            {
            new[] { InlineKeyboardButton.WithCallbackData("Полное редактирование", "duty_edit_full"), InlineKeyboardButton.WithCallbackData("Частичное редактирование", "duty_edit_partial") },
            new[] { InlineKeyboardButton.WithCallbackData("Отмена", "duty_cancel") }
        });
            await client.SendMessage(chatId, "Выберите тип редактирования или отмените действие:", replyMarkup: keyboard);
            await client.AnswerCallbackQuery(callbackQuery.Id);
        }
        else if (data == "duty_edit_full")
        {
            if (!session.TempData.ContainsKey("dutyFloor") || !session.TempData.ContainsKey("dutyMonth"))
            {
                await client.SendMessage(chatId, "Сначала выберите месяц и этаж.");
                return;
            }
            var month = (DateTime)session.TempData["dutyMonth"];
            await CreateDutySchedule(client, chatId, session, month, true);
        }
        else if (data == "duty_edit_partial")
        {
            if (!session.TempData.ContainsKey("dutyFloor"))
            {
                await client.SendMessage(chatId, "Сначала выберите этаж.");
                return;
            }
            session.TempData["dutyState"] = "awaitingDateEdit";
            await client.SendMessage(chatId, "Введите дату для редактирования (формат: yyyy-MM-dd, например, 2025-06-06):");
        }
        else if (data == "duty_edit_continue")
        {
            session.TempData["dutyState"] = "awaitingDateEdit";
            await client.SendMessage(chatId, "Введите дату для редактирования (формат: yyyy-MM-dd, например, 2025-06-06):");
        }
        else if (data == "duty_edit_save")
        {
            session.TempData.Remove("dutyEditDate");
            session.TempData.Remove("dutyState");
            session.TempData.Remove("dutyMonth");
            await ShowDutyOptions(client, chatId, session);
        }
        else if (data == "duty_cancel")
        {
            session.TempData.Remove("dutyState");
            session.TempData.Remove("dutyMonth");
            await ShowDutyOptions(client, chatId, session);
        }
    }

    private static async Task CreateDutySchedule(ITelegramBotClient client, long chatId, UserSession session, DateTime month, bool isEdit = false)
    {
        var floor = (int)session.TempData["dutyFloor"];
        session.TempData["dutyDate"] = month;
        session.TempData["dutyDay"] = 1;
        session.TempData["dutyState"] = "awaitingRoomCreate";

        if (!isEdit)
        {
            await client.SendMessage(chatId, $"Создание графика дежурств для этажа {floor} на {month:MMMM yyyy}. Введите номер комнаты для 1-го числа (например, {floor}01-{floor}22):");
        }
        else
        {
            await client.SendMessage(chatId, $"Полное редактирование графика дежурств для этажа {floor} на {month:MMMM yyyy}. Введите номер комнаты для 1-го числа (например, {floor}01-{floor}22):");
        }
    }

    private static async Task NextDutyDay(ITelegramBotClient client, long chatId, UserSession session)
    {
        var day = (int)session.TempData["dutyDay"];
        var baseDate = (DateTime)session.TempData["dutyDate"];
        var floor = (int)session.TempData["dutyFloor"];
        var lastDay = DateTime.DaysInMonth(baseDate.Year, baseDate.Month);

        if (day > lastDay)
        {
            session.TempData.Remove("dutyState");
            session.TempData.Remove("dutyDay");
            session.TempData.Remove("dutyDate");
            var image = GenerateDutyImage(baseDate, floor);
            await using var stream = new MemoryStream(image);
            await client.SendPhoto(chatId, InputFile.FromStream(stream, $"duty_{baseDate:yyyy-MM}.png"), caption: $"График дежурств на кухне - {baseDate:MMMM yyyy}");
            await ShowDutyOptions(client, chatId, session);
            return;
        }

        var nextDate = baseDate.AddDays(day - 1);
        await client.SendMessage(chatId, $"Введите номер комнаты для {nextDate:dd.MM.yyyy} (например, {floor}01-{floor}22):");
        session.TempData["dutyDay"] = day;
    }

    private static async Task ShowDutyOptions(ITelegramBotClient client, long chatId, UserSession session)
    {
        var keyboard = new InlineKeyboardMarkup(new[]
        {
        new[] { InlineKeyboardButton.WithCallbackData("Посмотреть", "duty_view"), InlineKeyboardButton.WithCallbackData("Создать", "duty_create") }
    });
        await client.SendMessage(chatId, "Выберите действие:", replyMarkup: keyboard);
    }

    private static byte[] GenerateDutyImage(DateTime month, int floor)
    {
        int cellPadding = 10, rowHeight = 30, headerHeight = 50, imageWidth = 800;
        int[] columnWidths = { 100, 600 };
        int imageHeight = headerHeight + DateTime.DaysInMonth(month.Year, month.Month) * rowHeight + 50;

        using var bitmap = new Bitmap(imageWidth, imageHeight);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(System.Drawing.Color.White);

        var headerFont = new System.Drawing.Font("Arial", 12, FontStyle.Bold);
        var rowFont = new System.Drawing.Font("Arial", 10);
        var brush = new SolidBrush(System.Drawing.Color.Black);

        graphics.DrawRectangle(Pens.Gray, 0, 0, columnWidths[0], headerHeight);
        graphics.DrawString("Дата", headerFont, brush, new RectangleF(cellPadding, 15, columnWidths[0] - 2 * cellPadding, headerHeight), new StringFormat { Alignment = StringAlignment.Center });
        graphics.DrawRectangle(Pens.Gray, columnWidths[0], 0, columnWidths[1], headerHeight);
        graphics.DrawString("Комната", headerFont, brush, new RectangleF(columnWidths[0] + cellPadding, 15, columnWidths[1] - 2 * cellPadding, headerHeight), new StringFormat { Alignment = StringAlignment.Center });

        using var db = new DutyScheduleContext();
        var duties = db.DutySchedule.Where(ds => ds.Floor == floor && ds.Date.Month == month.Month && ds.Date.Year == month.Year).ToList();

        int y = headerHeight;
        for (int day = 1; day <= DateTime.DaysInMonth(month.Year, month.Month); day++)
        {
            var date = new DateTime(month.Year, month.Month, day);
            var duty = duties.FirstOrDefault(ds => ds.Date.Date == date.Date);
            var room = duty?.Room.ToString() ?? "Не назначено";

            graphics.DrawRectangle(Pens.LightGray, 0, y, columnWidths[0], rowHeight);
            graphics.DrawString(date.ToString("dd.MM"), rowFont, brush, new RectangleF(cellPadding, y + 5, columnWidths[0] - 2 * cellPadding, rowHeight), new StringFormat { Alignment = StringAlignment.Center });
            graphics.DrawRectangle(Pens.LightGray, columnWidths[0], y, columnWidths[1], rowHeight);
            graphics.DrawString(room, rowFont, brush, new RectangleF(columnWidths[0] + cellPadding, y + 5, columnWidths[1] - 2 * cellPadding, rowHeight), new StringFormat { Alignment = StringAlignment.Center });
            y += rowHeight;
        }

        var footerFont = new System.Drawing.Font("Arial", 8, FontStyle.Italic);
        graphics.DrawString($"Этаж {floor} - {month:MMMM yyyy}", footerFont, Brushes.Gray, 20, imageHeight - 30);

        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    private static async Task SendDutyNotifications()
    {
        var now = DateTime.UtcNow;
        if (now.Hour == 11 && now.Minute == 0) // 13:00 CEST = 11:00 UTC
        {
            using var db = new DutyScheduleContext();
            var tomorrow = now.AddDays(1).Date;
            var duties = db.DutySchedule.Where(ds => ds.Date.Date == tomorrow).ToList();

            using var studentsDb = new StudentsContext();
            foreach (var duty in duties)
            {
                var students = studentsDb.Students.Where(s => s.Floor == duty.Floor && s.Room == duty.Room && s.TelegramId != null).ToList();
                foreach (var student in students)
                {
                    await _botClient.SendMessage(student.TelegramId.Value, $"🔔 Напоминание: завтра, {tomorrow:dd.MM.yyyy}, дежурство на кухне за комнатой {duty.Room} (этаж {duty.Floor}).");
                }
            }
        }
    }
    #endregion

    #region Обработка CallbackQuery
    private static async Task HandleCallbackQuery(ITelegramBotClient client, CallbackQuery callbackQuery)
    {
        var chatId = callbackQuery.Message.Chat.Id;
        if (!_userSessions.TryGetValue(chatId, out var session))
        {
            await client.AnswerCallbackQuery(callbackQuery.Id, "Сессия не найдена");
            return;
        }

        if (callbackQuery.Data.StartsWith("complaint_"))
        {
            await HandleComplaintCallback(client, callbackQuery, session);
        }
        else if (callbackQuery.Data.StartsWith("duty_"))
        {
            await HandleDutyCallback(client, callbackQuery, session);
        }

        await client.AnswerCallbackQuery(callbackQuery.Id);
    }
    #endregion

    private static Task ErrorHandler(ITelegramBotClient botClient, Exception error, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[{DateTime.Now}] ⚠ ОШИБКА: {error}");
        return Task.CompletedTask;
    }
}