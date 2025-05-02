using Telegram.Bot.Types;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using static Telegram.Bot.TelegramBotClient;
using System;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.ReplyMarkups;

class Program
{
    private static ITelegramBotClient _botClient;
    private static ReceiverOptions _receiverOptions;

    static async Task Main(string[] args)
    {
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
        Console.WriteLine($"{me.FirstName} запущен!");

        await Task.Delay(-1);
    }

    private static async Task UpdateHandler(ITelegramBotClient client, Update update, CancellationToken token)
    {
        try
        {
            if (update.Type == UpdateType.Message && update.Message?.Text != null)
            {
                await HandleMessage(client, update.Message);
                return;
            }

            if (update.Type == UpdateType.CallbackQuery)
            {
                await HandleCallbackQuery(client, update.CallbackQuery);
                return;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
    }

    private static async Task HandleMessage(ITelegramBotClient client, Message message)
    {
        if (message.Text == "/start")
        {
            var commands = new List<BotCommand>
            {
                new BotCommand { Command = "start", Description = "Начать работу" }
            };
            // Устанавливаем команды меню
            await _botClient.SetMyCommands(
                commands: commands,
                scope: new BotCommandScopeDefault(),
                languageCode: "ru");


            var replyKeyboard = new InlineKeyboardMarkup(new[]
            {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Студент", "student_auth"),
                InlineKeyboardButton.WithCallbackData("Сотрудник", "employee_auth")
            }
        });

            await client.SendMessage(
                chatId: message.Chat.Id,
                text: "Добро пожаловать! Выберите вашу роль:",
                replyMarkup: replyKeyboard);
        }
    }

    private static async Task HandleCallbackQuery(ITelegramBotClient client, CallbackQuery callbackQuery)
    {
        switch (callbackQuery.Data)
        {
            case "student_auth":
                await client.SendMessage(
                    chatId: callbackQuery.Message.Chat.Id,
                    text: "Вы выбрали авторизацию как Студент. Введите ваши данные...");
                // Здесь можно добавить логику для авторизации студента
                break;

            case "employee_auth":
                await client.SendMessage(
                    chatId: callbackQuery.Message.Chat.Id,
                    text: "Вы выбрали авторизацию как Сотрудник. Введите ваши данные...");
                // Здесь можно добавить логику для авторизации сотрудника
                break;
        }

        // Подтверждаем обработку callback
        await client.AnswerCallbackQuery(callbackQuery.Id);
    }

    private static Task ErrorHandler(ITelegramBotClient botClient, Exception error, CancellationToken cancellationToken)
    {
        var ErrorMessage = error switch
        {
            ApiRequestException apiRequestException
                => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => error.ToString()
        };

        Console.WriteLine(ErrorMessage);
        return Task.CompletedTask;
    }
}





//var botClient = new TelegramBotClient("7681929292:AAELFhLTiH3c4KZtnRrPY9aGD6gYyLWVo5E");

//// Создаем список команд для меню
//var commands = new List<BotCommand>
//{
//    new BotCommand { Command = "start", Description = "Начать работу" },
//    new BotCommand { Command = "help", Description = "Помощь" },
//    new BotCommand { Command = "settings", Description = "Настройки" }
//};

//// Устанавливаем команды меню
//await botClient.SetMyCommands(
//    commands: commands,
//    scope: new BotCommandScopeDefault(),
//    languageCode: "ru");

//Console.WriteLine("Меню установлено");