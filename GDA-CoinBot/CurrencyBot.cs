using GDA_CoinBot;
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


    public void StartReceiving()
    {
        AddCurrencyCodes();

        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new UpdateType[]
            {
                UpdateType.Message, UpdateType.CallbackQuery
            }
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
                await HandleMessageAsync(update, cancellationToken);
                break;

            case UpdateType.CallbackQuery:
                await HandleCallbackQueryAsync(update, cancellationToken);
                break;
        }
    }

    private async Task HandleMessageAsync(Update update,
        CancellationToken cancellationToken)
    {
        var chatId = update.Message.Chat.Id;

        await DeleteMessage(chatId, update.Message.MessageId, cancellationToken);
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
            await SendStartMessageAsync(chatId, cancellationToken);
            return;
        }

        if (IsShowCommand(messageText))
        {
            await ShowCurrencySelectionAsync(chatId, cancellationToken);
        }
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

    private async Task DeleteMessage(long chatId, int messageId, CancellationToken cancellationToken)
    {
        try
        {
            await _botClient.DeleteMessageAsync(chatId: chatId,
                messageId, cancellationToken: cancellationToken);
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