
class Program
{
    static void Main(string[] args)
    {
        var bot = new CurrencyBot(ApiConstants.BOT_API);
        bot.CreateCommands();
        bot.StartReceivingAsync();
        Console.ReadKey();
    }
}