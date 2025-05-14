using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using DormitoryPAT.Models;
using Microsoft.EntityFrameworkCore;
using DormitoryPAT.Context;
using System.Text;

class Program
{
    private static ITelegramBotClient _botClient;
    private static ReceiverOptions _receiverOptions;
    private static Dictionary<long, UserSession> _userSessions = new();

    // Класс для хранения состояния пользователя
    class UserSession
    {
        public Users User { get; set; }
        public AuthState AuthState { get; set; } = AuthState.None;
        public string CurrentCommand { get; set; }
        public Dictionary<string, object> TempData { get; set; } = new();
    }

    enum AuthState
    {
        None,
        AwaitingPhone,
        AwaitingPassword
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

        // Обработка команды /start
        if (message.Text == "/start")
        {
            Console.WriteLine($"[{DateTime.Now}] Пользователь {chatId} начал процесс авторизации");
            await StartAuthProcess(client, chatId);
            return;
        }

        // Обработка этапов авторизации
        if (session.AuthState == AuthState.AwaitingPhone && message.Contact != null)
        {
            Console.WriteLine($"[{DateTime.Now}] Пользователь {chatId} предоставил номер телефона");
            await ProcessPhoneNumber(client, message, session);
            return;
        }

        if (session.AuthState == AuthState.AwaitingPassword)
        {
            Console.WriteLine($"[{DateTime.Now}] Пользователь {chatId} ввел пароль");
            await ProcessPassword(client, message, session);
            return;
        }

        // Проверка авторизации
        if (session.User == null)
        {
            Console.WriteLine($"[{DateTime.Now}] Пользователь {chatId} не авторизован");
            await client.SendMessage(chatId, "Пожалуйста, начните с команды /start");
            return;
        }

        if (session.TempData.ContainsKey("complaintState") && session.TempData["complaintState"].ToString() == "awaitingText")
        {
            Console.WriteLine($"[{DateTime.Now}] Пользователь {chatId} отправил текст жалобы");
            await ProcessComplaintText(client, message, session);
            return;
        }

        if (session.TempData.ContainsKey("repairState"))
        {
            Console.WriteLine($"[{DateTime.Now}] Пользователь {chatId} на этапе создания заявки: {session.TempData["repairState"]}");
            await HandleRepairRequestCreation(client, message, session);
            return;
        }

        // Обработка команд после авторизации
        Console.WriteLine($"[{DateTime.Now}] Обработка команды от авторизованного пользователя {session.User.UserId}");
        await ProcessAuthorizedCommand(client, message, session);
    }

    #region Авторизация
    private static async Task StartAuthProcess(ITelegramBotClient client, long chatId)
    {
        Console.WriteLine($"[{DateTime.Now}] Начало процесса авторизации для {chatId}");

        var requestButton = new KeyboardButton("Предоставить номер телефона") { RequestContact = true };
        var keyboard = new ReplyKeyboardMarkup(requestButton) { ResizeKeyboard = true, OneTimeKeyboard = true };

        await client.SendMessage(
            chatId: chatId,
            text: "Для авторизации предоставьте номер телефона:",
            replyMarkup: keyboard);

        _userSessions[chatId].AuthState = AuthState.AwaitingPhone;
        Console.WriteLine($"[{DateTime.Now}] Установлено состояние AwaitingPhone для {chatId}");
    }

