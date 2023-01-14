using Telegram.Bot.Types;

namespace GDA_CoinBot;

public abstract class Command : ICommand
{
    public abstract Task HandleCommandAsync(Message message, CancellationToken cancellationToken);
}