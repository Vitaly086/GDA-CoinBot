using System.Text.Json;
using CoinMarketCap;
using GDA_CoinBot;

public static class CoinMarket
{
    private static readonly string API_KEY = ApiConstants.COIN_MARKET_API;
    private static readonly CoinMarketCapClient  _client = new CoinMarketCapClient(API_KEY);
    
    
    public static async Task<decimal> GetPriceAsync(string currencyCode)
    {
        using (var httpClient = new HttpClient())
        {
            httpClient.DefaultRequestHeaders.Add("X-CMC_PRO_API_KEY", API_KEY);

            var response = await httpClient.GetAsync($"https://pro-api.coinmarketcap.com/v1/cryptocurrency/quotes/latest?symbol={currencyCode}&convert=USD");
            var responseString = await response.Content.ReadAsStringAsync();

            var jsonResponse = JsonDocument.Parse(responseString);
            var price = jsonResponse.RootElement.GetProperty("data").GetProperty(currencyCode).GetProperty("quote").GetProperty("USD").GetProperty("price").GetDecimal();
        
            return price;
        }
    }
}