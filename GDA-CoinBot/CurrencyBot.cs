using GDA_CoinBot;
using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

public class CurrencyBot
{
    private const int INTERVAL = 5000;
    private readonly TelegramBotClient _telegramBotClient;
    private readonly Dictionary<long, string> _usersCurrentCurrency = new();
    private readonly Dictionary<string, ICommand> _commandsMap = new();

    private CancellationTokenSource _cancellationTokenSource;


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
        _usersCurrentCurrency.Add(chatId, currencyCode);
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
                var callbackHandler = new CallbackHandler(_telegramBotClient, this, _cancellationTokenSource);
                await callbackHandler.HandleCallbackQueryAsync(update, cancellationToken);
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
            await _telegramBotClient.DeleteMessageAsync(chatId, message.MessageId,
                cancellationToken: cancellationToken);
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

                StartTrackingPrice(message, trackValue, cancellationToken);

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
        catch (Exception exception)
        {
            await _telegramBotClient.SendTextMessageAsync(chatId, "Введите число.",
                cancellationToken: cancellationToken);
            Console.WriteLine(exception);
        }
    }

    private void StartTrackingPrice(Message message, decimal trackValue, CancellationToken cancellationToken)
    {
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

                    if (price >= trackValue)
                    {
                        await _telegramBotClient.SendTextMessageAsync(chatId, $"Цена {currencyCode} = {price}",
                            cancellationToken: cancellationToken);
                        _cancellationTokenSource.Cancel();
                        _usersCurrentCurrency.Remove(chatId);
                        Console.WriteLine("finish track");
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
}