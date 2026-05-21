namespace Learnexia.Shared.Contracts.Identity;

public interface IUserLookup
{
    Task<UserSummary?> FindByIdAsync(int userId, CancellationToken ct = default);
}

public sealed record UserSummary(int Id, string UserName, string Email);
