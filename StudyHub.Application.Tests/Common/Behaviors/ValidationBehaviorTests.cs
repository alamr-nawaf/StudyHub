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

// عبر الحاوية الحقيقية لا باستدعاء المعالِج مباشرة: الحاوية تتخطّى بصمت أي سلوك
// لا يطابق قيده العام، فاختبار المعالِج وحده يبقى أخضر والمدقّق لا يعمل أبدًا
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
