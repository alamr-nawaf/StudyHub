using MediatR;
using StudyHub.Application.DependencyInjection;
using StudyHub.Infrastructure.DependencyInjection;
using StudyHub.Application.Users.Commands.RegisterUser;


var builder = WebApplication.CreateBuilder(args);
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
app.MapPost("/api/auth/register", async (RegisterUserCommand command, IMediator mediator) =>
{
    var userId = await mediator.Send(command);
    return Results.Created($"/api/users/{userId}", new { userId });
});
app.UseHttpsRedirection();

app.Run();