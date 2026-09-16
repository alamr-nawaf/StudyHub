using MediatR;
using StudyHub.Application.Auth.Commands.Login;

namespace StudyHub.Application.Auth.Commands.Refresh;

// مجهول الهوية: التوكن نفسه هو الاعتماد، ولا ترويسة Authorization هنا
public record RefreshTokenCommand(string RefreshToken) : IRequest<LoginResult>;