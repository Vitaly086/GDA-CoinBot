using Telegram.Bot.Types;

namespace GDA_CoinBot;

public interface ICommand
{
    public Task HandleCommandAsync(Message message, CancellationToken cancellationToken);

}