using GDA_CoinBot;
using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

public class CurrencyBot
{
    // константа с интвервалом обновления курса
    private const int INTERVAL = 20000;

    private readonly TelegramBotClient _telegramBotClient;

    // словарь для выбранной текущей валюты пользователя
    private readonly Dictionary<long, string> _usersCurrentCurrency = new();

    // словарь для хранения команд бота
    private readonly Dictionary<string, ICommand> _commandsMap = new();

    // словарь для хранения токенов для отмены отслеживания валюты
    private readonly Dictionary<long, CancellationTokenSource> _usersTokenSources = new();

    // класс для обработки обратных вызовов
    private readonly CallbackHandler _callbackHandler;


    public CurrencyBot(string apiKey)
    {
        _telegramBotClient = new TelegramBotClient(apiKey);
        _callbackHandler = new CallbackHandler(_telegramBotClient, this);
    }

    /// <summary>
    /// Метод создает команды бота
    /// </summary>
    public void CreateCommands()
    {
        // Создаем и добавляем в словарь объекты классов команд
        _commandsMap.Add(CustomBotCommands.START, new StartCommand(_telegramBotClient));
        _commandsMap.Add(CustomBotCommands.SHOW_CURRENCIES, new ShowCurrencyCommand(this));
        _commandsMap.Add(CustomBotCommands.TRACK, new TrackCommand(_telegramBotClient));

        // создаем список и описание команд бота
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
    public void StartReceivingAsync()
    {
        // Создаем новый токен отменты
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
    /// Метод показывает инлайн кнопки выбора валюты
    /// </summary>
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

        // Отправляем сообщение с преложением выбрать валюты и инлайн кнопки для выбора
        await _telegramBotClient.SendTextMessageAsync(chatId: chatId,
            text: "Выберите валюту:",
            replyMarkup: inlineKeyboard, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Метод добавляет выбранную валюту в словарь.
    /// </summary>
    public void AddSelectedUserCurrency(long chatId, string currencyCode)
    {
        if (!_usersCurrentCurrency.ContainsKey(chatId))
        {
            _usersCurrentCurrency.Add(chatId, currencyCode);
        }

        //Если ключ уже есть в словаре, выбранная валюта заменяется.
        _usersCurrentCurrency[chatId] = currencyCode;
    }

    /// <summary>
    /// Метод возвращает tokenSource для отмены отслеживаемой валюты.
    /// </summary>
    public CancellationTokenSource GetTokenSource(long chatId)
    {
        return _usersTokenSources[chatId];
    }

    /// <summary>
    /// Метод удаляет сообщение пользователя
    /// </summary>
    public async Task DeleteMessage(long chatId, int messageId, CancellationToken cancellationToken)
    {
        // Используем try-catch в случае, если пользователь сам удалил сообщение, быстрее бота.
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
    ///  Метод обрабатывает обновления от пользователя.
    /// </summary>
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
    ///  Метод обрабатывает обновления типа Meessage
    /// </summary>
    private async Task HandleCommandMessageAsync(Update update, CancellationToken cancellationToken)
    {
        var message = update.Message;
        var chatId = message.Chat.Id;
        var text = message.Text;

        // Проверяем, что прислал пользователь, если не текст удлаем сообщение.
        if (text == null)
        {
            await DeleteMessage(chatId, message.MessageId, cancellationToken);
            await _telegramBotClient.SendTextMessageAsync(chatId: chatId,
                text: "Бот принимает только команды из меню.", cancellationToken: cancellationToken);
            return;
        }

        // Проверяем, прислал ли пользователь команду, если да вызываем метод обработки команды
        if (_commandsMap.ContainsKey(text))
        {
            await _commandsMap[text].HandleCommandAsync(message, cancellationToken);
            return;
        }

        // Проверяем выбрал ли пользоваль код валюты
        if (_usersCurrentCurrency.ContainsKey(chatId))
        {
            // Начинаем отслеживать цену валюты
            await StartTrackPrice(chatId, text, cancellationToken);
            return;
        }

        // Сообщение в случае необработнки никакой команды
        await _telegramBotClient.SendTextMessageAsync(chatId, "Введите команду", cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Метод начинает отслевижать цену валюты
    /// </summary>
    private async Task StartTrackPrice(long chatId, string text, CancellationToken cancellationToken)
    {
        // Используем блок try-catch для того, чтобы конвертировать желаемый курс валюты, введеный пользоваетелем.
        try
        {
            var trackValue = Convert.ToDecimal(text);
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
                $"Начато отслеживание {_usersCurrentCurrency[chatId]} с порогом {trackValue}",
                replyMarkup: inlineKeyboard,
                cancellationToken: cancellationToken);
        }

        // Ловим ошибку на случай если пользователь введет не число, а текст.
        catch (FormatException exception)
        {
            await _telegramBotClient.SendTextMessageAsync(chatId, "Введите число.",
                cancellationToken: cancellationToken);
            Console.WriteLine(exception);
        }
    }

    /// <summary>
    /// Метод добавляет tokenSource в словарь
    /// </summary>
    private void AddUsersTokenSources(long chatId, CancellationTokenSource cancellationTokenSource)
    {
        // Проверяем содержит ли словарь ключ - chatdId, если не добавляем
        if (!_usersTokenSources.ContainsKey(chatId))
        {
            _usersTokenSources.Add(chatId, cancellationTokenSource);
            return;
        }
        
        _usersTokenSources[chatId] = cancellationTokenSource;
    }
    
    /// <summary>
    /// Метод отслеживает изменение цены валюты
    /// </summary>
    private void TrackPrice(long chatId, decimal trackValue)
    {
        // Проверяем желаемая валюта больше или меньше показанной пользователю
        var isDesiredPriceHigher = trackValue > _callbackHandler.GetPreviousPrice(chatId);
        // создаем токен отменты 
        var cancellationToken = _usersTokenSources[chatId].Token;

        // создаем объект класса Timer, который будет запускать проверку курса с заданным интервалом
        var timer = new Timer(_ =>
        {
            // Запускаем задачу, которая проверяет цену валюты
            Task.Run(async () =>
            {
                var currencyCode = _usersCurrentCurrency[chatId];
                
                // Если токен не отменен, продолжаем
                if (!cancellationToken.IsCancellationRequested)
                {
                    // Получаем цену валюты
                    var price = await CoinMarket.GetPriceAsync(currencyCode);
                    // Для проверки работоспособности таймера, отправим в косоль цену валюты
                    Console.WriteLine(price);

                    // Если цена достигла желаемой выводим сообщение и останавливаем таймер
                    if (isDesiredPriceHigher && price >= trackValue)
                    {
                        await _telegramBotClient.SendTextMessageAsync(chatId,
                            $"Цена {currencyCode} = {price}.\n Отслеживание остановлено.",
                            cancellationToken: cancellationToken);
                        _usersTokenSources[chatId].Cancel();
                    }

                    // Если цена достигла желаемой выводим сообщение и останавливаем таймер
                    if (!isDesiredPriceHigher && price <= trackValue)
                    {
                        await _telegramBotClient.SendTextMessageAsync(chatId,
                            $"Цена {currencyCode} = {price}.\n Отслеживание остановлено.",
                            cancellationToken: cancellationToken);
                        _usersTokenSources[chatId].Cancel();
                    }
                }
            }, cancellationToken);
        }, null, 0, INTERVAL);
    }


    /// <summary>
    /// Метод обработки ошибок получения обновлений
    /// </summary>
    private Task HandleError(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        // Конвертируем ошибки в формат JSON и выводим в консоль
        var jsonException = JsonConvert.SerializeObject(exception);
        Console.WriteLine(jsonException);
        return Task.CompletedTask;
    }
}