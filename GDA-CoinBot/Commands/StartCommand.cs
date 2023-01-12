using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace GDA_CoinBot;

public class StartCommand : Command
{
    private readonly TelegramBotClient _botClient;


    public StartCommand(TelegramBotClient botClient)
    {
        _botClient = botClient;
    }


    public override async Task ExecuteAsync(Message message, CancellationToken cancellationToken)
    {
        var chatId = message.Chat.Id;
        await _botClient.DeleteMessageAsync(chatId, message.MessageId, cancellationToken: cancellationToken);

        var inlineKeyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Выбрать валюту.", CustomCallbackData.START_CHOICE)
            }
        });

        await _botClient.SendTextMessageAsync(
            chatId: chatId,
            text: "Привет!\n" +
                  "Данный бот показывает текущий курс выбранной валюты.\n",
            replyMarkup: inlineKeyboard, cancellationToken: cancellationToken);
    }
}