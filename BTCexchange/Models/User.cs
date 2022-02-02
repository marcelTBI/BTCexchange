using Newtonsoft.Json.Linq;
using System.Web;

namespace BTCexchange.Models
{
    public partial class User
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Token { get; set; } = null!;
        public long BtcBalance { get; set; }
        public long UsdBalance { get; set; }

        public UserDTO ToDTO()
        {
            return new UserDTO(Id, Name, Token, BtcBalance, UsdBalance);
        }

        public async static Task<double> GetUsdEquivalent(long btcBalance)
        {
            string api_key = "8c62ad13-ed99-45cc-8ab9-ebfeffab60f3";
            var URL = new UriBuilder("https://pro-api.coinmarketcap.com/v1/cryptocurrency/quotes/latest");

            var queryString = HttpUtility.ParseQueryString(string.Empty);
            queryString["symbol"] = "BTC";
            queryString["convert"] = "USD";

            URL.Query = queryString.ToString();

            var client = new HttpClient();

            using (var request = new HttpRequestMessage(HttpMethod.Get, URL.ToString()))
            {
                request.Headers.Add("X-CMC_PRO_API_KEY", api_key);
                request.Headers.Add("Accepts", "application/json");
                HttpResponseMessage response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string retJson = await response.Content.ReadAsStringAsync();
                dynamic jsonReturn = JObject.Parse(retJson);
                try
                {
                    double btcToUsd = Convert.ToDouble(jsonReturn.data.BTC.quote.USD.price);
                    return btcToUsd * btcBalance;   // TODO Satoshi
                }
                catch (Exception)
                {
                    throw;
                }
            }
        }

        public async Task<BalanceDTO> ToBalance()
        {
            return new BalanceDTO(BtcBalance, UsdBalance, await GetUsdEquivalent(BtcBalance));
        }
    }

    public class UserDTO
    {
        public int Id { get; }
        public string Name { get; set; }
        public string Token { get; }
        public long BtcBalance { get; set; }
        public long UsdBalance { get; set; }

        public UserDTO(int id, string name, string token, long btc, long usd)
        {
            Id = id;
            Name = name;
            Token = token;
            BtcBalance = btc;
            UsdBalance = usd;
        }
    }

    public class BalanceDTO
    {
        public long BtcBalance { get; set; }
        public long UsdBalance { get; set; }
        public double UsdEquivalent { get; }

        public BalanceDTO(long btcBalance, long usdBalance, double usdEquivalent)
        {
            BtcBalance = btcBalance;
            UsdBalance = usdBalance;
            UsdEquivalent = usdEquivalent;            
        }
    }
}
