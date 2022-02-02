using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

using BTCexchange.Models;

namespace BTCexchange.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly BTCexchangeContext _context;

        public static string RandomToken(int length)
        {
            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length).Select(s => s[RandomNumberGenerator.GetInt32(s.Length)]).ToArray());
        }

        public UsersController(BTCexchangeContext context)
        {
            _context = context;
        }

        // GET: api/Users
        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserDTO>>> GetUsers()
        {
            return await _context.Users.Select(u => u.ToDTO()).ToListAsync();
        }

        // GET: api/Users/5
        [HttpGet("{id}")]
        public async Task<ActionResult<UserDTO>> GetUser(int id)
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            return user.ToDTO();
        }

        // POST: api/Users
        [HttpPost]
        public async Task<ActionResult<UserDTO>> PostUser(string name)
        {
            User user = new User();
            user.Name = name;
            user.Token = RandomToken(20);
            user.UsdBalance = 0L;
            user.BtcBalance = 0L;

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return user.ToDTO();
        }

        // DELETE: api/Users/5
        /*[HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return NoContent();
        }*/

    }

    [Route("api/[controller]")]
    [ApiController]
    public class BalanceController : ControllerBase
    {

        private readonly BTCexchangeContext _context;

        public BalanceController(BTCexchangeContext context)
        {
            _context = context;
        }

        // GET: api/Balance
        [HttpGet]
        public async Task<ActionResult<BalanceDTO>> GetBalance()
        {
            // get header:
            string? token = BTCexchangeContext.GetToken(Request.Headers);

            // search for header in database
            if (token == null) return Unauthorized();
            User? user = await _context.FindUserByToken(token);
            if (user == null) return NotFound();

            // return User's balance
            return await user.ToBalance();
        }

        // POST: api/Balance
        [HttpPost]
        public async Task<ActionResult<bool>> PostBalance(long balance, string currency)
        {
            // get header:
            string? token = BTCexchangeContext.GetToken(Request.Headers);

            // search for header in database
            if (token == null) return Unauthorized();
            User? user = await _context.FindUserByToken(token);
            if (user == null) return NotFound();

            // try to update the user
            switch (currency)
            {
                case "BTC":
                    {
                        lock (OrdersController.orderLock)
                        {
                            if (user.BtcBalance + balance >= 0)
                            {
                                user.BtcBalance += balance;
                                _context.Entry(user).State = EntityState.Modified;
                                _context.SaveChanges();
                                return true;
                            }
                            else
                            {
                                return false;
                            }
                        }
                    }
                case "USD":
                    {
                        lock (OrdersController.orderLock)
                        {
                            if (user.UsdBalance + balance >= 0)
                            {
                                user.UsdBalance += balance;
                                _context.Entry(user).State = EntityState.Modified;
                                _context.SaveChanges();
                                return true;
                            }
                            else
                            {
                                return false;
                            }
                        }
                    }
                default:
                    {
                        throw new InvalidOperationException("Invalid currency symbol (BTC and USD accepted): " + currency);
                    }
            }
        }
    }
}
