using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorkQ.Server.Data;

namespace WorkQ.Server.Tests;

public sealed class WorkQApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public WorkQApiFactory()
    {
        var databaseName = $"Data Source=file:workq-tests-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _connection = new SqliteConnection(databaseName);
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
