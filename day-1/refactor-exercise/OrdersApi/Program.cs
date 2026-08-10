using Microsoft.EntityFrameworkCore;
using LegacyApi.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=orders.db"));

var app = builder.Build();

app.MapControllers();

app.Run();