    private static async Task ProcessPhoneNumber(ITelegramBotClient client, Message message, UserSession session)
    {
        Console.WriteLine($"[{DateTime.Now}] Обработка номера телефона для {message.Chat.Id}");

        try
        {
            var phoneNumber = message.Contact.PhoneNumber;
            Console.WriteLine($"[{DateTime.Now}] Получен номер: {phoneNumber}");

            var cleanPhone = long.Parse(phoneNumber.Replace("+", ""));

            using var db = new UsersContext();
            var user = await db.Users
                .Include(u => u.Students)
                .Include(u => u.Employees)
                .FirstOrDefaultAsync(u => u.PhoneNumber == cleanPhone);

            if (user == null)
            {
                Console.WriteLine($"[{DateTime.Now}] Пользователь с номером {cleanPhone} не найден");
                await client.SendMessage(
                    chatId: message.Chat.Id,
                    text: "❌ Пользователь не найден. Обратитесь к администратору.",
                    replyMarkup: new ReplyKeyboardRemove());
                return;
            }

            // Обновляем TelegramId если нужно
            if (user.TelegramId == null || user.TelegramId != message.Chat.Id)
            {
                user.TelegramId = message.Chat.Id;
                await db.SaveChangesAsync();
                Console.WriteLine($"[{DateTime.Now}] Обновлен TelegramId для пользователя {user.UserId} на {message.Chat.Id}");
            }

            Console.WriteLine($"[{DateTime.Now}] Найден пользователь: {user.UserId} - {user.FullName}");
            session.User = user;

            if (user.Role == UserRole.Студент)
            {
                Console.WriteLine($"[{DateTime.Now}] Авторизация студента {user.UserId}");
                if (user.Students == null)
                {
                    Console.WriteLine($"[{DateTime.Now}] Ошибка: данные студента не найдены");
                    await client.SendMessage(
                        chatId: message.Chat.Id,
                        text: "❌ Данные студента не найдены. Обратитесь к администратору.");
                    return;
                }

                await CompleteAuth(client, message.Chat.Id, user);
            }
            else
            {
                Console.WriteLine($"[{DateTime.Now}] Требуется пароль для сотрудника {user.UserId}");
                session.AuthState = AuthState.AwaitingPassword;
                await client.SendMessage(
                    chatId: message.Chat.Id,
                    text: "🔑 Введите ваш пароль:",
                    replyMarkup: new ReplyKeyboardRemove());
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now}] Ошибка обработки номера: {ex}");
            await client.SendMessage(
                chatId: message.Chat.Id,
                text: "⚠ Ошибка обработки номера. Попробуйте снова.");
        }
    }

    private static async Task ProcessPassword(ITelegramBotClient client, Message message, UserSession session)
    {
        Console.WriteLine($"[{DateTime.Now}] Обработка пароля для {message.Chat.Id}");

        try
        {
            var password = message.Text;
            Console.WriteLine($"[{DateTime.Now}] Получен пароль: {password}");

            using var db = new EmployeesContext();
            var employee = await db.Employees
                .FirstOrDefaultAsync(e => e.EmployeeId == session.User.UserId && e.Password == password);

            if (employee == null)
            {
                Console.WriteLine($"[{DateTime.Now}] Неверный пароль для пользователя {session.User.UserId}");
                await client.SendMessage(
                    chatId: message.Chat.Id,
                    text: "❌ Неверный пароль. Попробуйте снова.");
                return;
            }

            Console.WriteLine($"[{DateTime.Now}] Успешная авторизация Сотрудника {session.User.UserId}");
            await CompleteAuth(client, message.Chat.Id, session.User);
            session.AuthState = AuthState.None;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now}] Ошибка проверки пароля: {ex}");
            await client.SendMessage(
                chatId: message.Chat.Id,
                text: "⚠ Ошибка проверки пароля. Попробуйте снова.");
        }
    }

    private static async Task CompleteAuth(ITelegramBotClient client, long chatId, Users user)
    {
        Console.WriteLine($"[{DateTime.Now}] Завершение авторизации для {user.UserId}");

        string roleText;
        if (user.Role == UserRole.Студент)
        {
            roleText = user.Students.StudentRole.ToString();
        }
        else
        {
            roleText = user.Employees.EmployeeRole.ToString();
        }

        await client.SendMessage(
            chatId: chatId,
            text: $"✅ Вы успешно авторизованы как {roleText} {user.FullName}",
            replyMarkup: new ReplyKeyboardRemove());

        await UpdateMenuCommands(client, chatId, user);
    }
    #endregion

    #region Меню команд
    private static async Task UpdateMenuCommands(ITelegramBotClient client, long chatId, Users user)
    {
        Console.WriteLine($"[{DateTime.Now}] Обновление меню команд для {user.UserId}");
        var commands = new List<BotCommand>();

        if (user.Role == UserRole.Студент)
        {
            commands.Add(new BotCommand { Command = "repair", Description = "Заявка на ремонт" });
            commands.Add(new BotCommand { Command = "duty", Description = "График дежурств" });
            commands.Add(new BotCommand { Command = "complaint", Description = "Отправить жалобу" });
            commands.Add(new BotCommand { Command = "educator", Description = "Дежурный воспитатель" });

            if (user.Students.StudentRole == StudentRole.Староста_этажа ||
                user.Students.StudentRole == StudentRole.Председатель_общежития)
            {
                commands.Add(new BotCommand { Command = "notification", Description = "Отправить уведомление" });
            }
        }
        else if (user.Role == UserRole.Сотрудник)
        {
            switch (user.Employees.EmployeeRole)
            {
                case EmployeeRole.Мастер:
                    commands.Add(new BotCommand { Command = "repair", Description = "Список заявок" });
                    break;

                case EmployeeRole.Воспитатель:
                case EmployeeRole.Дежурный_воспитатель:
                    commands.Add(new BotCommand { Command = "complaint", Description = "Жалобы" });
                    commands.Add(new BotCommand { Command = "educator", Description = "Назначить дежурного" });
                    commands.Add(new BotCommand { Command = "notification", Description = "Отправить уведомление" });
                    break;

                case EmployeeRole.Администратор:
                    commands.Add(new BotCommand { Command = "repair", Description = "Ремонт" });
                    commands.Add(new BotCommand { Command = "duty", Description = "Управление графиком" });
                    commands.Add(new BotCommand { Command = "complaint", Description = "Жалобы" });
                    commands.Add(new BotCommand { Command = "educator", Description = "Назначить дежурного" });
                    commands.Add(new BotCommand { Command = "notification", Description = "Отправить уведомление" });
                    break;
            }
        }
        commands.Add(new BotCommand { Command = "phonebook", Description = "Телефонный справочник" });

        await client.SetMyCommands(commands, scope: new BotCommandScopeAllPrivateChats(), languageCode: "ru");
        Console.WriteLine($"[{DateTime.Now}] Меню команд обновлено для {user.UserId}");
    }
    #endregion

    #region Обработка команд после авторизации
    private static async Task ProcessAuthorizedCommand(ITelegramBotClient client, Message message, UserSession session)
    {
        var command = message.Text?.Split(' ')[0].ToLower();
        session.CurrentCommand = command;

        Console.WriteLine($"[{DateTime.Now}] Пользователь {session.User.UserId} выполнил команду: {command}");

        switch (command)
        {
            case "/start":
                await client.SendMessage(message.Chat.Id, "Вы уже авторизованы. Используйте доступные команды.");
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

            default:
                await client.SendMessage(message.Chat.Id, "Неизвестная команда. Используйте меню.");
                break;
        }
    }
    #endregion

    #region Обработка заявок на ремонт
    private static async Task HandleRepairCommand(ITelegramBotClient client, Message message, UserSession session)
    {
        Console.WriteLine($"[{DateTime.Now}] Обработка команды repair для пользователя {session.User.UserId}");

        if (session.User.Role == UserRole.Студент)
        {
            Console.WriteLine($"[{DateTime.Now}] Студент создает заявку на ремонт");
            await StartRepairRequestCreation(client, message.Chat.Id);
            return;
        }

        switch (session.User.Employees.EmployeeRole)
        {
            case EmployeeRole.Мастер:
                Console.WriteLine($"[{DateTime.Now}] Мастер запросил список заявок");
                await ShowRepairRequestsForMaster(client, message.Chat.Id, session.User.UserId);
                break;

            case EmployeeRole.Воспитатель:
            case EmployeeRole.Дежурный_воспитатель:
                Console.WriteLine($"[{DateTime.Now}] Воспитатель создает заявку");
                await StartRepairRequestCreation(client, message.Chat.Id);
                break;

            case EmployeeRole.Администратор:
                Console.WriteLine($"[{DateTime.Now}] Администратор выбрал управление заявками");
                var keyboard = new ReplyKeyboardMarkup(new[]
                {
                    new KeyboardButton("Создать заявку"),
                    new KeyboardButton("Просмотреть заявки")
                })
                { ResizeKeyboard = true };

                await client.SendMessage(
                    chatId: message.Chat.Id,
                    text: "Выберите действие:",
                    replyMarkup: keyboard);
                break;

            default:
                await client.SendMessage(
                    message.Chat.Id,
                    "⛔ Эта команда недоступна для вашей роли");
                break;
        }
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
            session.TempData["problemType"] = message.Text;
            Console.WriteLine($"[{DateTime.Now}] Пользователь выбрал тип проблемы: {message.Text}");

            await client.SendMessage(
                chatId: chatId,
                text: "Укажите место, где требуется ремонт:",
                replyMarkup: new ReplyKeyboardRemove());

            session.TempData["repairState"] = "awaitingLocation";
        }
        else if (session.TempData["repairState"].ToString() == "awaitingLocation")
        {
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
            var problemType = session.TempData["problemType"].ToString();
            var location = session.TempData["location"].ToString();

            Console.WriteLine($"[{DateTime.Now}] Создание заявки с параметрами: " +
                             $"Тип: {problemType}, Место: {location}, Комментарий: {comment}");

            using var db = new RepairRequestsContext();
            var request = new RepairRequests
            {
                UserId = session.User.UserId,
                Location = location,
                Problem = Enum.Parse<ProblemType>(problemType),
                UserComment = comment,
                Status = RequestStatus.Новая,
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

    private static async Task ShowRepairRequestsForMaster(ITelegramBotClient client, long chatId, int masterId)
    {
        Console.WriteLine($"[{DateTime.Now}] Загрузка заявок для мастера {masterId}");

        using var db = new RepairRequestsContext();
        var requests = await db.RepairRequests
            .Where(r => r.Status == RequestStatus.Новая || r.Status == RequestStatus.В_процессе)
            .Include(r => r.Users)
            .ToListAsync();

        if (!requests.Any())
        {
            Console.WriteLine($"[{DateTime.Now}] Нет активных заявок для мастера");
            await client.SendMessage(chatId, "Нет активных заявок на ремонт");
            return;
        }

        var message = new StringBuilder("📋 Список заявок на ремонт:\n\n");
        var keyboardButtons = new List<InlineKeyboardButton[]>();

        foreach (var request in requests)
        {
            message.AppendLine($"#{request.RequestId} - {request.Problem} - {request.Location} - {request.Status}");

            if (request.Status == RequestStatus.Новая)
            {
                keyboardButtons.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData($"Принять #{request.RequestId}", $"accept_{request.RequestId}"),
                    InlineKeyboardButton.WithCallbackData($"Отклонить #{request.RequestId}", $"reject_{request.RequestId}")
                });
            }
            else if (request.Status == RequestStatus.В_процессе)
            {
                keyboardButtons.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData($"Завершить #{request.RequestId}", $"complete_{request.RequestId}")
                });
            }
        }

        var keyboard = new InlineKeyboardMarkup(keyboardButtons);
        Console.WriteLine($"[{DateTime.Now}] Отправка списка заявок мастеру");
        await client.SendMessage(chatId, message.ToString(), replyMarkup: keyboard);
    }

    private static async Task HandleRepairCallback(ITelegramBotClient client, CallbackQuery callbackQuery, UserSession session)
    {
        var data = callbackQuery.Data;
        var chatId = callbackQuery.Message.Chat.Id;
        var requestId = int.Parse(data.Split('_')[1]);

        Console.WriteLine($"[{DateTime.Now}] Обработка callback для заявки #{requestId}: {data}");

        using var db = new RepairRequestsContext();
        var request = await db.RepairRequests.FindAsync(requestId);

        if (request == null)
        {
            Console.WriteLine($"[{DateTime.Now}] Заявка #{requestId} не найдена");
            await client.AnswerCallbackQuery(callbackQuery.Id, "Заявка не найдена");
            return;
        }

        if (data.StartsWith("accept_"))
        {
            request.Status = RequestStatus.В_процессе;
            request.MasterId = session.User.UserId;
            request.LastStatusChange = DateTime.Now;
            await db.SaveChangesAsync();
            Console.WriteLine($"[{DateTime.Now}] Заявка #{requestId} принята в работу");

            await client.AnswerCallbackQuery(callbackQuery.Id, "Заявка принята в работу");
            await client.EditMessageText(
                chatId: chatId,
                messageId: callbackQuery.Message.MessageId,
                text: callbackQuery.Message.Text + $"\n\n✅ Вы приняли заявку #{requestId}",
                replyMarkup: null);

            await NotifyUserAboutRequestStatus(client, request.UserId, requestId, "принята в работу");
        }
        else if (data.StartsWith("reject_"))
        {
            request.Status = RequestStatus.Отклонена;
            request.LastStatusChange = DateTime.Now;
            await db.SaveChangesAsync();
            Console.WriteLine($"[{DateTime.Now}] Заявка #{requestId} отклонена");

            await client.AnswerCallbackQuery(callbackQuery.Id, "Заявка отклонена");
            await client.EditMessageText(
                chatId: chatId,
                messageId: callbackQuery.Message.MessageId,
                text: callbackQuery.Message.Text + $"\n\n❌ Вы отклонили заявку #{requestId}",
                replyMarkup: null);

            await NotifyUserAboutRequestStatus(client, request.UserId, requestId, "отклонена");
        }
        else if (data.StartsWith("complete_"))
        {
            request.Status = RequestStatus.Завершена;
            request.LastStatusChange = DateTime.Now;
            await db.SaveChangesAsync();
            Console.WriteLine($"[{DateTime.Now}] Заявка #{requestId} завершена");

            await client.AnswerCallbackQuery(callbackQuery.Id, "Заявка завершена");
            await client.EditMessageText(
                chatId: chatId,
                messageId: callbackQuery.Message.MessageId,
                text: callbackQuery.Message.Text + $"\n\n🏁 Заявка #{requestId} завершена",
                replyMarkup: null);

            await NotifyUserAboutRequestStatus(client, request.UserId, requestId, "завершена");
        }
    }

    private static async Task NotifyUserAboutRequestStatus(ITelegramBotClient client, int userId, int requestId, string statusText)
    {
        Console.WriteLine($"[{DateTime.Now}] Уведомление пользователя {userId} о статусе заявки #{requestId}");

        using var db = new UsersContext();
        var user = await db.Users.FindAsync(userId);

        if (user?.TelegramId != null)
        {
            await client.SendMessage(
                chatId: user.TelegramId.Value,
                text: $"ℹ Статус вашей заявки #{requestId} изменен: {statusText}");
        }
    }
    #endregion

    #region Модуль жалоб
    private static async Task HandleComplaintCommand(ITelegramBotClient client, Message message, UserSession session)
    {
        Console.WriteLine($"[{DateTime.Now}] Обработка команды complaint для пользователя {session.User.UserId}");

        if (session.User.Role == UserRole.Студент)
        {
            Console.WriteLine($"[{DateTime.Now}] Студент создает жалобу");
            await StartComplaintProcess(client, message.Chat.Id);
        }
        else if (session.User.Role == UserRole.Сотрудник)
        {
            switch (session.User.Employees.EmployeeRole)
            {
                case EmployeeRole.Воспитатель:
                case EmployeeRole.Дежурный_воспитатель:
                case EmployeeRole.Администратор:
                    Console.WriteLine($"[{DateTime.Now}] Просмотр жалоб сотрудником");
                    await ShowComplaintsList(client, message.Chat.Id);
                    break;

                case EmployeeRole.Мастер:
                    Console.WriteLine($"[{DateTime.Now}] Мастер пытается получить доступ к жалобам");
                    await client.SendMessage(
                        message.Chat.Id,
                        "⛔ У вас нет доступа к просмотру жалоб",
                        replyMarkup: new ReplyKeyboardRemove());
                    break;
            }
        }
    }

    private static async Task StartComplaintProcess(ITelegramBotClient client, long chatId)
    {
        Console.WriteLine($"[{DateTime.Now}] Начало процесса создания жалобы для {chatId}");

        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Анонимно", "complaint_anonymous"),
                InlineKeyboardButton.WithCallbackData("Открыто", "complaint_public")
            }
        });

        await client.SendMessage(
            chatId: chatId,
            text: "Выберите тип жалобы:",
            replyMarkup: keyboard);

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

        Console.WriteLine($"[{DateTime.Now}] Обработка callback жалобы: {data}");

        if (data == "complaint_anonymous" || data == "complaint_public")
        {
            var isAnonymous = data == "complaint_anonymous";
            Console.WriteLine($"[{DateTime.Now}] Пользователь выбрал тип жалобы: {(isAnonymous ? "Анонимно" : "Открыто")}");

            session.TempData["complaintState"] = "awaitingText";
            session.TempData["isAnonymous"] = isAnonymous;

            await client.EditMessageText(
                chatId: chatId,
                messageId: callbackQuery.Message.MessageId,
                text: "✍ Напишите текст вашей жалобы:",
                replyMarkup: null);
        }
        else if (data.StartsWith("complaint_action_"))
        {
            var parts = data.Split('_');
            var action = parts[2];
            var complaintId = int.Parse(parts[3]);
            Console.WriteLine($"[{DateTime.Now}] Обработка действия над жалобой #{complaintId}: {action}");

            using var db = new ComplaintsContext();
            var complaint = await db.Complaints.FindAsync(complaintId);

            if (complaint == null)
            {
                Console.WriteLine($"[{DateTime.Now}] Жалоба #{complaintId} не найдена");
                await client.AnswerCallbackQuery(callbackQuery.Id, "Жалоба не найдена");
                return;
            }

            switch (action)
            {
                case "review":
                    complaint.Status = ComplaintStatus.На_рассмотрении;
                    complaint.ReviewedBy = session.User.UserId;
                    Console.WriteLine($"[{DateTime.Now}] Жалоба #{complaintId} взята на рассмотрение");
                    break;

                case "complete":
                    complaint.Status = ComplaintStatus.Выполнена;
                    Console.WriteLine($"[{DateTime.Now}] Жалоба #{complaintId} выполнена");
                    break;

                case "reject":
                    complaint.Status = ComplaintStatus.Отклонена;
                    Console.WriteLine($"[{DateTime.Now}] Жалоба #{complaintId} отклонена");
                    break;
            }

            complaint.LastStatusChange = DateTime.Now;
            await db.SaveChangesAsync();

            await client.AnswerCallbackQuery(callbackQuery.Id, $"Статус изменен на: {complaint.Status}");
            await client.EditMessageText(
                chatId: chatId,
                messageId: callbackQuery.Message.MessageId,
                text: callbackQuery.Message.Text + $"\n\nСтатус изменен: {complaint.Status}",
                replyMarkup: null);

            if (complaint.UserId.HasValue && complaint.UserId.Value != session.User.UserId)
            {
                await NotifyUserAboutComplaintStatus(client, complaint.UserId.Value, complaintId, complaint.Status);
            }
        }
    }

    private static async Task ProcessComplaintText(ITelegramBotClient client, Message message, UserSession session)
    {
        var chatId = message.Chat.Id;
        var complaintText = message.Text;
        var isAnonymous = (bool)session.TempData["isAnonymous"];

        Console.WriteLine($"[{DateTime.Now}] Создание жалобы: Анонимно={isAnonymous}, Текст: {complaintText}");

        using var db = new ComplaintsContext();
        var complaint = new Complaints
        {
            UserId = isAnonymous ? null : (int?)session.User.UserId,
            ComplaintText = complaintText,
            Status = ComplaintStatus.Новая,
            SubmissionDate = DateTime.Now,
            LastStatusChange = DateTime.Now
        };

        await db.Complaints.AddAsync(complaint);
        await db.SaveChangesAsync();
        Console.WriteLine($"[{DateTime.Now}] Жалоба #{complaint.ComplaintId} сохранена в БД");

        session.TempData.Remove("complaintState");
        session.TempData.Remove("isAnonymous");

        await client.SendMessage(
            chatId: chatId,
            text: $"✅ Жалоба #{complaint.ComplaintId} успешно отправлена!\n" +
                  $"Тип: {(isAnonymous ? "Анонимно" : "Открыто")}");

        await NotifyEducatorsAboutNewComplaint(client, complaint.ComplaintId);
    }

    private static async Task ShowComplaintsList(ITelegramBotClient client, long chatId)
    {
        Console.WriteLine($"[{DateTime.Now}] Загрузка списка жалоб");

        using var db = new ComplaintsContext();
        var complaints = await db.Complaints
            .Include(c => c.Users)
            .Where(c => c.Status != ComplaintStatus.Выполнена && c.Status != ComplaintStatus.Отклонена)
            .OrderByDescending(c => c.SubmissionDate)
            .ToListAsync();

        if (!complaints.Any())
        {
            Console.WriteLine($"[{DateTime.Now}] Нет активных жалоб");
            await client.SendMessage(chatId, "Нет активных жалоб");
            return;
        }

        var message = new StringBuilder("📋 Список жалоб:\n\n");
        var keyboardButtons = new List<InlineKeyboardButton[]>();

        foreach (var complaint in complaints)
        {
            var author = complaint.UserId == null ? "Аноним" : complaint.Users?.FullName ?? "Неизвестно";
            message.AppendLine($"#{complaint.ComplaintId} - {author} - {complaint.Status}");

            var buttons = new List<InlineKeyboardButton>();

            if (complaint.Status == ComplaintStatus.Новая)
            {
                keyboardButtons.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData($"Взять в работу #{complaint.ComplaintId}", $"complaint_action_review_{complaint.ComplaintId}"),
                    InlineKeyboardButton.WithCallbackData($"Отклонить #{complaint.ComplaintId}", $"complaint_action_reject_{complaint.ComplaintId}")
                });
            }
            else if (complaint.Status == ComplaintStatus.На_рассмотрении)
            {
                keyboardButtons.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData($"Завершить #{complaint.ComplaintId}", $"complaint_action_complete_{complaint.ComplaintId}")
                });
            }

            keyboardButtons.Add(buttons.ToArray());
        }

        var keyboard = new InlineKeyboardMarkup(keyboardButtons);
        Console.WriteLine($"[{DateTime.Now}] Отправка списка жалоб");
        await client.SendMessage(chatId, message.ToString(), replyMarkup: keyboard);
    }

    private static async Task NotifyEducatorsAboutNewComplaint(ITelegramBotClient client, int complaintId)
    {
        Console.WriteLine($"[{DateTime.Now}] Уведомление воспитателей о новой жалобе #{complaintId}");

        using var db = new UsersContext();
        var educators = await db.Users
            .Include(u => u.Employees)
            .Where(u => u.Role == UserRole.Сотрудник &&
                      (u.Employees.EmployeeRole == EmployeeRole.Воспитатель ||
                       u.Employees.EmployeeRole == EmployeeRole.Дежурный_воспитатель ||
                       u.Employees.EmployeeRole == EmployeeRole.Администратор) &&
                      u.TelegramId != null)
            .ToListAsync();

        foreach (var educator in educators)
        {
            Console.WriteLine($"[{DateTime.Now}] Отправка уведомления воспитателю {educator.UserId}");
            await client.SendMessage(
                chatId: educator.TelegramId.Value,
                text: $"❗ Поступила новая жалоба #{complaintId}");
        }
    }

    private static async Task NotifyUserAboutComplaintStatus(ITelegramBotClient client, int userId, int complaintId, ComplaintStatus status)
    {
        Console.WriteLine($"[{DateTime.Now}] Уведомление пользователя {userId} о статусе жалобы #{complaintId}");

        using var db = new UsersContext();
        var user = await db.Users.FindAsync(userId);

        if (user?.TelegramId != null)
        {
            await client.SendMessage(
                chatId: user.TelegramId.Value,
                text: $"ℹ Статус вашей жалобы #{complaintId} изменен: {status}");
        }
    }
    #endregion

    #region Модуль телефонного справочника

    private static async Task HandlePhonebookCommand(ITelegramBotClient client, long chatId)
    {
        Console.WriteLine($"[{DateTime.Now}] Пользователь запросил телефонный справочник");

        try
        {
            var message = new StringBuilder("📞 Телефоны:\n\n");

            message.AppendLine("8 (342) 206-02-54 - пост охраны\n");
            message.AppendLine("8 (342) 206-03-40 (доб. 904) - и.о. заведующего общежитием Козлов Иван Валерьевич\n");
            message.AppendLine("8 (342) 206-03-40 (доб. 424) - воспитатель Габдулова Ильсина Наилевна (2,3,4 этажи; дневное время), каб. 424\n");
            message.AppendLine("8 (342) 206-03-40 (доб. 524) - воспитатель Микова Елена Александровна (5,6,7 этажи; дневное время), каб. 524\n");
            message.AppendLine("8 (342) 206-03-40 (доб. 324) - дежурный по общежитию (2,3,4 этажи; вечернее и ночное время), каб. 324\n");
            message.AppendLine("8 (342) 206-03-40 (доб. 624) - дежурный по общежитию (5,6,7 этажи; вечернее и ночное время), каб. 624");

            Console.WriteLine($"[{DateTime.Now}] Отправка телефонного справочника пользователю");
            await client.SendMessage(chatId, message.ToString());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now}] Ошибка при формировании телефонного справочника: {ex}");
            await client.SendMessage(chatId, "⚠ Произошла ошибка при формировании телефонного справочника");
        }
    }

    #endregion


    #region Обработка CallbackQuery
    private static async Task HandleCallbackQuery(ITelegramBotClient client, CallbackQuery callbackQuery)
    {
        var chatId = callbackQuery.Message.Chat.Id;
        Console.WriteLine($"[{DateTime.Now}] Обработка callback от {chatId}: {callbackQuery.Data}");

        if (!_userSessions.TryGetValue(chatId, out var session))
        {
            Console.WriteLine($"[{DateTime.Now}] Сессия для чата {chatId} не найдена");
            await client.AnswerCallbackQuery(callbackQuery.Id, "Сессия не найдена");
            return;
        }

        if (callbackQuery.Data.StartsWith("accept_") ||
            callbackQuery.Data.StartsWith("reject_") ||
            callbackQuery.Data.StartsWith("complete_"))
        {
            await HandleRepairCallback(client, callbackQuery, session);
            return;
        }

        if (callbackQuery.Data.StartsWith("complaint_"))
        {
            await HandleComplaintCallback(client, callbackQuery, session);
            return;
        }

        Console.WriteLine($"[{DateTime.Now}] Неизвестный callback: {callbackQuery.Data}");
        await client.AnswerCallbackQuery(callbackQuery.Id, "Команда в разработке");
    }
    #endregion

    private static Task ErrorHandler(ITelegramBotClient botClient, Exception error, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[{DateTime.Now}] ⚠ ОШИБКА: {error}");
        return Task.CompletedTask;
    }
}