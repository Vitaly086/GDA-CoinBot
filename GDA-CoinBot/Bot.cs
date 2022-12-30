using System.Diagnostics.CodeAnalysis;
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
    private CancellationTokenSource _cancellationTokenSource;

    public Bot(string apiKey)
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
        long? chatId;
        InlineKeyboardMarkup? inlineKeyboard;
        switch (update.Type)
        {
            case UpdateType.Message:
                chatId = update.Message?.Chat.Id;

                await _botClient.DeleteMessageAsync(chatId: chatId,
                    update.Message.MessageId,
                    cancellationToken: cancellationToken);

                if (update.Message?.Text == null)
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

                break;

            case UpdateType.CallbackQuery:
                chatId = update.CallbackQuery.Message.Chat.Id;
                var updateData = update.CallbackQuery.Data;

                switch (updateData)
                {
                    case CustomCallbackData.CHANGE_CURRENCY:
                    {
                        await _botClient.DeleteMessageAsync(chatId: chatId,
                            messageId: update.CallbackQuery.Message.MessageId,
                            cancellationToken: cancellationToken);

                        await ShowCurrencySelectionAsync(chatId, cancellationToken);
                        break;
                    }

                    case CustomCallbackData.RESULT:
                    {
                        await ShowCurrencySelectionAsync(chatId, cancellationToken);
                        break;
                    }

                    default:
                    {
                        await SendCurrencyPriceAsync(update, cancellationToken, chatId);
                        break;
                    }
                }
                break;
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
            text: "Выберите валютую",
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
                InlineKeyboardButton.WithCallbackData("Выбрать валюту.", CustomCallbackData.CHANGE_CURRENCY)
            }
        });

        await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: "Привет!\n" +
                  "Данный бот показывает текущий курс выбранной валюты.\n",
            replyMarkup: inlineKeyboard,
            cancellationToken: cancellationToken);
    }

    private async Task SendCurrencyPriceAsync(Update update, CancellationToken cancellationToken,
        [DisallowNull] long? chatId)
    {
        InlineKeyboardMarkup inlineKeyboard;
        await _botClient.DeleteMessageAsync(chatId: chatId,
            messageId: update.CallbackQuery.Message.MessageId,
            cancellationToken: cancellationToken);

        var currencyCode = update.CallbackQuery.Data;
        var price = await CoinMarket.GetPriceAsync(currencyCode);

        inlineKeyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Выбрать другую валюту.",
                    CustomCallbackData.RESULT)
            }
        });

        await _botClient.SendTextMessageAsync(chatId,
            text: $"Валюта: {currencyCode}, стоимость: {Math.Round(price, 3)}$",
            replyMarkup: inlineKeyboard,
            cancellationToken: cancellationToken);
    }
}