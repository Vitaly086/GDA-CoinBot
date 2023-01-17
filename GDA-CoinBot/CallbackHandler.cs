using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace GDA_CoinBot;

/// <summary>
/// Класс обработчик коллбэков
/// </summary>
public class CallbackHandler
{
    // Словарь выбора валюты для отслеживания пользователем
    private readonly Dictionary<long, decimal> _previousCoinPrice = new();
    private readonly ITelegramBotClient _telegramBotClient;
    private readonly CurrencyBot _currencyBot;

    public CallbackHandler(ITelegramBotClient telegramBotClient, CurrencyBot currencyBot)
    {
        _telegramBotClient = telegramBotClient;
        _currencyBot = currencyBot;
    }

    // Метод обработки коллбэка
    public async Task HandleCallbackQueryAsync(Update update, CancellationToken cancellationToken)
    {
        // Разделяем коллбэк на действие и код валюты
        var action = SplitCallbackData(update, out var currencyCode);

        // Выбираем кейс по данным из коллбэка и запускаем нужный метод
        switch (action)
        {
            case CustomCallbackData.START_CHOICE:
                await HandleStartChoice(update, cancellationToken);
                break;

            case CustomCallbackData.CHANGE_CURRENCY:
                await HandleChangeCurrency(update, cancellationToken);
                break;

            case CustomCallbackData.SELECT_CURRENCY:
                await HandleSelectCurrency(update, currencyCode, cancellationToken);
                break;

            case CustomCallbackData.START_TRACK:
                await HandleStartTrack(update, cancellationToken, currencyCode);
                break;

            case CustomCallbackData.CANCEL_TRACK:
                await HandleCancelTrack(update);
                break;
        }
    }

    // Метод возвращает прайс, который видел пользователь на момент принятие решения о желаемом курсе
    public decimal GetPreviousPrice(long chatId)
    {
        // Получаем цену валюты
        var previousPrice = _previousCoinPrice[chatId];
        // Удаляем цену валюты из словаря
        _previousCoinPrice.Remove(chatId);
        // Возвращаем цену вылаюты
        return previousPrice;
    }

    // Метод обработки коллбэка инлайн кнопки команды старт
    private async Task HandleStartChoice(Update update, CancellationToken cancellationToken)
    {
        var message = update.CallbackQuery.Message;
        // Удаляем стартовое сообщение
        await _telegramBotClient.DeleteMessageAsync(chatId: message.Chat.Id, messageId: message.MessageId,
            cancellationToken: cancellationToken);
        // Показываем меню с выбором валюты
        await _currencyBot.ShowCurrencySelectionAsync(message, cancellationToken);
    }

    // Метод обработки коллбэка смены валюты
    private async Task HandleChangeCurrency(Update update, CancellationToken cancellationToken)
    {
        var message = update.CallbackQuery.Message;
        // Выводим меню с выбором валюты
        await _currencyBot.ShowCurrencySelectionAsync(message, cancellationToken);
    }

    // Метод обработки коллбэка выбора валюты 
    private async Task HandleSelectCurrency(Update update, string currencyCode,
        CancellationToken cancellationToken)
    {
        var message = update.CallbackQuery.Message;
        var chatId = message.Chat.Id;
        // Удаляем сообщение
        await _currencyBot.DeleteMessage(chatId, message.MessageId, cancellationToken);

        // Получаем прайс валюты
        var price = await CoinMarket.GetPriceAsync(currencyCode);

        // Создаем инлайн кнопку с коллбэком смены валюты
        var inlineKeyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Выбрать другую валюту.", CustomCallbackData.CHANGE_CURRENCY)
            }
        });
        // Отправляем сообщение с инлайн кнопкой
        await _telegramBotClient.SendTextMessageAsync(chatId, text: $"Валюта: {currencyCode}, стоимость: {price}$",
            replyMarkup: inlineKeyboard, cancellationToken: cancellationToken);
    }

    // Метод обработки коллбэка начала отслеживания валюты
    private async Task HandleStartTrack(Update update, CancellationToken cancellationToken,
        string currencyCode)
    {
        var message = update.CallbackQuery.Message;
        var chatId = message.Chat.Id;
        // Получаем текущий курс валюты
        var price = await CoinMarket.GetPriceAsync(currencyCode);
        // Добавляем курс валюты в словарь, чтобы после ввода пользоваетелем желаемой валюты
        // Понять, нужен курс больше или меньше текущего
        _previousCoinPrice.Add(chatId, price);

        // Отправляем пользователю сообщение со стоимостью валюты
        await _telegramBotClient.SendTextMessageAsync(chatId, text: $"Валюта: {currencyCode}, стоимость: {price}$",
            cancellationToken: cancellationToken);
        _currencyBot.AddSelectedUserCurrency(chatId, currencyCode);

        // Отправляем сообщение с предложением ввести желаемый курс
        await _telegramBotClient.SendTextMessageAsync(chatId, text: "Введите желаемый курс",
            cancellationToken: cancellationToken);
    }

    // Метод обработки коллбэка остановки отслежиавния
    private async Task HandleCancelTrack(Update update)
    {
        var chatId = update.CallbackQuery.Message.Chat.Id;
        // Получаем tokenSource для выбранной валюты и останавливаем его
        _currencyBot.GetTokenSource(chatId).Cancel();
        // Выводим сообщение, что отслежиавние остановлено
        await _telegramBotClient.SendTextMessageAsync(chatId, "Отслеживание остановлено.");
    }

    // Метод для разделения коллбэка на действие и код валюты
    private static string SplitCallbackData(Update update, out string currencyCode)
    {
        // Получаемое сообщение разделаем на массив слов
        var words = update.CallbackQuery.Data.Split('|');

        // Первое слово это действие
        var action = words[0];
        currencyCode = string.Empty;
        // Проверяем, если слов больше чем один, сохраняем код валюты
        if (words.Length > 1)
        {
            currencyCode = words[1];
        }

        return action;
    }
}