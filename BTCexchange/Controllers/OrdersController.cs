using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using BTCexchange.Models;

namespace BTCexchange.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrdersController : ControllerBase
    {
        private readonly BTCexchangeContext _context;
        public static readonly object orderLock = new object();

        public OrdersController(BTCexchangeContext context)
        {
            _context = context;
        }

        private static async Task<string> NotifyUrlAsync(string url, int id)   // This is not tested
        {
            HttpClient client = new HttpClient();
            Dictionary<string, string> parameters = new Dictionary<string, string> { { "OrderId", id.ToString() } };
            FormUrlEncodedContent encodedContent = new FormUrlEncodedContent(parameters);
            HttpResponseMessage response = await client.PostAsync(url, encodedContent);
            response.EnsureSuccessStatusCode();
            string responseContent = await response.Content.ReadAsStringAsync();

            return responseContent;
        }

        private static string NotifyUrl(string url, int id)   // This is not tested
        {
            HttpClient client = new HttpClient();
            Dictionary<string, string> parameters = new Dictionary<string, string> { { "OrderId", id.ToString() } };
            FormUrlEncodedContent encodedContent = new FormUrlEncodedContent(parameters);
            Task<HttpResponseMessage> task = Task.Run(() => client.PostAsync(url, encodedContent));
            task.Wait();
            HttpResponseMessage response = task.Result;
            response.EnsureSuccessStatusCode();
            StreamReader reader = new StreamReader(response.Content.ReadAsStream());
            string responseContent = reader.ReadToEnd();

            return responseContent;
        }


        private MarketOrderReturn BuyBTC(long quantity, double? limitPrice = null)
        {
            if (limitPrice == null) limitPrice = double.MaxValue;
            Order[] sellingOrders = _context.Orders.Where(o => !o.Buying && o.Status == "LIVE" && o.LimitPrice <= limitPrice).OrderBy(o => o.LimitPrice).ToArray();   // maybe do not trade with himself

            long remQuantity = quantity;
            long price = 0L;

            foreach (Order order in sellingOrders)
            {
                // update both orders
                long processingQuantity = Math.Min(order.RemainQuantity, remQuantity);
                if (processingQuantity > 0)
                {
                    User? user = _context.Users.Find(order.UserId);
                    if (user == null) throw new InvalidOperationException($"Didn't find user {order.UserId} for StandingOrder {order.Id}");
                    remQuantity -= processingQuantity;
                    price += processingQuantity * order.LimitPrice;

                    order.AvgPrice = (order.AvgPrice * order.FilledQuantity + order.LimitPrice * processingQuantity) / (order.FilledQuantity + processingQuantity);
                    order.RemainQuantity -= processingQuantity;
                    order.FilledQuantity += processingQuantity;
                    if (order.RemainQuantity == 0L) order.Status = "FULFILLED";

                    _context.Entry(order).State = EntityState.Modified;

                    user.UsdBalance += processingQuantity * order.LimitPrice;
                    _context.Entry(user).State = EntityState.Modified;
                    _context.SaveChanges();
                }
                // notify order via Webhook (maybe move to async, not locked part?)
                if (order.NotifyUrl != null)
                {
                    string response = NotifyUrl(order.NotifyUrl, order.Id);
                }

                if (remQuantity == 0) break;

            }

            double avgPrice = 0.0;
            if (quantity - remQuantity != 0) avgPrice = price / (double)(quantity - remQuantity);
            return new MarketOrderReturn(quantity - remQuantity, avgPrice);
        }

        private MarketOrderReturn SellBTC(long quantity, double? limitPrice = null)
        {
            if (limitPrice == null) limitPrice = 0.0;
            Order[] buyingOrders = _context.Orders.Where(o => o.Buying && o.Status == "LIVE" && o.LimitPrice >= limitPrice).OrderByDescending(o => o.LimitPrice).ToArray();

            long remQuantity = quantity;
            long price = 0L;

            foreach (Order order in buyingOrders)
            {
                // update both order and the order's user's balance
                long processingQuantity = Math.Min(order.RemainQuantity, remQuantity);
                if (processingQuantity > 0)
                {
                    User? user = _context.Users.Find(order.UserId);
                    if (user == null) throw new InvalidOperationException($"Didn't find user {order.UserId} for StandingOrder {order.Id}");
                    remQuantity -= processingQuantity;
                    price += processingQuantity * order.LimitPrice;

                    order.AvgPrice = (order.AvgPrice * order.FilledQuantity + order.LimitPrice * processingQuantity) / (order.FilledQuantity + processingQuantity);
                    order.RemainQuantity -= processingQuantity;
                    order.FilledQuantity += processingQuantity;
                    if (order.RemainQuantity == 0L) order.Status = "FULFILLED";

                    _context.Entry(order).State = EntityState.Modified;

                    user.BtcBalance += processingQuantity;
                    _context.Entry(user).State = EntityState.Modified;
                    _context.SaveChanges();
                }
                // notify order via Webhook (maybe move to async, not locked part?)
                if (order.NotifyUrl != null)
                {
                    string response = NotifyUrl(order.NotifyUrl, order.Id);
                }

                if (remQuantity == 0) break;
            }

            double avgPrice = 0.0;
            if (quantity - remQuantity != 0) avgPrice = price / (double)(quantity - remQuantity);
            return new MarketOrderReturn(quantity - remQuantity, avgPrice);
        }

        private MarketOrderReturn FulfillOrder(long quantity, bool buying, User user, double? limitPrice = null)
        {
            if (buying)
            {
                MarketOrderReturn ret = BuyBTC(quantity, limitPrice);
                if (ret.Quantity > 0)
                {
                    user.BtcBalance += ret.Quantity;
                    user.UsdBalance -= (long)(ret.AvgPrice * ret.Quantity);
                    _context.Entry(user).State = EntityState.Modified;
                    _context.SaveChanges();
                }
                return ret;
            }
            else
            {
                MarketOrderReturn ret = SellBTC(quantity, limitPrice);
                if (ret.Quantity > 0)
                {
                    user.BtcBalance -= ret.Quantity;
                    user.UsdBalance += (long)(ret.AvgPrice * ret.Quantity);
                    _context.Entry(user).State = EntityState.Modified;
                    _context.SaveChanges();
                }
                return ret;
            }
        }

        // GET: api/Orders/Market/quantity/type
        [HttpGet("Market")]
        public async Task<ActionResult<MarketOrderReturn>> MarketOrder(long quantity, string type)
        {
            // get header:
            string? token = BTCexchangeContext.GetToken(Request.Headers);

            // search for header in database
            if (token == null) return Unauthorized();
            User? user = await _context.FindUserByToken(token);
            if (user == null) return NotFound();

            bool buying = IsBuying(type);

            lock (orderLock)
            {
                return FulfillOrder(quantity, buying, user);
            }
        }

        // GET: api/Orders
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Order>>> GetOrders()
        {
            return await _context.Orders.ToListAsync();
        }

        // GET: api/Orders/Standing/5
        [HttpGet("Standing/{id}")]
        public async Task<ActionResult<OrderDTO>> GetOrder(int id)
        {
            // get header:
            string? token = BTCexchangeContext.GetToken(Request.Headers);

            // search for header in database
            if (token == null) return Unauthorized();
            User? user = await _context.FindUserByToken(token);
            if (user == null) return NotFound(token);

            var order = await _context.Orders.FindAsync(id);

            if (order == null)
            {
                return NotFound();
            }

            // check if the order was for the correct user
            if (order.UserId != user.Id) return Unauthorized($"Order {id} does not belong to the Authorized User");

            return order.ToDTO();
        }

        private static bool CanPerformOrder(long quantity, bool buying, long limitPrice, User user)
        {
            if (buying) return user.UsdBalance >= limitPrice * quantity;
            else return user.BtcBalance >= quantity;
        }

        private static bool IsBuying(string type)
        {
            switch (type)
            {
                case "BUY":
                    {
                        return true;
                    }
                case "SELL":
                    {
                        return false;
                    }
                default:
                    {
                        throw new InvalidOperationException("Invalid type (BUY or SELL accepted): " + type);
                    }
            }
        }

        // POST: api/Orders/Standing        
        [HttpPost("Standing")]
        public async Task<ActionResult<int>> PostOrder(long quantity, string type, long limitPrice, string? webhookUrl = null)
        {
            // get header:
            string? token = BTCexchangeContext.GetToken(Request.Headers);

            // search for header in database
            if (token == null) return Unauthorized();
            User? user = await _context.FindUserByToken(token);
            if (user == null) return NotFound();

            // create (partial) order
            Order order = new Order();
            order.UserId = user.Id;
            order.LimitPrice = limitPrice;
            order.NotifyUrl = webhookUrl;
            order.Buying = IsBuying(type);
            order.FilledQuantity = 0L;
            order.RemainQuantity = quantity;
            order.AvgPrice = 0L;

            lock (orderLock)
            {
                order.Status = CanPerformOrder(quantity, order.Buying, limitPrice, user) ? "LIVE" : "CANCELED";

                // try to fulfill the order                       
                if (order.Status == "LIVE")
                {
                    MarketOrderReturn mor = FulfillOrder(quantity, order.Buying, user, limitPrice);
                    order.FilledQuantity += mor.Quantity;
                    order.RemainQuantity -= mor.Quantity;
                    order.AvgPrice = mor.AvgPrice;
                    if (order.RemainQuantity == 0) order.Status = "FULFILLED";

                    // subtract the resources from the user
                    if (order.Buying)
                    {
                        user.BtcBalance += mor.Quantity;
                        user.UsdBalance -= quantity * limitPrice;
                    }
                    else
                    {
                        user.BtcBalance -= quantity;
                        user.UsdBalance += (long)(mor.Quantity * mor.AvgPrice);
                    }
                }

                // put the changes in the db:
                _context.Entry(user).State = EntityState.Modified;
                _context.Orders.Add(order);
                _context.SaveChanges();
            }

            return order.Id;
        }

        // DELETE: api/Orders/Standing/5
        [HttpDelete("Standing/{id}")]
        public async Task<IActionResult> DeleteOrder(int id)
        {
            // get header:
            string? token = BTCexchangeContext.GetToken(Request.Headers);

            // search for header in database
            if (token == null) return Unauthorized();
            User? userAuth = await _context.FindUserByToken(token);
            if (userAuth == null) return NotFound();

            lock (orderLock)
            {
                // search for order
                Order? order = _context.Orders.Find(id);
                if (order == null)
                {
                    return NotFound();
                }
                if (order.UserId != userAuth.Id) return Unauthorized($"Order {id} does not belong to the Authorized User");

                // return allocated resources to user
                User? user = _context.Users.Find(order.UserId);
                if (user == null) throw new InvalidOperationException($"Didn't find user {order.UserId} for StandingOrder {order.Id}");

                if (order.Buying && order.RemainQuantity > 0 && order.Status == "LIVE")
                {
                    user.UsdBalance += order.RemainQuantity * order.LimitPrice;
                    _context.Entry(user).State = EntityState.Modified;
                }
                if (!order.Buying && order.RemainQuantity > 0 && order.Status == "LIVE")
                {
                    user.BtcBalance += order.RemainQuantity;
                    _context.Entry(user).State = EntityState.Modified;
                }

                // update database
                _context.Orders.Remove(order);
                _context.SaveChanges();
            }
            return NoContent();
        }
    }
}
