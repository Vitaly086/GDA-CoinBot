
class Program
{
    static void Main(string[] args)
    {
        // Создадим новый экземпляр бота
        var currencyBot = new CurrencyBot(ApiConstants.BOT_API);
        // Создадим команды бота
        currencyBot.CreateCommands();
        // Начнем отслеживание
        currencyBot.StartReceivingAsync();
        // Ожидаем нажатия клавиши до завершения программы
        Console.ReadKey();
    }
}