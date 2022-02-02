using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using BTCexchange.Controllers;
using BTCexchange.Models;

namespace BTCexchangeTests
{
    [TestClass]
    public class UnitTests
    {
        public class TestContext : BTCexchangeContext
        {
            public TestContext(DbContextOptions<BTCexchangeContext> options) : base(options)
            {
            }
            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                optionsBuilder.UseInMemoryDatabase("BTCexchange");
            }
        }

        public class TestControllers
        {
            readonly HttpContext httpContext = new DefaultHttpContext();
            readonly UsersController userController;
            readonly OrdersController ordersController;
            readonly BalanceController balanceController;

            public TestControllers()
            {
                DbContextOptions<BTCexchangeContext> options = new DbContextOptionsBuilder<BTCexchangeContext>().UseInMemoryDatabase("BTCexchange").Options;
                TestContext context = new TestContext(options);
                userController = new UsersController(context)
                {
                    ControllerContext = new ControllerContext()
                    {
                        HttpContext = httpContext
                    }
                };
                ordersController = new OrdersController(context)
                {
                    ControllerContext = new ControllerContext()
                    {
                        HttpContext = httpContext
                    }
                };
                balanceController = new BalanceController(context)
                {
                    ControllerContext = new ControllerContext()
                    {
                        HttpContext = httpContext
                    }
                };
            }

            public UsersController GetUserController()
            {
                return userController;
            }

            public OrdersController GetOrdersController()
            {
                return ordersController;
            }
            public HttpContext GetHttpContext()
            {
                return httpContext;
            }
            public BalanceController GetBalanceController()
            {
                return balanceController;
            }
        }

        [TestMethod]
        public async Task TestBalance()
        {
            // get TestControllers class
            TestControllers tcs = new TestControllers();

            // register an User and check if it is there
            ActionResult<UserDTO> testUserAR = await tcs.GetUserController().PostUser("TestUser");
            UserDTO? testUser = testUserAR.Value;
            Assert.IsNotNull(testUser);
            Assert.AreEqual("TestUser", testUser.Name);

            // post some balance for that user and check if it is there
            tcs.GetHttpContext().Request.Headers["token"] = testUser.Token;
            Assert.IsTrue((await tcs.GetBalanceController().PostBalance(5L, "BTC")).Value);
            Assert.IsTrue((await tcs.GetBalanceController().PostBalance(250000L, "USD")).Value);
            ActionResult<BalanceDTO> balanceAR = await tcs.GetBalanceController().GetBalance();
            Assert.IsNotNull(balanceAR);
            BalanceDTO? balance = balanceAR.Value;
            Assert.IsNotNull(balance);
            Assert.AreEqual(balance.BtcBalance, 5L);
            Assert.AreEqual(balance.UsdBalance, 250000L);
            Assert.IsTrue(balance.UsdEquivalent > 0L);
        }

