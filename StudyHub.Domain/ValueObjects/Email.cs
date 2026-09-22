namespace StudyHub.Domain.ValueObjects;

/// <summary>
/// An e-mail address that is normalized and checked in one place. It exists because that
/// logic was once written twice, and a divergence between the two copies would let a
/// duplicate account walk past a unique index.
/// </summary>
public sealed record Email
{
    public string Value { get; }

    // Private: the only way to build an Email is through Create
    private Email(string value) => Value = value;

    public static Email Create(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("Email cannot be empty.");

        var normalized = input.Trim().ToLowerInvariant();

        // A minimal check only: full format validation is the Application validator's job,
        // and this type's promise is normalization, not RFC compliance
        if (!normalized.Contains('@'))
            throw new ArgumentException("Email format is invalid.");

        return new Email(normalized);
    }
    public static Email FromPersisted(string value) => new(value);

    public override string ToString() => Value;
}