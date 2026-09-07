using MediatR;
using StudyHub.API.Common;
using StudyHub.Application.DependencyInjection;
using StudyHub.Application.Users.Commands.RegisterUser;
using StudyHub.Infrastructure.DependencyInjection;


var builder = WebApplication.CreateBuilder(args);
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();


builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();


var app = builder.Build();

app.UseExceptionHandler();   

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