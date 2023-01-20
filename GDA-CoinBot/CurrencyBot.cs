using GDA_CoinBot;
using Newtonsoft.Json;
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
    private const int INTERVAL = 20000;
    private readonly TelegramBotClient _telegramBotClient;
    private readonly Dictionary<long, string> _usersTrackCurrency = new();
    private readonly Dictionary<string, ICommand> _commandsMap = new();
    private readonly Dictionary<long, CancellationTokenSource> _usersTokenSources = new();
    private readonly CallbackHandler _callbackHandler;


    public CurrencyBot(string apiKey)
    {
        _telegramBotClient = new TelegramBotClient(apiKey);
        _callbackHandler = new CallbackHandler(_telegramBotClient, this);
    }

    /// <summary>
    /// Метод создает команды, которые бот будет обрабатывать
    /// </summary>
    public void CreateCommands()
    {
        // Создаем и добавляем в словарь объекты классов команд
        _commandsMap.Add(CustomBotCommands.START, new StartCommand(_telegramBotClient));
        _commandsMap.Add(CustomBotCommands.SHOW_CURRENCIES, new ShowCurrencyCommand(this));
        _commandsMap.Add(CustomBotCommands.TRACK, new TrackCommand(_telegramBotClient));

        // создаем список и описание меню команд бота 
        _telegramBotClient.SetMyCommandsAsync(new List<BotCommand>()
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
            },
            new()
            {
                Command = CustomBotCommands.TRACK,
                Description = "Отслеживание изменения цены выбранной валюты"
            }
        });
    }

    /// <summary>
    /// Метод начинает отслеживание сообщений от пользователя
    /// </summary>
    public void StartReceiving()
    {
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        // Создаем список обрабатываемых типов сообщений
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new UpdateType[] { UpdateType.Message, UpdateType.CallbackQuery }
        };

        // Начинаем отслеживание сообщений от пользователя
        _telegramBotClient.StartReceiving(
            HandleUpdateAsync,
            HandleError,
            receiverOptions,
            cancellationToken);
    }

    /// <summary>
    /// Метод отправляет сообщение с инлайн кнопкам для выбора валюты и вызывает коллбэк нажатия на кнопку
    /// Например пользователь нажал на кнопку BTC, вызывается коллбэк Select|BTC
    /// </summary>
    /// <param name="message"> Сообщение от пользователя </param>
    public async Task ShowCurrencySelectionAsync(Message message, CancellationToken cancellationToken)
    {
        var chatId = message.Chat.Id;

        // Создаем массив инлайн кнопок
        var inlineKeyboard = new InlineKeyboardMarkup(new[]
        {
            // Строка 1
            new[]
            {
                // Создаем кнопку с коллбэком действие SELECT и код валюты BTC с разделителем
                // Чтобы в дальнейшем отделить действие от кода валюты
                InlineKeyboardButton.WithCallbackData("Bitcoin",
                    $"{CustomCallbackData.SELECT_CURRENCY}|{CustomCallbackData.BTC}"),
                // Создаем кнопку с коллбэком SELECT и ETH
                InlineKeyboardButton.WithCallbackData("Ethereum",
                    $"{CustomCallbackData.SELECT_CURRENCY}|{CustomCallbackData.ETH}"),
            },
            // Строка 2
            new[]
            {
                // Создаем кнопку с коллбэком SELECT и BNB
                InlineKeyboardButton.WithCallbackData("BNB",
                    $"{CustomCallbackData.SELECT_CURRENCY}|{CustomCallbackData.BNB}"),
                // Создаем кнопку с коллбэком SELECT и DOGE
                InlineKeyboardButton.WithCallbackData("DogeCoin",
                    $"{CustomCallbackData.SELECT_CURRENCY}|{CustomCallbackData.DOGE}"),
            }
        });

        // Отправляем сообщение с инлайн кнопками для выбора валюты
        await _telegramBotClient.SendTextMessageAsync(chatId: chatId,
            text: "Выберите валюту:",
            replyMarkup: inlineKeyboard, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Метод для добавления выбранной валюты для отслеживания в словарь
    /// </summary>
    /// <param name="currencyCode"> Код валюты </param>
    public void AddCurrencyForTrack(long chatId, string currencyCode)
    {
        if (!_usersTrackCurrency.ContainsKey(chatId))
        {
            _usersTrackCurrency.Add(chatId, currencyCode);
        }

        _usersTrackCurrency[chatId] = currencyCode;
    }

    /// <summary>
    /// Метод возвращает tokenSource из словаря, для отмены отслеживания выбранной валюты.
    /// </summary>
    public CancellationTokenSource GetTokenSource(long chatId)
    {
        return _usersTokenSources[chatId];
    }

    /// <summary>
    /// Метод удаляет сообщение пользователя
    /// </summary>
    /// <param name="messageId">Индетификатор сообщения, которое надо удалить </param>
    public async Task DeleteMessage(long chatId, int messageId, CancellationToken cancellationToken)
    {
        // Используем try-catch в случае, если пользователь сам удалил сообщение, раньше бота.
        try
        {
            await _telegramBotClient.DeleteMessageAsync(chatId, messageId, cancellationToken);
        }
        catch (ApiRequestException exception)
        {
            // В случае ошибки с кодом 400 (Сообщение удалено), выводим сообщение в консоль.
            if (exception.ErrorCode == 400)
            {
                Console.WriteLine("User deleted message");
            }
        }
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
                await HandleCommandMessageAsync(update, cancellationToken);
                break;

            case UpdateType.CallbackQuery:
                await _callbackHandler.HandleCallbackQueryAsync(update, cancellationToken);
                break;
        }
    }

    /// <summary>
    /// Метод обрабатывает события типа сообщение.
    /// Которое включает в себя текст, картинки, видео, стикеры.
    /// </summary>
    /// <param name="update"> Информация о произошедшем событии </param>
    private async Task HandleCommandMessageAsync(Update update, CancellationToken cancellationToken)
    {
        var message = update.Message;
        var chatId = message.Chat.Id;
        var messageText = message.Text;

        // Проверяем, что пользователь присал текстовое сообщение
        if (messageText == null)
        {
            await DeleteMessage(chatId, message.MessageId, cancellationToken);
            await _telegramBotClient.SendTextMessageAsync(chatId: chatId,
                text: "Бот принимает только команды из меню.", cancellationToken: cancellationToken);
            return;
        }

        // Проверяем, прислал ли пользователь команду из меню, если да вызываем метод обработки команды
        if (_commandsMap.ContainsKey(messageText))
        {
            await _commandsMap[messageText].HandleCommandAsync(message, cancellationToken);
            return;
        }

        // Проверяем выбрал ли пользоваль код валюты для отслеживания
        if (_usersTrackCurrency.ContainsKey(chatId))
        {
            await StartTrackPrice(chatId, messageText, cancellationToken);
            return;
        }

        // Сообщение в случае, если пользователь написал не команду бота
        await _telegramBotClient.SendTextMessageAsync(chatId, "Введите команду.", cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Метод начинает отслевижать изменение цены валюты 
    /// </summary>
    /// <param name="messageText"> Цена валюты, которую пользователь хочет получить </param>
    private async Task StartTrackPrice(long chatId, string messageText, CancellationToken cancellationToken)
    {
        // Используем блок try-catch для того, чтобы конвертировать желаемый курс валюты, введеный пользоваетелем.
        try
        {
            var trackValue = Convert.ToDecimal(messageText);
            var cancellationTokenSource = new CancellationTokenSource();

            // Добавляемый новй токен отмены, чтобы остановить отслеживание
            AddUsersTokenSources(chatId, cancellationTokenSource);
            // Отслеживаем изменение цены
            TrackPrice(chatId, trackValue);

            // Создаем инлайн кнопку с коллбэком отмены отслеживания
            var inlineKeyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Остановить отслеживание",
                        $"{CustomCallbackData.CANCEL_TRACK}"),
                }
            });

            // Направляем сообщение с инлайн кнопкой
            await _telegramBotClient.SendTextMessageAsync(chatId,
                $"Начато отслеживание {_usersTrackCurrency[chatId]} с порогом {trackValue}",
                replyMarkup: inlineKeyboard,
                cancellationToken: cancellationToken);
        }

        // Ловим ошибку конвертации формата, на случай если пользователь введет не число, а текст.
        catch (FormatException exception)
        {
            await _telegramBotClient.SendTextMessageAsync(chatId, "Введите число.",
                cancellationToken: cancellationToken);
            Console.WriteLine(exception);
        }
    }

    /// <summary>
    /// Метод добавляет в словарь, созданный tokenSource для остановки отслеживания цены валюты 
    /// </summary>
    private void AddUsersTokenSources(long chatId, CancellationTokenSource cancellationTokenSource)
    {
        if (!_usersTokenSources.ContainsKey(chatId))
        {
            _usersTokenSources.Add(chatId, cancellationTokenSource);
            return;
        }

        _usersTokenSources[chatId] = cancellationTokenSource;
    }

    /// <summary>
    /// Метод отслеживает изменение цены валюты с интервалом по таймеру.
    /// Например, каждые 20 секунд, происходит запрос цены выбранной валюты.
    /// </summary>
    /// <param name="trackValue"> Желаемая цена валюты </param>
    private void TrackPrice(long chatId, decimal trackValue)
    {
        // Проверяем желаемая валюта больше или меньше отпрваленной пользователю ранее
        var isDesiredPriceHigher = trackValue > _callbackHandler.GetPreviousPrice(chatId);
        // создаем токен отменты для текущей валюты
        var cancellationToken = _usersTokenSources[chatId].Token;

        // создаем объект класса Timer, который будет запускать проверку курса с заданным интервалом
        var timer = new Timer(_ =>
        {
            // Запускаем задачу, которая проверяет цену валюты
            Task.Run(async () =>
            {
                var currencyCode = _usersTrackCurrency[chatId];

                // Если токен не отменен, продолжаем
                if (!cancellationToken.IsCancellationRequested)
                {
                    var price = await CoinMarket.GetPriceAsync(currencyCode);

                    // Если цена достигла желаемой выводим сообщение и останавливаем таймер
                    if (isDesiredPriceHigher && price >= trackValue)
                    {
                        await StopTrack(chatId, currencyCode, price, cancellationToken);
                    }

                    // Если цена достигла желаемой выводим сообщение и останавливаем таймер
                    if (!isDesiredPriceHigher && price <= trackValue)
                    {
                        await StopTrack(chatId, currencyCode, price, cancellationToken);
                    }
                }
            }, cancellationToken);
        }, null, 0, INTERVAL);
    }

    /// <summary>
    /// Метод останавливет остлеживание изменения цены валюты, при достижении нужной цены
    /// </summary>
    /// <param name="currencyCode"> Код валюты </param>
    /// <param name="price"> Цена валюты </param>
    private async Task StopTrack(long chatId, string currencyCode, decimal price, CancellationToken cancellationToken)
    {
        await _telegramBotClient.SendTextMessageAsync(chatId,
            $"Цена {currencyCode} = {price}.\n Отслеживание остановлено.",
            cancellationToken: cancellationToken);
        _usersTokenSources[chatId].Cancel();
    }

    /// <summary>
    /// Метод обрабатывает ошибки бота во время отслеживания сообщеий от пользователя 
    /// </summary>
    /// <param name="exception"> Тип ошибки </param>
    private Task HandleError(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        // Конвертируем ошибки в формат JSON и выводим в консоль
        var jsonException = JsonConvert.SerializeObject(exception);
        Console.WriteLine(jsonException);
        return Task.CompletedTask;
    }
}