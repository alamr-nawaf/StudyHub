namespace StudyHub.Application.Common.Interfaces;

// الخام والهاش والصلاحية من نداء واحد — فلا يمكن أن يُحفظ هاش لا يخصّ الخام المُعاد
public sealed record RefreshTokenResult(string RawToken, string TokenHash, DateTime ExpiresAt);