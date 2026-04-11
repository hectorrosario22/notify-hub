using Microsoft.EntityFrameworkCore;
using NotifyHub.Api.Endpoints;
using NotifyHub.Api.Middleware;
using NotifyHub.Infrastructure;
using NotifyHub.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider
        .GetRequiredService<NotifyHubDbContext>()
        .Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<ExceptionHandlerMiddleware>();
app.UseHttpsRedirection();
app.MapEndpoints();

app.Run();
