namespace StudyHub.Application.Common.Interfaces;

// التوكن ولحظة انتهائه معًا — منها يُحسب expiresIn في رد الدخول والتجديد (المتطلبات §8)
public sealed record AccessToken(string Value, DateTime ExpiresAt);