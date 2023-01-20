using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace GDA_CoinBot;

/// <summary>
/// Класс команды, которая обрабатывает коллбэк отслеживание изменения цены валюты
/// </summary>
public class TrackCommand : Command
{
    private readonly TelegramBotClient _botClient;

    public TrackCommand(TelegramBotClient botClient)
    {
        _botClient = botClient;
    }
    
    /// <summary>
    /// Метод отправляет пользователю сообщение с выбором валюты 
    /// И инлайн кнопками с коллбэком действия Track и кодом валюты
    /// Например действие Track | код валюты BTC
    /// </summary>
    /// <param name="message"> Сообщение полученное от пользователя </param>
    /// <param name="cancellationToken"> Токен отмены</param>
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