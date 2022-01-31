using Newtonsoft.Json.Linq;
using System.Net;
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

        public BalanceDTO ToBalance()
        {
            return new BalanceDTO(BtcBalance, UsdBalance);
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

        public static double GetUsdEquivalent(long btcBalance)
        {
            string api_key = "8c62ad13-ed99-45cc-8ab9-ebfeffab60f3";
            var URL = new UriBuilder("https://pro-api.coinmarketcap.com/v1/cryptocurrency/quotes/latest");

            var queryString = HttpUtility.ParseQueryString(string.Empty);
            queryString["symbol"] = "BTC";
            queryString["convert"] = "USD";

            URL.Query = queryString.ToString();

            var client = new WebClient();
            client.Headers.Add("X-CMC_PRO_API_KEY", api_key);
            client.Headers.Add("Accepts", "application/json");
            // client.Headers.Add("Accept - Encoding", "deflate, gzip");
            string retJson = client.DownloadString(URL.ToString());
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

        public BalanceDTO(long btcBalance, long usdBalance, double? usdEquivalent = null)
        {

            BtcBalance = btcBalance;
            UsdBalance = usdBalance;
            if (usdEquivalent != null) UsdEquivalent = usdEquivalent.Value;
            else UsdEquivalent = GetUsdEquivalent(BtcBalance);
        }
    }
}
