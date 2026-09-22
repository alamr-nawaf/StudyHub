using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StudyHub.Application.Auth.Commands.Logout;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Application.DependencyInjection;

namespace StudyHub.Application.Tests.Common.Behaviors;

// Through the real container rather than by calling the behaviour directly: the container
// silently skips any behaviour whose generic constraint does not match, so a test that calls
// the handler alone stays green while the validator never runs at all
public class ValidationBehaviorTests
{
    [Fact]
    public async Task Send_InvalidCommandWithoutResponse_ShouldThrowValidationException()
    {
        var refreshTokens = new Mock<IRefreshTokenRepository>();

        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddApplicationServices();
        services.AddSingleton(refreshTokens.Object);
        services.AddSingleton(Mock.Of<ICurrentUserService>());
        services.AddSingleton(Mock.Of<ITokenService>());
        services.AddSingleton(Mock.Of<IUnitOfWork>());

        await using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var act = () => mediator.Send(new LogoutCommand(""));

        await act.Should().ThrowAsync<ValidationException>();
        refreshTokens.Verify(r => r.GetByHashAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
