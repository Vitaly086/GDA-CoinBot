using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace GDA_CoinBot;

public class ShowCurrencyMenuCommand : Command
{
    private readonly TelegramBotClient _botClient;


    public ShowCurrencyMenuCommand(TelegramBotClient botClient)
    {
        _botClient = botClient;
    }


    public override async Task ExecuteAsync(Message message, CancellationToken cancellationToken)
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

        await _botClient.SendTextMessageAsync(chatId: chatId,
            text: "Выберите валюту:",
            replyMarkup: inlineKeyboard, cancellationToken: cancellationToken);
    }
}