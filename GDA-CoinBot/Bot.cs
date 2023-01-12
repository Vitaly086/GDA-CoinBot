using GDA_CoinBot;
using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

public class Bot
{
    private readonly TelegramBotClient _botClient;
    private readonly int _interval = 10000;
    private readonly Dictionary<long, string> _usersCurrentCurrency = new();
    private readonly Dictionary<string, Command> _commandsMap = new();


    private CancellationTokenSource _cancellationTokenSource;
    private Timer _timer;


    public Bot(string apiKey)
    {
        _botClient = new TelegramBotClient(apiKey);
    }

    public void CreateCommands()
    {
        _commandsMap.Add(CustomBotCommands.START, new StartCommand(_botClient));
        _commandsMap.Add(CustomBotCommands.SHOW_CURRENCY, new ShowCurrencyCommand(_botClient));
        _commandsMap.Add(CustomBotCommands.TRACK, new TrackCommand(_botClient));

        _botClient.SetMyCommandsAsync(new List<BotCommand>()
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

        _botClient.StartReceiving(
            HandleUpdateAsync,
            HandleError,
            receiverOptions,
            cancellationToken);
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
                await HandleCallbackQueryAsync(update, cancellationToken);
                break;
        }
    }

    private async Task HandleCommandMessageAsync(Update update, CancellationToken cancellationToken)
    {
        var message = update.Message;
        var chatId = message.Chat.Id;
        var text = message.Text;

        switch (text)
        {
            case null:
                await _botClient.DeleteMessageAsync(chatId, message.MessageId, cancellationToken: cancellationToken);
                await _botClient.SendTextMessageAsync(chatId: chatId,
                    text: "Бот принимает только команды из меню.", cancellationToken: cancellationToken);
                return;

            case CustomBotCommands.START:
                await _commandsMap[CustomBotCommands.START].ExecuteAsync(message, cancellationToken);
                break;
            case CustomBotCommands.SHOW_CURRENCY:
                await _commandsMap[CustomBotCommands.SHOW_CURRENCY].ExecuteAsync(message, cancellationToken);
                break;
            case CustomBotCommands.TRACK:
                await _commandsMap[CustomBotCommands.TRACK].ExecuteAsync(message, cancellationToken);
                break;
        }

        try
        {
            if (_usersCurrentCurrency.ContainsKey(chatId))
            {
                var trackValue = Convert.ToDecimal(text);
                
                await StartTrackingPrice(message, trackValue, cancellationToken);

                var inlineKeyboard = new InlineKeyboardMarkup(new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("Остановить отслеживание",
                            $"{CustomCallbackData.CANCEL_TRACK}"),
                    }
                });

                await _botClient.SendTextMessageAsync(chatId, "",
                    replyMarkup: inlineKeyboard,
                    cancellationToken: cancellationToken);
            }
        }
        catch (Exception exception)
        {
            await _botClient.SendTextMessageAsync(chatId, "Введите число.", cancellationToken: cancellationToken);
            Console.WriteLine(exception);
        }
    }

    private async Task HandleCallbackQueryAsync(Update update, CancellationToken cancellationToken)
    {
        var chatId = update.CallbackQuery.Message.Chat.Id;
        var message = update.CallbackQuery.Message;
        var words = update.CallbackQuery.Data.Split('|');

        var action = words[0]; //todo change it
        var currencyCode = string.Empty;
        if (words.Length > 1)
        {
            currencyCode = words[1];
        }

        decimal price;
        await Task.Run(async () =>
        {
            switch (action)
            {
                case CustomCallbackData.START_CHOICE:
                    await _botClient.DeleteMessageAsync(chatId: chatId,
                        messageId: update.CallbackQuery.Message.MessageId);
                    await _commandsMap[CustomBotCommands.SHOW_CURRENCY]
                        .ExecuteAsync(message, cancellationToken); //можно ли так вызывать метод?
                    break;

                case CustomCallbackData.CHANGE_CURRENCY:
                    await _commandsMap[CustomBotCommands.SHOW_CURRENCY]
                        .ExecuteAsync(message, cancellationToken); //можно ли так вызывать метод?
                    break;

                case CustomCallbackData.SELECT:
                    await _botClient.DeleteMessageAsync(chatId: chatId,
                        messageId: update.CallbackQuery.Message.MessageId);

                    price = await CoinMarket.GetPriceAsync(currencyCode);
                    var inlineKeyboard = new InlineKeyboardMarkup(new[]
                    {
                        new[]
                        {
                            InlineKeyboardButton.WithCallbackData("Выбрать другую валюту.",
                                CustomCallbackData.CHANGE_CURRENCY)
                        }
                    });

                    await _botClient.SendTextMessageAsync(chatId,
                        text: $"Валюта: {currencyCode}, стоимость: {price}$",
                        replyMarkup: inlineKeyboard);
                    break;

                case CustomCallbackData.TRACK:
                    price = await CoinMarket.GetPriceAsync(currencyCode);
                    await _botClient.SendTextMessageAsync(chatId, text: $"Валюта: {currencyCode}, стоимость: {price}$");
                    _usersCurrentCurrency.Add(chatId, currencyCode);
                    await _botClient.SendTextMessageAsync(chatId, text: "Введите желаемый курс");
                    break;
                case CustomCallbackData.CANCEL_TRACK:
                    _cancellationTokenSource.Cancel();
                    break;
            }
        }, cancellationToken);
    }

    private async Task StartTrackingPrice(Message message, decimal trackValue, CancellationToken cancellationToken)
    {
        await Task.Run(async () =>
        {
            var chatId = message.Chat.Id;
            var currencyCode = _usersCurrentCurrency[chatId];

            while (!cancellationToken.IsCancellationRequested)
            {
                var price = await CoinMarket.GetPriceAsync(currencyCode);
                Console.WriteLine($"Цена {currencyCode} = {price}");

                if (price >= trackValue)
                {
                    await _botClient.SendTextMessageAsync(chatId, $"Цена {currencyCode} = {price}",
                        cancellationToken: cancellationToken);
                    _cancellationTokenSource.Cancel();
                }

                await Task.Delay(_interval, cancellationToken);
            }
        }, cancellationToken);
    }
    
    


    private async Task CheckCurrencyValue(Message message, decimal trackValue, CancellationToken cancellationToken)
    {
        var chatId = message.Chat.Id;
        var currencyCode = _usersCurrentCurrency[chatId];

        var price = await CoinMarket.GetPriceAsync(currencyCode);
        Console.WriteLine($"Цена {currencyCode} = {price}");

        if (price >= trackValue)
        {
            await _botClient.SendTextMessageAsync(chatId, $"Цена {currencyCode} = {price}",
                cancellationToken: cancellationToken);
        }
    }


    private Task HandleError(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var jsonException = JsonConvert.SerializeObject(exception);
        Console.WriteLine(jsonException);
        return Task.CompletedTask;
    }
}