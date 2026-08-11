using Microsoft.EntityFrameworkCore;
using LegacyApi.Data;
using LegacyApi.Services.Tax;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=orders.db"));

builder.Services.AddTransient<ITaxStrategy, UsTaxStrategy>();
builder.Services.AddTransient<ITaxStrategy, UkTaxStrategy>();
builder.Services.AddTransient<ITaxStrategy, DefaultTaxStrategy>();
builder.Services.AddSingleton<ITaxStrategyProvider, TaxStrategyProvider>();

var app = builder.Build();

app.MapControllers();

app.Run();