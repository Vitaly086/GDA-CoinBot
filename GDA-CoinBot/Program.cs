
class Program
{
    static void Main(string[] args)
    {
        var currencyBot = new CurrencyBot(ApiConstants.BOT_API);
        currencyBot.CreateCommands();
        currencyBot.StartReceiving();
        // Ожидаем нажатия клавиши до завершения программы
        Console.ReadKey();
    }
}