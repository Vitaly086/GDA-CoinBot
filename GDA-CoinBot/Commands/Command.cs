using Telegram.Bot.Types;

namespace GDA_CoinBot;

/// <summary>
/// Абстрактный класс команд с абстрактным методом обработки команды
/// </summary>
public abstract class Command : ICommand
{
    public abstract Task HandleCommandAsync(Message message, CancellationToken cancellationToken);
}