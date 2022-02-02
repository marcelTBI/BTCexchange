using Microsoft.EntityFrameworkCore;

using BTCexchange.Models;
using BTCexchange.src;

namespace BTCexchange
{
    public class Program
    {

        public static void Main(string[] args)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c => c.OperationFilter<AuthorizationHeaderFilter>());

            // Add connection to database as DBContext
            IConfiguration config = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
            builder.Services.AddDbContext<BTCexchangeContext>(options => options.UseNpgsql(config.GetConnectionString("DefaultDatabase")));

            WebApplication app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();
            app.UseAuthorization();
            app.MapControllers();
            app.Run();
        }
    }
}
