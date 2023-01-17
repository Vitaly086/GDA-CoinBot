namespace GDA_CoinBot;

/// <summary>
/// Класс содержит константы коллбэков
/// </summary>
public static class CustomCallbackData
{
    // Коды валют
    public const string BTC = "BTC";
    public const string ETH = "ETH";
    public const string BNB = "BNB";
    public const string DOGE = "DOGE";
    
    // Коллбэк нажатия на инлайн кнопку в ответ на стартовое сообщение
    public const string START_CHOICE = "StartChoice";
    // Коллбэк выбора кода вылюты
    public const string SELECT_CURRENCY = "SelectCurrency";
    // Коллбэк вызова команды для смены валюты
    public const string CHANGE_CURRENCY = "ChangeCurrency";
    // Коллбэк начала отслеживания валюты
    public const string START_TRACK = "StartTrack";
    // Коллбэк отмены отслеживания валюты
    public const string CANCEL_TRACK = "CancelTrack";
}