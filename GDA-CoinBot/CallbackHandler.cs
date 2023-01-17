using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace GDA_CoinBot;

public class CallbackHandler
{
    private readonly Dictionary<long, decimal> _previousCoinPrice = new Dictionary<long, decimal>();
    private readonly ITelegramBotClient _telegramBotClient;
    private readonly CurrencyBot _currencyBot;

    public CallbackHandler(ITelegramBotClient telegramBotClient, CurrencyBot currencyBot)
    {
        _telegramBotClient = telegramBotClient;
        _currencyBot = currencyBot;
    }

    public async Task HandleCallbackQueryAsync(Update update, CancellationToken cancellationToken)
    {
        var action = SplitCallbackData(update, out var currencyCode);

        switch (action)
        {
            case CustomCallbackData.START_CHOICE:
                await HandleStartChoice(update, cancellationToken);
                break;

            case CustomCallbackData.CHANGE_CURRENCY:
                await HandleChangeCurrency(update, cancellationToken);
                break;

            case CustomCallbackData.SELECT:
                await HandleSelect(update, currencyCode, cancellationToken);
                break;

            case CustomCallbackData.TRACK:
                await HandleTrack(update, cancellationToken, currencyCode);
                break;

            case CustomCallbackData.CANCEL_TRACK:
                await HandleCancelTrack(update);
                break;
        }
    }

    public decimal GetPreviousPrice(long chatId)
    {
        var previousPrice = _previousCoinPrice[chatId];
        _previousCoinPrice.Remove(chatId);
        return previousPrice;
    }

    private async Task HandleStartChoice(Update update, CancellationToken cancellationToken)
    {
        var message = update.CallbackQuery.Message;
        await _telegramBotClient.DeleteMessageAsync(chatId: message.Chat.Id, messageId: message.MessageId,
            cancellationToken: cancellationToken);
        await _currencyBot.ShowCurrency(message, cancellationToken);
    }

    private async Task HandleChangeCurrency(Update update, CancellationToken cancellationToken)
    {
        var message = update.CallbackQuery.Message;
        await _currencyBot.ShowCurrency(message, cancellationToken);
    }

    private async Task HandleSelect(Update update, string currencyCode,
        CancellationToken cancellationToken)
    {
        var message = update.CallbackQuery.Message;
        var chatId = message.Chat.Id;
        await _currencyBot.DeleteMessage(update, cancellationToken);

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

    private async Task HandleTrack(Update update, CancellationToken cancellationToken,
        string currencyCode)
    {
        var message = update.CallbackQuery.Message;
        var chatId = message.Chat.Id;
        var price = await CoinMarket.GetPriceAsync(currencyCode);
        _previousCoinPrice.Add(chatId, price);

        await _telegramBotClient.SendTextMessageAsync(chatId, text: $"Валюта: {currencyCode}, стоимость: {price}$",
            cancellationToken: cancellationToken);
        _currencyBot.AddSelectedUserCurrency(chatId, currencyCode);

        await _telegramBotClient.SendTextMessageAsync(chatId, text: "Введите желаемый курс",
            cancellationToken: cancellationToken);
    }

    private async Task HandleCancelTrack(Update update)
    {
        try
        {
            var chatId = update.CallbackQuery.Message.Chat.Id;
            _currencyBot.GetTokenSource(chatId).Cancel();
            await _telegramBotClient.SendTextMessageAsync(chatId, "Отслеживание остановлено.");
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
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