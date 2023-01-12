using Telegram.Bot.Types;

namespace GDA_CoinBot;

public abstract class Command
{
    public abstract Task ExecuteAsync(Message message, CancellationToken cancellationToken);
}