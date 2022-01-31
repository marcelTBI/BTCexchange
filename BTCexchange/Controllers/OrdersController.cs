using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BTCexchange.Models;
using System.Net;
using System.Text;

namespace BTCexchange.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrdersController : ControllerBase
    {
        private readonly BTCexchangeContext _context;

        public OrdersController(BTCexchangeContext context)
        {
            _context = context;
        }

        public string NotifyUrl(string url, int id)   // This is not tested
        {
            HttpWebRequest rq = (HttpWebRequest)WebRequest.Create(url);
            using (Stream s = rq.GetRequestStream())
            {
                // write data here
                using (var writer = new BinaryWriter(s, Encoding.UTF8, false))
                {
                    writer.Write(id);
                }
            }

            string response = "";
            HttpWebResponse resp = (HttpWebResponse)rq.GetResponse();
            using (Stream s = resp.GetResponseStream())
            {
                using (var reader = new BinaryReader(s, Encoding.UTF8, false))
                {
                    response = reader.ReadString();
                }
            }

            return response;
        }


        private async Task<MarketOrderReturn> BuyBTC(long quantity, double? limitPrice = null)
        {
            if (limitPrice == null) limitPrice = double.MaxValue;
            Order[] sellingOrders = await _context.Orders.Where(o => !o.Buying && o.Status == "LIVE" && o.LimitPrice <= limitPrice).OrderBy(o => o.LimitPrice).ToArrayAsync();

            long remQuantity = quantity;
            long price = 0L;

            foreach (Order order in sellingOrders)
            {
                // update both orders
                long processingQuantity = Math.Min(order.RemainQuantity, remQuantity);
                if (processingQuantity > 0)
                {
                    User? user = await _context.Users.FindAsync(order.UserId);
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
                    await _context.SaveChangesAsync();
                }
                // notify order via Webhook TODO
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

        private async Task<MarketOrderReturn> SellBTC(long quantity, double? limitPrice = null)
        {
            if (limitPrice == null) limitPrice = 0.0;
            Order[] buyingOrders = await _context.Orders.Where(o => o.Buying && o.Status == "LIVE" && o.LimitPrice >= limitPrice).OrderByDescending(o => o.LimitPrice).ToArrayAsync();

            long remQuantity = quantity;
            long price = 0L;

            foreach (Order order in buyingOrders)
            {
                // update both order and the order's user's balance
                long processingQuantity = Math.Min(order.RemainQuantity, remQuantity);
                if (processingQuantity > 0)
                {
                    User? user = await _context.Users.FindAsync(order.UserId);
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
                    await _context.SaveChangesAsync();
                }
                // notify order via Webhook TODO
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

        private async Task<MarketOrderReturn> FulfillOrder(long quantity, string type, User user, double? limitPrice = null)
        {
            switch (type)
            {
                case "BUY":
                    {
                        MarketOrderReturn ret = await BuyBTC(quantity, limitPrice);
                        if (ret.quantity > 0)
                        {
                            user.BtcBalance += ret.quantity;
                            user.UsdBalance -= (long)(ret.avgPrice * ret.quantity);
                            _context.Entry(user).State = EntityState.Modified;
                            await _context.SaveChangesAsync();
                        }
                        return ret;
                    }
                case "SELL":
                    {
                        MarketOrderReturn ret = await SellBTC(quantity, limitPrice);
                        if (ret.quantity > 0)
                        {
                            user.BtcBalance -= ret.quantity;
                            user.UsdBalance += (long)(ret.avgPrice * ret.quantity);
                            _context.Entry(user).State = EntityState.Modified;
                            await _context.SaveChangesAsync();
                        }
                        return ret;
                    }
                default:
                    {
                        throw new InvalidOperationException("Invalid type (BUY or SELL accepted): " + type);
                    }
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

            return await FulfillOrder(quantity, type, user);
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

        private bool CanPerformOrder(long quantity, string type, long limitPrice, User user)
        {
            switch (type)
            {
                case "BUY":
                    {
                        return user.UsdBalance >= limitPrice * quantity;
                    }
                case "SELL":
                    {
                        return user.BtcBalance >= quantity;
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
            switch (type)
            {
                case "BUY":
                    {
                        order.Buying = true;
                        break;
                    }
                case "SELL":
                    {
                        order.Buying = false;
                        break;
                    }
                default:
                    {
                        throw new InvalidOperationException("Invalid type (BUY or SELL accepted): " + type);
                    }
            }
            order.Status = CanPerformOrder(quantity, type, limitPrice, user) ? "LIVE" : "CANCELED";
            order.UserId = user.Id;
            order.LimitPrice = limitPrice;
            order.NotifyUrl = webhookUrl;

            order.FilledQuantity = 0L;
            order.RemainQuantity = quantity;
            order.AvgPrice = 0L;

            // try to fulfill the order                       
            if (order.Status == "LIVE")
            {
                MarketOrderReturn mor = await FulfillOrder(quantity, type, user, limitPrice);
                order.FilledQuantity += mor.quantity;
                order.RemainQuantity -= mor.quantity;
                order.AvgPrice = mor.avgPrice;
                if (order.RemainQuantity == 0) order.Status = "FULFILLED";

                // subtract the resources from the user
                if (order.Buying)
                {
                    user.BtcBalance += mor.quantity;
                    user.UsdBalance -= quantity * limitPrice;
                }
                else
                {
                    user.BtcBalance -= quantity;
                    user.UsdBalance += (long)(mor.quantity * mor.avgPrice);
                }
            }

            // put the changes in the db:
            _context.Entry(user).State = EntityState.Modified;
            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

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

            // search for order
            var order = await _context.Orders.FindAsync(id);
            if (order == null)
            {
                return NotFound();
            }
            if (order.UserId != userAuth.Id) return Unauthorized($"Order {id} does not belong to the Authorized User");

            // return allocated resources to user
            User? user = await _context.Users.FindAsync(order.UserId);
            if (user == null) throw new InvalidOperationException($"Didn't find user {order.UserId} for StandingOrder {order.Id}");

            if (order.Buying && order.RemainQuantity > 0 && order.Status == "LIVE")
            {
                user.UsdBalance += order.RemainQuantity * order.LimitPrice;
            }
            if (!order.Buying && order.RemainQuantity > 0 && order.Status == "LIVE")
            {
                user.BtcBalance += order.RemainQuantity;
            }

            // update database
            _context.Orders.Remove(order);
            _context.Entry(user).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool OrderExists(int id)
        {
            return _context.Orders.Any(e => e.Id == id);
        }
    }
}
