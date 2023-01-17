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
    private const int INTERVAL = 20000;
    private readonly TelegramBotClient _telegramBotClient;
    private readonly Dictionary<long, string> _usersCurrentCurrency = new();
    private readonly Dictionary<string, ICommand> _commandsMap = new();
    private readonly Dictionary<long, CancellationTokenSource> _usersTokenSources = new();

    private CancellationTokenSource _cancellationTokenSource;
    private CallbackHandler _callbackHandler;


    public CurrencyBot(string apiKey)
    {
        _telegramBotClient = new TelegramBotClient(apiKey);
    }

    public void CreateCommands()
    {
        _commandsMap.Add(CustomBotCommands.START, new StartCommand(this));
        _commandsMap.Add(CustomBotCommands.SHOW_CURRENCY, new ShowCurrencyCommand(_telegramBotClient));
        _commandsMap.Add(CustomBotCommands.TRACK, new TrackCommand(_telegramBotClient));

        _telegramBotClient.SetMyCommandsAsync(new List<BotCommand>()
        {
            new()
            {
                Command = CustomBotCommands.START,
                Description = "Запуск бота."
            },
            new()
            {
                Command = CustomBotCommands.SHOW_CURRENCY,
                Description = "Вывод сообщения с выбором 1 из 4 валют, для получения ее цены в данный момент"
            },
            new()
            {
                Command = CustomBotCommands.TRACK,
                Description = "Отслеживание изменения цены выбранной валюты"
            }
        });
    }

    public void StartReceivingAsync()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _cancellationTokenSource.Token;
        _callbackHandler = new CallbackHandler(_telegramBotClient, this);

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new UpdateType[] { UpdateType.Message, UpdateType.CallbackQuery }
        };

        _telegramBotClient.StartReceiving(
            HandleUpdateAsync,
            HandleError,
            receiverOptions,
            cancellationToken);
    }

    public async Task ShowCurrency(Message message, CancellationToken cancellationToken)
    {
        var chatId = message.Chat.Id;

        var inlineKeyboard = new InlineKeyboardMarkup(new[]
        {
            // Row 1
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Bitcoin",
                    $"{CustomCallbackData.SELECT}|{CustomCallbackData.BTC}"),
                InlineKeyboardButton.WithCallbackData("Ethereum",
                    $"{CustomCallbackData.SELECT}|{CustomCallbackData.ETH}"),
            },
            // Row 2
            new[]
            {
                InlineKeyboardButton.WithCallbackData("BNB",
                    $"{CustomCallbackData.SELECT}|{CustomCallbackData.BNB}"),
                InlineKeyboardButton.WithCallbackData("DogeCoin",
                    $"{CustomCallbackData.SELECT}|{CustomCallbackData.DOGE}"),
            }
        });

        await _telegramBotClient.SendTextMessageAsync(chatId: chatId,
            text: "Выберите валюту:",
            replyMarkup: inlineKeyboard, cancellationToken: cancellationToken);
    }

    public void AddSelectedUserCurrency(long chatId, string currencyCode)
    {
        if (!_usersCurrentCurrency.ContainsKey(chatId))
        {
            _usersCurrentCurrency.Add(chatId, currencyCode);
        }

        _usersCurrentCurrency[chatId] = currencyCode;
    }

    public CancellationTokenSource GetTokenSource(long chatId)
    {
        return _usersTokenSources[chatId];
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update,
        CancellationToken cancellationToken)
    {
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

    private async Task HandleCommandMessageAsync(Update update, CancellationToken cancellationToken)
    {
        var message = update.Message;
        var chatId = message.Chat.Id;
        var text = message.Text;

        if (text == null)
        {
            await DeleteMessage(update, cancellationToken);
            await _telegramBotClient.SendTextMessageAsync(chatId: chatId,
                text: "Бот принимает только команды из меню.", cancellationToken: cancellationToken);
            return;
        }

        if (_commandsMap.ContainsKey(text))
        {
            await _commandsMap[text].HandleCommandAsync(message, cancellationToken);
            return;
        }

        try
        {
            if (_usersCurrentCurrency.ContainsKey(chatId))
            {
                var trackValue = Convert.ToDecimal(text);

                var cancellationTokenSource = new CancellationTokenSource();

                AddUsersTokenSources(chatId, cancellationTokenSource);
                StartTrackingPrice(message, trackValue);

                var inlineKeyboard = new InlineKeyboardMarkup(new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("Остановить отслеживание",
                            $"{CustomCallbackData.CANCEL_TRACK}"),
                    }
                });

                await _telegramBotClient.SendTextMessageAsync(chatId,
                    $"Начато отслеживание {_usersCurrentCurrency[chatId]} с порогом {trackValue}",
                    replyMarkup: inlineKeyboard,
                    cancellationToken: cancellationToken);
            }
        }
        catch (FormatException exception)
        {
            await _telegramBotClient.SendTextMessageAsync(chatId, "Введите число.",
                cancellationToken: cancellationToken);
            Console.WriteLine(exception);
        }
    }

    private void AddUsersTokenSources(long chatId, CancellationTokenSource cancellationTokenSource)
    {
        if (!_usersTokenSources.ContainsKey(chatId))
        {
            _usersTokenSources.Add(chatId, cancellationTokenSource);
        }
        else
        {
            _usersTokenSources[chatId] = cancellationTokenSource;
        }
    }

    private void StartTrackingPrice(Message message, decimal trackValue)
    {
        var isDesiredPriceHigher = trackValue > _callbackHandler.GetPreviousPrice(message.Chat.Id);
        var cancellationToken = _usersTokenSources[message.Chat.Id].Token;

        var timer = new Timer(_ =>
        {
            Task.Run(async () =>
            {
                var chatId = message.Chat.Id;
                var currencyCode = _usersCurrentCurrency[chatId];

                if (!cancellationToken.IsCancellationRequested)
                {
                    var price = await CoinMarket.GetPriceAsync(currencyCode);
                    Console.WriteLine($"Цена {currencyCode} = {price}");

                    if (isDesiredPriceHigher && price >= trackValue)
                    {
                        await _telegramBotClient.SendTextMessageAsync(chatId,
                            $"Цена {currencyCode} = {price}.\n Отслеживание остановлено.",
                            cancellationToken: cancellationToken);
                        _usersTokenSources[chatId].Cancel();
                    }

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


    private Task HandleError(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var jsonException = JsonConvert.SerializeObject(exception);
        Console.WriteLine(jsonException);
        return Task.CompletedTask;
    }

    public async Task DeleteMessage(Update update, CancellationToken cancellationToken)
    {
        try
        {
            switch (update.Type)
            {
                case UpdateType.Message:
                    await _telegramBotClient.DeleteMessageAsync(chatId: update.Message.Chat.Id,
                        update.Message.MessageId,
                        cancellationToken: cancellationToken);
                    return;

                case UpdateType.CallbackQuery:
                    await _telegramBotClient.DeleteMessageAsync(chatId: update.CallbackQuery.Message.Chat.Id,
                        update.CallbackQuery.Message.MessageId,
                        cancellationToken: cancellationToken);
                    return;
            }
        }
        catch (ApiRequestException exception)
        {
            if (exception.ErrorCode == 400)
            {
                Console.WriteLine("User deleted message");
            }
        }
    }
}