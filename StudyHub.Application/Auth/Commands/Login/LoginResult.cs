namespace StudyHub.Application.Auth.Commands.Login;

// شكل رد الدخول والتجديد معًا (§8). ExpiresIn بالثواني لا بالدقائق
public record LoginResult(string AccessToken, string RefreshToken, string TokenType, int ExpiresIn);