        [TestMethod]
        public async Task TestOrders()
        {
            // get TestControllers class
            TestControllers tcs = new TestControllers();

            // register users A, B, C, D
            UserDTO? testUserA = (await tcs.GetUserController().PostUser("A")).Value;
            UserDTO? testUserB = (await tcs.GetUserController().PostUser("B")).Value;
            UserDTO? testUserC = (await tcs.GetUserController().PostUser("C")).Value;
            UserDTO? testUserD = (await tcs.GetUserController().PostUser("D")).Value;
            Assert.IsNotNull(testUserA);
            Assert.IsNotNull(testUserB);
            Assert.IsNotNull(testUserC);
            Assert.IsNotNull(testUserD);

            // deposit $$$
            tcs.GetHttpContext().Request.Headers["token"] = testUserA.Token;
            Assert.IsTrue((await tcs.GetBalanceController().PostBalance(1L, "BTC")).Value);
            tcs.GetHttpContext().Request.Headers["token"] = testUserB.Token;
            Assert.IsTrue((await tcs.GetBalanceController().PostBalance(10L, "BTC")).Value);
            tcs.GetHttpContext().Request.Headers["token"] = testUserC.Token;
            Assert.IsTrue((await tcs.GetBalanceController().PostBalance(250000L, "USD")).Value);
            tcs.GetHttpContext().Request.Headers["token"] = testUserD.Token;
            Assert.IsTrue((await tcs.GetBalanceController().PostBalance(300000L, "USD")).Value);

            // create standing order for A
            {
                tcs.GetHttpContext().Request.Headers["token"] = testUserA.Token;
                int? orderId = (await tcs.GetOrdersController().PostOrder(10L, "SELL", 10000L, null)).Value;
                Assert.IsNotNull(orderId);
                OrderDTO? order = (await tcs.GetOrdersController().GetOrder(orderId.Value)).Value;
                Assert.IsNotNull(order);
                Assert.IsTrue(order.Status == "CANCELED");
            }

            // deposit some BTC
            tcs.GetHttpContext().Request.Headers["token"] = testUserA.Token;
            Assert.IsTrue((await tcs.GetBalanceController().PostBalance(9L, "BTC")).Value);

            // create the standing order again
            tcs.GetHttpContext().Request.Headers["token"] = testUserA.Token;
            int? orderIdA = (await tcs.GetOrdersController().PostOrder(10L, "SELL", 10000L, null)).Value;
            Assert.IsNotNull(orderIdA);
            OrderDTO? orderA = (await tcs.GetOrdersController().GetOrder(orderIdA.Value)).Value;
            Assert.IsNotNull(orderA);
            Assert.IsTrue(orderA.Status == "LIVE");

            // create standing order for B
            tcs.GetHttpContext().Request.Headers["token"] = testUserB.Token;
            int? orderIdB = (await tcs.GetOrdersController().PostOrder(10L, "SELL", 20000L, null)).Value;
            Assert.IsNotNull(orderIdB);
            OrderDTO? orderB = (await tcs.GetOrdersController().GetOrder(orderIdB.Value)).Value;
            Assert.IsNotNull(orderB);
            Assert.IsTrue(orderB.Status == "LIVE");

            // create market order for C
            {
                tcs.GetHttpContext().Request.Headers["token"] = testUserC.Token;
                MarketOrderReturn? mor = (await tcs.GetOrdersController().MarketOrder(15L, "BUY")).Value;
                Assert.IsNotNull(mor);
                Assert.IsTrue(mor.Quantity == 15L);
                Assert.IsTrue(mor.AvgPrice == 200000L / (double)15L);

                tcs.GetHttpContext().Request.Headers["token"] = testUserA.Token;
                OrderDTO? orderA2 = (await tcs.GetOrdersController().GetOrder(orderIdA.Value)).Value;
                Assert.IsNotNull(orderA2);
                Assert.IsTrue(orderA2.Status == "FULFILLED");
                Assert.IsTrue(orderA2.RemainQuantity == 0L);
                Assert.IsTrue(orderA2.AvgPrice == 10000.0);

                tcs.GetHttpContext().Request.Headers["token"] = testUserB.Token;
                OrderDTO? orderB2 = (await tcs.GetOrdersController().GetOrder(orderIdB.Value)).Value;
                Assert.IsNotNull(orderB2);
                Assert.IsTrue(orderB2.Status == "LIVE");
                Assert.IsTrue(orderB2.RemainQuantity == 5L);
                Assert.IsTrue(orderB2.AvgPrice == 20000.0);

                tcs.GetHttpContext().Request.Headers["token"] = testUserC.Token;
                UserDTO? userC = (await tcs.GetUserController().GetUser(testUserC.Id)).Value;
                Assert.IsNotNull(userC);
                Assert.IsTrue(userC.BtcBalance == 15L);
                Assert.IsTrue(userC.UsdBalance == 50000L);
            }

            // create standing order for D
            tcs.GetHttpContext().Request.Headers["token"] = testUserD.Token;
            int? orderIdDFirst = (await tcs.GetOrdersController().PostOrder(10L, "BUY", 10000L, null)).Value;
            Assert.IsNotNull(orderIdDFirst);
            OrderDTO? orderDFirst = (await tcs.GetOrdersController().GetOrder(orderIdDFirst.Value)).Value;
            Assert.IsNotNull(orderDFirst);
            Assert.IsTrue(orderDFirst.Status == "LIVE");

            // create second (not enough money)
            {
                int? orderIdD = (await tcs.GetOrdersController().PostOrder(10L, "BUY", 25000L, null)).Value;
                OrderDTO? orderD = (await tcs.GetOrdersController().GetOrder(orderIdD.Value)).Value;
                Assert.IsNotNull(orderD);
                Assert.IsTrue(orderD.Status == "CANCELED");
            }

            // delete the first order
            await tcs.GetOrdersController().DeleteOrder(orderIdDFirst.Value);

            // create second again
            {
                int? orderIdD = (await tcs.GetOrdersController().PostOrder(10L, "BUY", 25000L, null)).Value;
                OrderDTO? orderD = (await tcs.GetOrdersController().GetOrder(orderIdD.Value)).Value;
                Assert.IsNotNull(orderD);
                Assert.IsTrue(orderD.Status == "LIVE");
                Assert.IsTrue(orderD.AvgPrice == 20000.0);
                Assert.IsTrue(orderD.FilledQuantity == 5L);
            }

            // check if the B's order is updated
            tcs.GetHttpContext().Request.Headers["token"] = testUserB.Token;
            OrderDTO? orderB3 = (await tcs.GetOrdersController().GetOrder(orderIdB.Value)).Value;
            Assert.IsNotNull(orderB3);
            Assert.IsTrue(orderB3.Status == "FULFILLED");
            Assert.IsTrue(orderB3.AvgPrice == 20000.0);
        }
    }
}