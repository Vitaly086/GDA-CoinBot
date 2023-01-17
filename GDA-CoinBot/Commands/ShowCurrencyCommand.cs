using Telegram.Bot.Types;

namespace GDA_CoinBot;

/// <summary>
/// Класс команды вывода валюты пользователю
/// </summary>
public class ShowCurrencyCommand : Command
{
    private readonly CurrencyBot _currencyBot;
    public ShowCurrencyCommand(CurrencyBot currencyBot)
    {
        _currencyBot = currencyBot;
    }
    
    /// <summary>
    /// Переопределенный метод отправляет пользователю сообщение выбора валют с инлайн кнопками
    /// </summary>
    public override async Task HandleCommandAsync(Message message, CancellationToken cancellationToken)
    {
        await _currencyBot.ShowCurrencySelectionAsync(message, cancellationToken);
    }
}