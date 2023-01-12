using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace GDA_CoinBot;

public class TrackMenuCommand : Command
{
    private readonly TelegramBotClient _botClient;

    public TrackMenuCommand(TelegramBotClient botClient)
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
                    $"{CustomCallbackData.TRACK}|{CustomCallbackData.BTC}"),
                InlineKeyboardButton.WithCallbackData("Ethereum",
                    $"{CustomCallbackData.TRACK}|{CustomCallbackData.ETH}"),
            },
            // Row 2
            new[]
            {
                InlineKeyboardButton.WithCallbackData("BNB",
                    $"{CustomCallbackData.TRACK}|{CustomCallbackData.BNB}"),
                InlineKeyboardButton.WithCallbackData("DogeCoin",
                    $"{CustomCallbackData.TRACK}|{CustomCallbackData.DOGE}"),
            }
        });

        await _botClient.SendTextMessageAsync(chatId, text: "Выберите валютую",
            replyMarkup: inlineKeyboard, cancellationToken: cancellationToken);
    }
}