
class Program
{
    static void Main(string[] args)
    {
        var currencyBot = new CurrencyBot(ApiConstants.BOT_API);
        currencyBot.CreateCommands();
        currencyBot.StartReceivingAsync();
        // Ожидаем нажатия клавиши до завершения программы
        Console.ReadKey();
    }
}