using Telegram.Bot.Types;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using static Telegram.Bot.TelegramBotClient;
using System;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.ReplyMarkups;
using DormitoryPAT.Models;
using DormitoryPAT.Context;
using Microsoft.EntityFrameworkCore;

class Program
{
    private static ITelegramBotClient _botClient;
    private static ReceiverOptions _receiverOptions;
    private static Dictionary<long, UserSession> _userSessions = new();

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
        Console.WriteLine("Запуск бота...");      

        _botClient = new TelegramBotClient("7681929292:AAELFhLTiH3c4KZtnRrPY9aGD6gYyLWVo5E");
        _receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new[]
            {
                UpdateType.Message,
                UpdateType.CallbackQuery
            },
            DropPendingUpdates = true,
        };

        using var cts = new CancellationTokenSource();
        
        _botClient.StartReceiving(UpdateHandler, ErrorHandler, _receiverOptions, cts.Token);

        var me = await _botClient.GetMe();
        Console.WriteLine($"{me.FirstName} запущен! @{me.Username}");
        Console.WriteLine("Ожидание сообщений...");

        await Task.Delay(-1);
    }

    private static async Task UpdateHandler(ITelegramBotClient client, Update update, CancellationToken token)
    {
        try
        {
            Console.WriteLine($"Получено обновление: {update.Type}");

            switch (update.Type)
            {
                case UpdateType.Message when update.Message?.Text != null:
                    Console.WriteLine($"Сообщение от {update.Message.From.Id}: {update.Message.Text}");
                    await HandleMessage(client, update.Message);
                    break;

                case UpdateType.Message when update.Message?.Contact != null:
                    Console.WriteLine($"Получен контакт от {update.Message.From.Id}");
                    await HandleMessage(client, update.Message);
                    break;

                case UpdateType.CallbackQuery:
                    Console.WriteLine($"Callback от {update.CallbackQuery.From.Id}: {update.CallbackQuery.Data}");
                    await HandleCallbackQuery(client, update.CallbackQuery);
                    break;

                default:
                    Console.WriteLine($"Необработанный тип обновления: {update.Type}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка обработки обновления: {ex}");
        }
    }

    private static async Task HandleMessage(ITelegramBotClient client, Message message)
    {
        var chatId = message.Chat.Id;
        Console.WriteLine($"Обработка сообщения в чате {chatId}");

        if (!_userSessions.TryGetValue(chatId, out var session))
        {
            Console.WriteLine($"Создана новая сессия для чата {chatId}");
            session = new UserSession();
            _userSessions[chatId] = session;
        }

        if (message.Text == "/start")
        {
            Console.WriteLine($"Команда /start от {chatId}");

            await StartAuthProcess(client, message.Chat.Id);
            return;
        }

        if (session.AuthState == AuthState.AwaitingPhone && message.Contact != null)
        {
            Console.WriteLine($"Получен контакт от {chatId}");
            await ProcessPhoneNumber(client, message, session);
            return;
        }

        if (session.AuthState == AuthState.AwaitingPassword)
        {
            Console.WriteLine($"Получен пароль от {chatId}");
            await ProcessPassword(client, message, session);
            return;
        }

        if (session.User == null)
        {
            Console.WriteLine($"Пользователь не авторизован в чате {chatId}");
            await client.SendMessage(chatId, "Пожалуйста, начните с команды /start");
            return;
        }

        Console.WriteLine($"Обработка команды от авторизованного пользователя {session.User.UserId}");
        await ProcessAuthorizedCommand(client, message, session);
    }

    private static async Task StartAuthProcess(ITelegramBotClient client, long chatId)
    {
        Console.WriteLine($"Начало процесса авторизации для {chatId}");

        var requestButton = new KeyboardButton("Предоставить номер телефона")
        {
            RequestContact = true
        };

        var keyboard = new ReplyKeyboardMarkup(requestButton)
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = true
        };

        await client.SendMessage(
            chatId: chatId,
            text: "Для авторизации предоставьте номер телефона:",
            replyMarkup: keyboard);

        _userSessions[chatId].AuthState = AuthState.AwaitingPhone;
        Console.WriteLine($"Установлено состояние AwaitingPhone для {chatId}");
    }

    private static async Task ProcessPhoneNumber(ITelegramBotClient client, Message message, UserSession session)
    {
        Console.WriteLine($"Обработка номера телефона для {message.Chat.Id}");

        try
        {
            var phoneNumber = message.Contact.PhoneNumber;
            Console.WriteLine($"Получен номер: {phoneNumber}");

            var cleanPhone = long.Parse(phoneNumber.Replace("+", ""));

            using var db = new UsersContext();
            var user = db.Users
                .Include(u => u.Students) // Явно загружаем данные студента
                .Include(u => u.Employees) // Явно загружаем данные сотрудника
                .FirstOrDefault(u => u.PhoneNumber == cleanPhone);

            if (user == null)
            {
                Console.WriteLine($"Пользователь с номером {cleanPhone} не найден");
                await client.SendMessage(
                    chatId: message.Chat.Id,
                    text: "❌ Пользователь не найден. Обратитесь к администратору.",
                    replyMarkup: new ReplyKeyboardRemove());
                return;
            }

            // Обновляем TelegramId, если он пустой или отличается от текущего
            if (user.TelegramId == null || user.TelegramId != message.Chat.Id)
            {
                user.TelegramId = message.Chat.Id;
                db.SaveChanges();
                Console.WriteLine($"Обновлен TelegramId для пользователя {user.UserId} на {message.Chat.Id}");
            }

            Console.WriteLine($"Найден пользователь: {user.UserId} - {user.FullName}");
            session.User = user;

            if (user.Role == UserRole.Студент)
            {
                Console.WriteLine($"Авторизация студента {user.UserId}");
                if (user.Students == null)
                {
                    Console.WriteLine("Ошибка: данные студента не найдены");
                    await client.SendMessage(
                        chatId: message.Chat.Id,
                        text: "❌ Данные студента не найдены. Обратитесь к администратору.");
                    return;
                }

                await CompleteAuth(client, message.Chat.Id, user);
            }
            else
            {
                Console.WriteLine($"Требуется пароль для сотрудника {user.UserId}");
                session.AuthState = AuthState.AwaitingPassword;
                await client.SendMessage(
                    chatId: message.Chat.Id,
                    text: "🔑 Введите ваш пароль:",
                    replyMarkup: new ReplyKeyboardRemove());
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка обработки номера: {ex}");
            await client.SendMessage(
                chatId: message.Chat.Id,
                text: "⚠ Ошибка обработки номера. Попробуйте снова.");
        }
    }

    private static async Task ProcessPassword(ITelegramBotClient client, Message message, UserSession session)
    {
        Console.WriteLine($"Обработка пароля для {message.Chat.Id}");

        try
        {
            var password = message.Text;
            Console.WriteLine($"Получен пароль: {password}");

            using var db = new EmployeesContext();
            var employee = db.Employees
                .FirstOrDefault(e => e.EmployeeId == session.User.UserId && e.Password == password);

            if (employee == null)
            {
                Console.WriteLine($"Неверный пароль для пользователя {session.User.UserId}");
                await client.SendMessage(
                    chatId: message.Chat.Id,
                    text: "❌ Неверный пароль. Попробуйте снова.");
                return;
            }

            Console.WriteLine($"Успешная авторизация Сотрудника {session.User.UserId}");
            await CompleteAuth(client, message.Chat.Id, session.User);
            session.AuthState = AuthState.None;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка проверки пароля: {ex}");
            await client.SendMessage(
                chatId: message.Chat.Id,
                text: "⚠ Ошибка проверки пароля. Попробуйте снова.");
        }
    }

    private static async Task CompleteAuth(ITelegramBotClient client, long chatId, Users user)
    {
        Console.WriteLine($"Завершение авторизации для {user.UserId}");

        string roleText;
        if (user.Role == UserRole.Студент)
        {
            if (user.Students == null)
            {
                await client.SendMessage(
                    chatId: chatId,
                    text: "❌ Ошибка: данные студента не найдены");
                return;
            }
            var student = user.Students;
            roleText = student.StudentRole.ToString();
        }
        else
        {
            var employee = user.Employees;
            roleText = employee.EmployeeRole.ToString();
        }

        await client.SendMessage(
            chatId: chatId,
            text: $"✅ Вы успешно авторизованы как {roleText} {user.FullName}",
            replyMarkup: new ReplyKeyboardRemove());

        await UpdateMenuCommands(client, chatId, user);
    }

    private static async Task UpdateMenuCommands(ITelegramBotClient client, long chatId, Users user)
    {        
        Console.WriteLine($"Обновление меню команд для {user.UserId}");
        var commands = new List<BotCommand>();

        if (user.Role == UserRole.Студент)
        {
            if (user.Students == null)
            {
                Console.WriteLine("Ошибка: данные студента не найдены");
                await client.SendMessage(
                    chatId: chatId,
                    text: "❌ Данные студента не загружены. Обратитесь к администратору.");
                return;
            }

            var student = user.Students;

            // Основные команды для всех студентов
            commands.Add(new BotCommand { Command = "repair", Description = "Заявка на ремонт" });
            commands.Add(new BotCommand { Command = "duty", Description = "График дежурств" });
            commands.Add(new BotCommand { Command = "complaint", Description = "Отправить жалобу" });
            commands.Add(new BotCommand { Command = "phonebook", Description = "Телефонный справочник" });
            commands.Add(new BotCommand { Command = "educator", Description = "Дежурный воспитатель" });

            // Проверяем student на null перед доступом к StudentRole
            if (student.StudentRole == StudentRole.Староста_этажа || student.StudentRole == StudentRole.Председатель_общежития)
            {                
                commands.Add(new BotCommand { Command = "notification", Description = "Отправить уведомление" });
            }
        }
        else if (user.Role == UserRole.Сотрудник)
        {
            var employee = user.Employees;

            if (employee != null) // Добавляем проверку и для employee
            {
                switch (employee.EmployeeRole)
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
                        commands.Add(new BotCommand { Command = "phonebook", Description = "Телефонный справочник" });
                        break;
                }
            }
        }

        await client.SetMyCommands(
            commands: commands,
            scope: new BotCommandScopeAllPrivateChats(),
            languageCode: "ru");

        Console.WriteLine($"Меню команд обновлено для {user.UserId}");
    }

    private static async Task ProcessAuthorizedCommand(ITelegramBotClient client, Message message, UserSession session)
    {
        Console.WriteLine($"Обработка команды от {session.User.UserId}");

        var command = message.Text?.Split(' ')[0].ToLower();
        session.CurrentCommand = command;

        Console.WriteLine($"Текущая команда: {command}");

        switch (command)
        {
            case "/start":
                await client.SendMessage(message.Chat.Id, "Вы уже авторизованы. Используйте доступные команды.");
                break;

            case "/repair":
                await HandleRepairCommand(client, message, session);
                break;

            case "/duty":
                await HandleDutyCommand(client, message, session);
                break;

            case "/complaint":
                await HandleComplaintCommand(client, message, session);
                break;

            case "/educator":
                await HandleEducatorCommand(client, message, session);
                break;

            case "/phonebook":
                await HandlePhonebookCommand(client, message.Chat.Id);
                break;

            case "/notification":
                await HandleNotificationCommand(client, message, session);
                break;

            default:
                await client.SendMessage(message.Chat.Id, "Неизвестная команда. Используйте меню.");
                break;
        }
    }

    private static async Task HandleRepairCommand(ITelegramBotClient client, Message message, UserSession session)
    {
        if (session.User.Role == UserRole.Студент)
        {
            // Студенты, старосты и председатели могут создавать заявки
            await client.SendMessage(
                message.Chat.Id,
                "Опишите проблему для ремонта:",
                replyMarkup: new ReplyKeyboardRemove());

            session.TempData["repairState"] = "awaitingDescription";
        }
        else if (session.User.Role == UserRole.Сотрудник)
        {
            switch (session.User.Employees.EmployeeRole)
            {
                case EmployeeRole.Мастер:
                    // Мастер видит список заявок
                    await ShowRepairRequests(message.Chat.Id);
                    break;

                case EmployeeRole.Администратор:
                    // Админ может и создавать и просматривать
                    var keyboard = new ReplyKeyboardMarkup(new[]
                    {
                    new KeyboardButton("Создать заявку"),
                    new KeyboardButton("Просмотреть заявки")
                })
                    { ResizeKeyboard = true };

                    await client.SendMessage(
                        message.Chat.Id,
                        "Выберите действие:",
                        replyMarkup: keyboard);
                    break;

                default:
                    await client.SendMessage(
                        message.Chat.Id,
                        "Эта команда недоступна для вашей роли.");
                    break;
            }
        }
    }

    private static async Task HandleDutyCommand(ITelegramBotClient client, Message message, UserSession session)
    {
        if (session.User.Role == UserRole.Студент)
        {
            var student = session.User.Students;

            if (student.StudentRole == StudentRole.Студент)
            {
                // Обычный студент - просмотр своего графика
                await ShowStudentDutySchedule(message.Chat.Id, session.User.UserId);
            }
            else if (student.StudentRole == StudentRole.Староста_этажа)
            {
                // Староста - управление графиком своего этажа
                await ShowFloorDutyManagement(message.Chat.Id, student.Floor);
            }
            else if (student.StudentRole == StudentRole.Председатель_общежития)
            {
                // Председатель - управление всем общежитием
                await ShowDormitoryDutyManagement(message.Chat.Id);
            }
        }
        else if (session.User.Role == UserRole.Сотрудник)
        {
            switch (session.User.Employees.EmployeeRole)
            {
                case EmployeeRole.Мастер:
                    await client.SendMessage(
                        message.Chat.Id,
                        "Эта команда недоступна для мастера.");
                    break;

                case EmployeeRole.Воспитатель:
                case EmployeeRole.Дежурный_воспитатель:
                    // Просмотр графика
                    await ShowDutySchedule(message.Chat.Id);
                    break;

                case EmployeeRole.Администратор:
                    // Полное управление
                    await ShowDormitoryDutyManagement(message.Chat.Id);
                    break;
            }
        }
    }

    private static async Task HandleComplaintCommand(ITelegramBotClient client, Message message, UserSession session)
    {
        if (session.User.Role == UserRole.Студент)
        {
            // Все студенты могут жаловаться
            await client.SendMessage(
                message.Chat.Id,
                "Опишите вашу жалобу:",
                replyMarkup: new ReplyKeyboardRemove());

            session.TempData["complaintState"] = "awaitingDescription";
        }
        else if (session.User.Role == UserRole.Сотрудник)
        {
            switch (session.User.Employees.EmployeeRole)
            {
                case EmployeeRole.Мастер:
                    await client.SendMessage(
                        message.Chat.Id,
                        "Эта команда недоступна для мастера.");
                    break;

                case EmployeeRole.Воспитатель:
                case EmployeeRole.Дежурный_воспитатель:
                    // Просмотр жалоб
                    await ShowComplaintsList(message.Chat.Id);
                    break;

                case EmployeeRole.Администратор:
                    // Админ может и жаловаться и просматривать
                    var keyboard = new ReplyKeyboardMarkup(new[]
                    {
                    new KeyboardButton("Отправить жалобу"),
                    new KeyboardButton("Просмотреть жалобы")
                })
                    { ResizeKeyboard = true };

                    await client.SendMessage(
                        message.Chat.Id,
                        "Выберите действие:",
                        replyMarkup: keyboard);
                    break;
            }
        }
    }

    private static async Task HandleEducatorCommand(ITelegramBotClient client, Message message, UserSession session)
    {
        if (session.User.Role == UserRole.Студент)
        {
            // Студенты только просматривают
            await ShowCurrentEducator(message.Chat.Id);
        }
        else if (session.User.Role == UserRole.Сотрудник)
        {
            switch (session.User.Employees.EmployeeRole)
            {
                case EmployeeRole.Мастер:
                    await client.SendMessage(
                        message.Chat.Id,
                        "Эта команда недоступна для мастера.");
                    break;

                case EmployeeRole.Воспитатель:
                case EmployeeRole.Дежурный_воспитатель:
                case EmployeeRole.Администратор:
                    // Могут назначать и просматривать
                    var keyboard = new InlineKeyboardMarkup(new[]
                    {
                    InlineKeyboardButton.WithCallbackData("Просмотреть", "educator_view"),
                    InlineKeyboardButton.WithCallbackData("Назначить", "educator_set")
                });

                    await client.SendMessage(
                        message.Chat.Id,
                        "Выберите действие:",
                        replyMarkup: keyboard);
                    break;
            }
        }
    }

    private static async Task HandlePhonebookCommand(ITelegramBotClient client, long chatId)
    {
        // Для всех одинаковая реализация
        await ShowPhonebook(chatId);
    }

    private static async Task HandleNotificationCommand(ITelegramBotClient client, Message message, UserSession session)
    {
        if (session.User.Role == UserRole.Студент)
        {
            var student = session.User.Students;

            if (student.StudentRole == StudentRole.Староста_этажа)
            {
                // Староста может отправлять уведомления своему этажу
                await SendNotificationToFloor(message.Chat.Id, student.Floor);
            }
            else if (student.StudentRole == StudentRole.Председатель_общежития)
            {
                // Председатель - всему общежитию
                await SendNotificationToDormitory(message.Chat.Id);
            }
            else
            {
                await client.SendMessage(
                    message.Chat.Id,
                    "Эта команда недоступна для студентов.");
            }
        }
        else if (session.User.Role == UserRole.Сотрудник)
        {
            switch (session.User.Employees.EmployeeRole)
            {
                case EmployeeRole.Мастер:
                    await client.SendMessage(
                        message.Chat.Id,
                        "Эта команда недоступна для мастера.");
                    break;

                case EmployeeRole.Воспитатель:
                case EmployeeRole.Дежурный_воспитатель:
                case EmployeeRole.Администратор:
                    await SendNotificationToDormitory(message.Chat.Id);
                    break;
            }
        }
    }

    // Вспомогательные методы для каждой команды
    private static async Task ShowRepairRequests(long chatId)
    {
        // Реализация показа заявок
    }

    private static async Task ShowStudentDutySchedule(long chatId, int userId)
    {
        // Реализация показа графика для студента
    }

    private static async Task ShowFloorDutyManagement(long chatId, int floor)
    {
        // Реализация управления графиком этажа
    }

    private static async Task ShowDormitoryDutyManagement(long chatId)
    {
        // Реализация
    }

    private static async Task ShowDutySchedule(long chatId)
    {
        // Реализация
    }

    private static async Task ShowComplaintsList(long chatId)
    {
        // Реализация
    }

    private static async Task ShowCurrentEducator(long chatId)
    {
        // Реализация 
    }

    private static async Task ShowPhonebook(long chatId)
    {
        // Реализация
    }

    private static async Task SendNotificationToFloor(long chatId, int floor)
    {
        // Реализация
    }
    private static async Task SendNotificationToDormitory(long chatId)
    {
        // Реализация
    }

    private static async Task HandleCallbackQuery(ITelegramBotClient client, CallbackQuery callbackQuery)
    {
        
    }    

    private static Task ErrorHandler(ITelegramBotClient botClient, Exception error, CancellationToken cancellationToken)
    {
        var errorMessage = error switch
        {
            ApiRequestException apiRequestException
                => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => error.ToString()
        };

        Console.WriteLine($"⚠ ОШИБКА: {errorMessage}");
        return Task.CompletedTask;
    }
}