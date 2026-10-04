using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using stock_api.Data;
using stock_api.Endpoints;
using stock_api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddOptions<OrderSubmissionOptions>()
    .Bind(builder.Configuration.GetSection(OrderSubmissionOptions.SectionName))
    .Validate(
        options => options.DuplicateWindowSeconds >= 0,
        "OrderSubmission:DuplicateWindowSeconds must be zero or greater.");
builder.Services.AddScoped<OrderSubmissionService>();

var connectionString = builder.Configuration.GetConnectionString("OrdersDatabase")
    ?? throw new InvalidOperationException(
        "Connection string 'OrdersDatabase' is required. Set it in appsettings.Development.json or the ConnectionStrings__OrdersDatabase environment variable.");
builder.Services.AddDbContext<OrderDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp", policy =>
        policy.WithOrigins("http://localhost:4200")
            .AllowAnyMethod()
            .AllowAnyHeader());
});

var app = builder.Build();

_ = app.Services.GetRequiredService<IOptions<OrderSubmissionOptions>>().Value;

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("AllowAngularApp");

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    await db.Database.MigrateAsync();
}

CustomerEndpoints.Map(app);
OrderEndpoints.Map(app);

app.Run();

public partial class Program { }
