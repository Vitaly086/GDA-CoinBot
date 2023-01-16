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
    private readonly TelegramBotClient _botClient;
    private readonly List<string> _currencyCodes = new List<string>();
    private CancellationTokenSource _cancellationTokenSource;

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
                Command = CustomBotCommands.PRICE,
                Description = "Вывод сообщения с выбором 1 из 4 валют, для получения ее цены в данный момент"
            }
        });
    }


    public void StartReceivingAsync()
    {
        AddCurrencyCodes();
        
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

    private void AddCurrencyCodes()
    {
        _currencyCodes.Add(CustomCallbackData.BTC);
        _currencyCodes.Add(CustomCallbackData.BNB);
        _currencyCodes.Add(CustomCallbackData.ETH);
        _currencyCodes.Add(CustomCallbackData.DOGE);
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update,
        CancellationToken cancellationToken)
    {
        switch (update.Type)
        {
            case UpdateType.Message:
                await HandleMessageAsync(botClient, update, cancellationToken);
                break;

            case UpdateType.CallbackQuery:
                await HandleCallbackQueryAsync(update, cancellationToken);
                return;
        }
    }

    private async Task HandleMessageAsync(ITelegramBotClient botClient, Update update,
        CancellationToken cancellationToken)
    {
        var chatId = update.Message.Chat.Id;

        await DeleteMessage(update, cancellationToken);
        if (update.Message.Text == null)
        {
            await _botClient.SendTextMessageAsync(chatId: chatId,
                text: "Бот принимает только команды из меню.",
                cancellationToken: cancellationToken);
            return;
        }

        var messageText = update.Message.Text;

        if (IsStartCommand(messageText))
        {
            await SendStartMessageAsync(botClient, cancellationToken, chatId);
            return;
        }

        if (IsPriceCommand(messageText))
        {
            await ShowCurrencySelectionAsync(chatId, cancellationToken);
        }
    }


    private async Task HandleCallbackQueryAsync(Update update, CancellationToken cancellationToken)
    {
        var chatId = update.CallbackQuery.Message.Chat.Id;
        var callbackData = update.CallbackQuery.Data;

        if (callbackData == CustomCallbackData.SELECT_CURRENCY)
        {
            await DeleteMessage(update, cancellationToken);
            await ShowCurrencySelectionAsync(chatId, cancellationToken);
            return;
        }

        if (callbackData == CustomCallbackData.CHANGE_CURRENCY)
        {
            await ShowCurrencySelectionAsync(chatId, cancellationToken);
            return;
        }

        if (_currencyCodes.Contains(callbackData))
        {
            await DeleteMessage(update, cancellationToken);
            await SendCurrencyPriceAsync(update, cancellationToken, chatId);
        }
    }

    private Task HandleError(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var jsonException = JsonConvert.SerializeObject(exception);
        Console.WriteLine(jsonException);
        return Task.CompletedTask;
    }

    private bool IsStartCommand(string? messageText)
    {
        return messageText.ToLower() == CustomBotCommands.START;
    }

    private bool IsPriceCommand(string? messageText)
    {
        return messageText.ToLower() == CustomBotCommands.PRICE;
    }

    private async Task ShowCurrencySelectionAsync(long? chatId, CancellationToken cancellationToken)
    {
        var inlineKeyboard = new InlineKeyboardMarkup(new[]
        {
            // Row 1
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Bitcoin", CustomCallbackData.BTC),
                InlineKeyboardButton.WithCallbackData("Ethereum", CustomCallbackData.ETH),
            },
            // Row 2
            new[]
            {
                InlineKeyboardButton.WithCallbackData("BNB", CustomCallbackData.BNB),
                InlineKeyboardButton.WithCallbackData("Dogecoin", CustomCallbackData.DOGE),
            },
        });

        await _botClient.SendTextMessageAsync(chatId: chatId,
            text: "Выберите валюту:",
            replyMarkup: inlineKeyboard,
            cancellationToken: cancellationToken);
    }

    private static async Task SendStartMessageAsync(ITelegramBotClient botClient, CancellationToken cancellationToken,
        long? chatId)
    {
        var inlineKeyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Выбрать валюту.", CustomCallbackData.SELECT_CURRENCY)
            }
        });

        await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: "Привет!\n" +
                  "Данный бот показывает текущий курс выбранной валюты.\n",
            replyMarkup: inlineKeyboard,
            cancellationToken: cancellationToken);
    }

    private async Task SendCurrencyPriceAsync(Update update, CancellationToken cancellationToken, long? chatId)
    {
        var currencyCode = update.CallbackQuery.Data;
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

    private async Task DeleteMessage(Update update, CancellationToken cancellationToken)
    {
        try
        {
            switch (update.Type)
            {
                case UpdateType.Message:
                    await _botClient.DeleteMessageAsync(chatId: update.Message.Chat.Id,
                        update.Message.MessageId,
                        cancellationToken: cancellationToken);
                    return;
                
                case UpdateType.CallbackQuery:
                    await _botClient.DeleteMessageAsync(chatId: update.CallbackQuery.Message.Chat.Id,
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