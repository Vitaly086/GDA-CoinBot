using GDA_CoinBot;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

/// <summary>
/// Класс отвечает за работу бота получения цены волюты.
/// Имеет объект класса TelegramBotClient для работы с TelegramApi
/// Константу интеравала обновления курса валюты
/// Словарь для хранения кода валюты выбранной для отслеживания пользователем
/// Словарь для хранения команд бота
/// Словарь для хранения токенов для отмены отслеживания валюты
/// Объект класса для обработки обратных вызовов
/// </summary>
public class CurrencyBot
{
    private readonly TelegramBotClient _botClient;
    private readonly List<string> _currencyCodes = new List<string>();

    public CurrencyBot(string apiKey)
    {
        _botClient = new TelegramBotClient(apiKey);
    }

    public void CreateCommands()
    {
        _botClient.SetMyCommandsAsync(new List<BotCommand>()
        {
            new()
            {
                Command = CustomBotCommands.START,
                Description = "Запуск бота."
            },
            new()
            {
                Command = CustomBotCommands.SHOW_CURRENCIES,
                Description = "Вывод сообщения с выбором 1 из 4 валют, для получения ее цены в данный момент"
            }
        });
    }

    /// <summary>
    /// Метод начинает отслеживание сообщений от пользователя
    /// </summary>
    public void StartReceiving()
    {
        AddCurrencyCodes();

        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        
        // Создаем список обрабатываемых типов сообщений
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new UpdateType[]
            {
                UpdateType.Message, UpdateType.CallbackQuery
            }
        };

