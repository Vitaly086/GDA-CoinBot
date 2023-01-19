using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace GDA_CoinBot;

/// <summary>
/// Класс обработчик коллбэков
/// Содержит словарь с ценой валюты, которую выбрал пользователь для отслеживания
/// </summary>
public class CallbackHandler
{
    private readonly Dictionary<long, decimal> _previousCoinPrice = new();
    private readonly ITelegramBotClient _telegramBotClient;
    private readonly CurrencyBot _currencyBot;

    public CallbackHandler(ITelegramBotClient telegramBotClient, CurrencyBot currencyBot)
    {
        _telegramBotClient = telegramBotClient;
        _currencyBot = currencyBot;
    }

    /// <summary>
    ///  Метод обрабатывает все коллбэки нажатия на инлай кнопки пользователем
    ///  И запускает выполнение нужного метода в зависимости от полученного сообщения
    /// </summary>
    /// <param name="update"> Информация о произошедшем событии </param>
    public async Task HandleCallbackQueryAsync(Update update, CancellationToken cancellationToken)
    {
        // Разделяем коллбэк на действие и код валюты
        var action = SplitCallbackData(update, out var currencyCode);
        
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
    
    /// <summary>
    /// Метод возвращает прайс, который видел пользователь на момент принятие решения о желаемом курсе
    /// </summary>
    /// <returns></returns>
    public decimal GetPreviousPrice(long chatId)
    {
        var previousPrice = _previousCoinPrice[chatId];
        _previousCoinPrice.Remove(chatId);
        return previousPrice;
    }
    
    /// <summary>
    /// Обработка коллбэка нажатия на инлайн кнопку в стартовом сообщении
    /// </summary>
    /// <param name="update"> Информация о произошедшем событии </param>
    private async Task HandleStartChoice(Update update, CancellationToken cancellationToken)
    {
        var message = update.CallbackQuery.Message;
        await _currencyBot.DeleteMessage(message.Chat.Id, message.MessageId, cancellationToken);
        // Показываем меню с выбором валюты
        await _currencyBot.ShowCurrencySelectionAsync(message, cancellationToken);
    }

    /// <summary>
    /// Обработка коллбэка нажатия на инлайн кнопку смены кода валюты
    /// </summary>
    /// <param name="update"> Информация о произошедшем событии </param>
    private async Task HandleChangeCurrency(Update update, CancellationToken cancellationToken)
    {
        var message = update.CallbackQuery.Message;
        // Выводим меню с выбором валюты
        await _currencyBot.ShowCurrencySelectionAsync(message, cancellationToken);
    }

    /// <summary>
    /// Обработка коллбэка нажатия на инлайн кнопку выбора кода валюты 
    /// </summary>
    /// <param name="update"> Информация о произошедшем событии </param>
    private async Task HandleSelectCurrency(Update update, string currencyCode,
        CancellationToken cancellationToken)
    {
        var message = update.CallbackQuery.Message;
        var chatId = message.Chat.Id;
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

    /// <summary>
    /// Обработка коллбэка нажатия на инлайн кнопку начала отслеживания валюты
    /// </summary>
    /// <param name="update"> Информация о произошедшем событии </param>
    /// <param name="currencyCode"> Код выбранной валюты </param>
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

        await _telegramBotClient.SendTextMessageAsync(chatId, text: $"Валюта: {currencyCode}, стоимость: {price}$",
            cancellationToken: cancellationToken);
        _currencyBot.AddCurrencyForTrack(chatId, currencyCode);

        await _telegramBotClient.SendTextMessageAsync(chatId, text: "Введите желаемый курс",
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Обработка коллбэка нажатия на инлайн кнопку остановки отслежиавния цены валюты
    /// </summary>
    /// <param name="update"> Информация о произошедшем событии </param>
    private async Task HandleCancelTrack(Update update)
    {
        var chatId = update.CallbackQuery.Message.Chat.Id;
        
        // Получаем tokenSource для выбранной валюты и останавливаем его
        _currencyBot.GetTokenSource(chatId).Cancel();
        
        await _telegramBotClient.SendTextMessageAsync(chatId, "Отслеживание остановлено.");
    }

    /// <summary>
    /// Метод для разделения коллбэка на действие и код валюты
    /// Например действие - Select | код валюты BTC
    /// </summary>
    /// <param name="update"> Информация о произошедшем событии </param>
    /// <param name="currencyCode"> Код выбранной валюты </param>
    /// <returns></returns>
    private static string SplitCallbackData(Update update, out string currencyCode)
    {
        // Получаемое сообщение разделаем на массив слов
        var words = update.CallbackQuery.Data.Split('|');

        // Первое слово это действие
        var action = words[0];
        currencyCode = string.Empty;
        
        // Проверяем, если слов больше чем одно, сохраняем код валюты
        if (words.Length > 1)
        {
            currencyCode = words[1];
        }

        return action;
    }
}