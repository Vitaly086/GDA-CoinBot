using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace GDA_CoinBot;

public class StartCommand : Command
{
    private readonly CurrencyBot _currencyBot;
    
    public StartCommand(CurrencyBot currencyBot)
    {
        _currencyBot = currencyBot;
    }

    public override async Task HandleCommandAsync(Message message, CancellationToken cancellationToken)
    {
        await _currencyBot.ShowCurrency(message, cancellationToken);
    }
}