        // Начинаем отслеживание сообщений от пользователя
        _botClient.StartReceiving(
            HandleUpdateAsync,
            HandleError,
            receiverOptions,
            cancellationToken);
    }

    /// <summary>
    /// Метод добавляет коды валют в массив
    /// </summary>
    private void AddCurrencyCodes()
    {
        _currencyCodes.Add(CustomCallbackData.BTC);
        _currencyCodes.Add(CustomCallbackData.BNB);
        _currencyCodes.Add(CustomCallbackData.ETH);
        _currencyCodes.Add(CustomCallbackData.DOGE);
    }

    /// <summary>
    /// Метод обработывает события проиходящие с ботом.
    /// Например: пользователь написал боту или нажал инлайн кнопку
    /// </summary>
    /// <param name="update"> Информация о произошедшем событии </param>
    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update,
        CancellationToken cancellationToken)
    {
        //  В зависимости от типа сообщения, запускаем нужный метод.
        switch (update.Type)
        {
            case UpdateType.Message:
                await HandleMessageAsync(update, cancellationToken);
                break;

            case UpdateType.CallbackQuery:
                await HandleCallbackQueryAsync(update, cancellationToken);
                break;
        }
    }

    /// <summary>
    /// Метод обрабатывает события типа сообщение.
    /// Которое включает в себя текст, картинки, видео, стикеры.
    /// </summary>
    /// <param name="update"> Информация о произошедшем событии </param>
    private async Task HandleMessageAsync(Update update,
        CancellationToken cancellationToken)
    {
        var chatId = update.Message.Chat.Id;

        await DeleteMessage(chatId, update.Message.MessageId, cancellationToken);
        
        // Проверяем, что пользователь присал текстовое сообщение
        if (update.Message.Text == null)
        {
            await _botClient.SendTextMessageAsync(chatId: chatId,
                text: "Бот принимает только команды из меню.",
                cancellationToken: cancellationToken);
            return;
        }

        var messageText = update.Message.Text;

        // Проверяем, прислал ли команду /start, если да вызываем метод обработки команды
        if (IsStartCommand(messageText))
        {
            await SendStartMessageAsync(chatId, cancellationToken);
            return;
        }

        // Проверяем, прислал ли команду /show, если да вызываем метод обработки команд
        if (IsShowCommand(messageText))
        {
            await ShowCurrencySelectionAsync(chatId, cancellationToken);
            return;
        }
        // Сообщение в случае, если пользователь написал не команду бота
        await _botClient.SendTextMessageAsync(chatId, "Введите команду.", cancellationToken: cancellationToken);
    }


    private async Task HandleCallbackQueryAsync(Update update, CancellationToken cancellationToken)
    {
        var chatId = update.CallbackQuery.Message.Chat.Id;
        var callbackData = update.CallbackQuery.Data;
        var messageId = update.CallbackQuery.Message.MessageId;

        if (callbackData == CustomCallbackData.SELECT_CURRENCY)
        {
            await DeleteMessage(chatId, messageId, cancellationToken);
            await ShowCurrencySelectionAsync(chatId, cancellationToken);
            return;
        }

        if (_currencyCodes.Contains(callbackData))
        {
            await DeleteMessage(chatId, messageId, cancellationToken);
            await SendCurrencyPriceAsync(chatId, callbackData, cancellationToken);
            return;
        }

        if (callbackData == CustomCallbackData.CHANGE_CURRENCY)
        {
            await ShowCurrencySelectionAsync(chatId, cancellationToken);
        }
    }

    private Task HandleError(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine(exception);
        return Task.CompletedTask;
    }

    private bool IsStartCommand(string messageText)
    {
        return messageText.ToLower() == CustomBotCommands.START;
    }

    private bool IsShowCommand(string messageText)
    {
        return messageText.ToLower() == CustomBotCommands.SHOW_CURRENCIES;
    }

    /// <summary>
    /// Метод отправляет сообщение с инлайн кнопкам для выбора валюты и вызывает коллбэк нажатия на кнопку
    /// Например пользователь нажал на кнопку BTC, вызывается коллбэк Select|BTC
    /// </summary>
    /// <param name="message"> Сообщение от пользователя </param>
    private async Task ShowCurrencySelectionAsync(long? chatId, CancellationToken cancellationToken)
    {
        // Создаем массив инлайн кнопок
        var inlineKeyboard = new InlineKeyboardMarkup(new[]
        {
            // Строка 1
            new[]
            {
                // Создаем кнопку с коллбэком кода валюты BTC
                InlineKeyboardButton.WithCallbackData("Bitcoin", CustomCallbackData.BTC),
                // Создаем кнопку с коллбэком кода валюты ETH
                InlineKeyboardButton.WithCallbackData("Ethereum", CustomCallbackData.ETH),
            },
            // Строка 2
            new[]
            {
                // Создаем кнопку с коллбэком кода валюты BNB
                InlineKeyboardButton.WithCallbackData("BNB", CustomCallbackData.BNB),
                // Создаем кнопку с коллбэком кода валюты DOGE
                InlineKeyboardButton.WithCallbackData("Dogecoin", CustomCallbackData.DOGE),
            },
        });

        // Отправляем сообщение с инлайн кнопками для выбора валюты
        await _botClient.SendTextMessageAsync(chatId: chatId,
            text: "Выберите валюту:",
            replyMarkup: inlineKeyboard,
            cancellationToken: cancellationToken);
    }

    private async Task SendStartMessageAsync(long? chatId, CancellationToken cancellationToken)
    {
        var inlineKeyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Выбрать валюту.", CustomCallbackData.SELECT_CURRENCY)
            }
        });

        await _botClient.SendTextMessageAsync(
            chatId: chatId,
            text: "Привет!\n" +
                  "Данный бот показывает текущий курс выбранной валюты.\n",
            replyMarkup: inlineKeyboard,
            cancellationToken: cancellationToken);
    }

    private async Task SendCurrencyPriceAsync(long? chatId, string currencyCode, CancellationToken cancellationToken)
    {
        var price = await CoinMarket.GetPriceAsync(currencyCode);

        var inlineKeyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Выбрать другую валюту.",
                    CustomCallbackData.CHANGE_CURRENCY)
            }
        });

        await _botClient.SendTextMessageAsync(chatId,
            text: $"Валюта: {currencyCode}, стоимость: {Math.Round(price, 3)}$",
            replyMarkup: inlineKeyboard,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Метод удаляет сообщение пользователя
    /// </summary>
    /// <param name="messageId">Индетификатор сообщения, которое надо удалить </param>
    private async Task DeleteMessage(long chatId, int messageId, CancellationToken cancellationToken)
    {
        // Используем try-catch в случае, если пользователь сам удалил сообщение, раньше бота.
        try
        {
            await _botClient.DeleteMessageAsync(chatId, messageId, cancellationToken);
        }
        catch (ApiRequestException exception)
        {
            // В случае ошибки с кодом 400 (Сообщение удалено), выводим сообщение в консоль.
            if (exception.ErrorCode == 400)
            {
                Console.WriteLine("Пользователь удалил сообщение");
            }
        }
    }
}