using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace GDA_CoinBot;

/// <summary>
/// Класс команды отслеживать
/// </summary>
public class TrackCommand : Command
{
    private readonly TelegramBotClient _botClient;

    public TrackCommand(TelegramBotClient botClient)
    {
        _botClient = botClient;
    }

    // Переопределенный метод отправляет пользователю сообщение с выбором валюты и инлайн кнопка
    // С коллбэком действия Track и код валюты
    public override async Task HandleCommandAsync(Message message, CancellationToken cancellationToken)
    {
        var chatId = message.Chat.Id;
        // Создаем инлайн кнопки
        var inlineKeyboard = new InlineKeyboardMarkup(new[]
        {
            // Строка 1
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Bitcoin",
                    $"{CustomCallbackData.START_TRACK}|{CustomCallbackData.BTC}"),
                InlineKeyboardButton.WithCallbackData("Ethereum",
                    $"{CustomCallbackData.START_TRACK}|{CustomCallbackData.ETH}"),
            },
            // Строка 2
            new[]
            {
                InlineKeyboardButton.WithCallbackData("BNB",
                    $"{CustomCallbackData.START_TRACK}|{CustomCallbackData.BNB}"),
                InlineKeyboardButton.WithCallbackData("DogeCoin",
                    $"{CustomCallbackData.START_TRACK}|{CustomCallbackData.DOGE}"),
            }
        });
        
        // Отправляем сообщение с инлайн кнопками
        await _botClient.SendTextMessageAsync(chatId, text: "Выберите валютую",
            replyMarkup: inlineKeyboard, cancellationToken: cancellationToken);
    }
}