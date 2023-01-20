using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace GDA_CoinBot;

/// <summary>
/// Класс команды Старт
/// </summary>
public class StartCommand : Command
{
    private readonly ITelegramBotClient _botClient;

    public StartCommand(ITelegramBotClient botClient)
    {
        _botClient = botClient;
    }

    /// <summary>
    /// Метод отправляет пользователю стартовое сообщение
    /// С инлайн кнопкой и коллбэком стартового выбора
    /// </summary>
    /// <param name="message"> Сообщение полученное от пользователя </param>
    /// <param name="cancellationToken"> Токен отмены</param>
    public override async Task HandleCommandAsync(Message message, CancellationToken cancellationToken)
    {
        var chatId = message.Chat.Id;
        // Создаем инлайн кнопку с коллбэком овета на стартовое сообщение
        var inlineKeyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Выбрать валюту.", CustomCallbackData.START_CHOICE)
            }
        });

        // Отправляем сообщение с инлайн кнопкой
        await _botClient.SendTextMessageAsync(
            chatId, text: "Привет!\n" + "Данный бот показывает текущий курс выбранной валюты.\n",
            replyMarkup: inlineKeyboard, cancellationToken: cancellationToken);
    }
}