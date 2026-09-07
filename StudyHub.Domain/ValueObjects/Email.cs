namespace StudyHub.Domain.ValueObjects;

public sealed record Email
{
    public string Value { get; }

    // خاص: لا سبيل لإنشاء إيميل إلا عبر Create
    private Email(string value) => Value = value;

    public static Email Create(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("Email cannot be empty.");

        var normalized = input.Trim().ToLowerInvariant();

        // فحص أدنى فقط — التحقق الكامل من الشكل مسؤولية المدقّق في طبقة التطبيق
        if (!normalized.Contains('@'))
            throw new ArgumentException("Email format is invalid.");

        return new Email(normalized);
    }

    public override string ToString() => Value;
}