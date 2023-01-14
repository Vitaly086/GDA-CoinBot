using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace GDA_CoinBot;

public class CallbackHandler
{
    public Dictionary<long, decimal> PreviousCoinPrice { get; } = new Dictionary<long, decimal>();
    private readonly ITelegramBotClient _telegramBotClient;
    private readonly CurrencyBot _currencyBot;
    private readonly CancellationTokenSource _cancellationTokenSource;

    public CallbackHandler(ITelegramBotClient telegramBotClient, CurrencyBot currencyBot, CancellationTokenSource cancellationTokenSource)
    {
        _telegramBotClient = telegramBotClient;
        _currencyBot = currencyBot;
        _cancellationTokenSource = cancellationTokenSource;
    }

    public async Task HandleCallbackQueryAsync(Update update, CancellationToken cancellationToken)
    {
        var message = update.CallbackQuery.Message;
        var action = SplitCallbackData(update, out var currencyCode);

        switch (action)
        {
            case CustomCallbackData.START_CHOICE:
                await HandleStartChoice(message, cancellationToken);
                break;

            case CustomCallbackData.CHANGE_CURRENCY:
                await HandleChangeCurrency(message, cancellationToken);
                break;

            case CustomCallbackData.SELECT:
                await HandleSelect(message, currencyCode, cancellationToken);
                break;

            case CustomCallbackData.TRACK:
                await HandleTrack(message, cancellationToken, currencyCode);
                break;
            
            case CustomCallbackData.CANCEL_TRACK:
                _cancellationTokenSource.Cancel();
                Console.WriteLine("stop with callback");
                break;
        }
    }

    private async Task HandleStartChoice(Message message, CancellationToken cancellationToken)
    {
        await _telegramBotClient.DeleteMessageAsync(chatId: message.Chat.Id, messageId: message.MessageId,
            cancellationToken: cancellationToken);
        await _currencyBot.ShowCurrency(message, cancellationToken);
    }

    private async Task HandleChangeCurrency(Message message, CancellationToken cancellationToken)
    {
        await _currencyBot.ShowCurrency(message, cancellationToken);
    }

    private async Task HandleSelect(Message message, string currencyCode,
        CancellationToken cancellationToken)
    {
        var chatId = message.Chat.Id;
        await _telegramBotClient.DeleteMessageAsync(chatId: chatId, messageId: message.MessageId,
            cancellationToken: cancellationToken);
        var price = await CoinMarket.GetPriceAsync(currencyCode);
        var inlineKeyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Выбрать другую валюту.", CustomCallbackData.CHANGE_CURRENCY)
            }
        });
        await _telegramBotClient.SendTextMessageAsync(chatId, text: $"Валюта: {currencyCode}, стоимость: {price}$",
            replyMarkup: inlineKeyboard, cancellationToken: cancellationToken);
    }

    private async Task HandleTrack( Message message, CancellationToken cancellationToken,
        string currencyCode)
    {
        var chatId = message.Chat.Id;
        var price = await CoinMarket.GetPriceAsync(currencyCode);
        PreviousCoinPrice.Add(chatId, price);

        await _telegramBotClient.SendTextMessageAsync(chatId, text: $"Валюта: {currencyCode}, стоимость: {price}$",
            cancellationToken: cancellationToken);
        _currencyBot.AddSelectedUserCurrency(chatId, currencyCode);
        await _telegramBotClient.SendTextMessageAsync(chatId, text: "Введите желаемый курс",
            cancellationToken: cancellationToken);
    }

    private static string SplitCallbackData(Update update, out string currencyCode)
    {
        var words = update.CallbackQuery.Data.Split('|');

        var action = words[0];
        currencyCode = string.Empty;
        if (words.Length > 1)
        {
            currencyCode = words[1];
        }

        return action;
    }
}