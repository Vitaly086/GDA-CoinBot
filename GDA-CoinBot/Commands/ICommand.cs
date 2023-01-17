using Telegram.Bot.Types;

namespace GDA_CoinBot;

/// <summary>
/// Интерфейс с методом обработки команды
/// </summary>
public interface ICommand
{
    public Task HandleCommandAsync(Message message, CancellationToken cancellationToken);

}