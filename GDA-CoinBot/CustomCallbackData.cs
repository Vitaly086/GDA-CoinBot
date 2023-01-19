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
    // Коллбэк нажатия на инлайн кнопку для вызова команды для смены валюты
    public const string CHANGE_CURRENCY = "ChangeCurrency";
}