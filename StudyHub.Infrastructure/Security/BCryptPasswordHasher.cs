using BC = BCrypt.Net.BCrypt;
using StudyHub.Application.Common.Interfaces;

namespace StudyHub.Infrastructure.Security;

public class BCryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string Hash(string password) => BC.HashPassword(password, WorkFactor);

    public bool Verify(string password, string hash) => BC.Verify(password, hash);
}