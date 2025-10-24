using Dataisland.Organizations;
using Dataisland.Organizations.Users;

namespace Dataisland.Account;

[Serializable]
public class Account
{
    public UserId UserId { get; set; }
    
    public string Type { get; set; } = null!;
    public string Provider { get; set; } = null!;
    public string ProviderAccountId { get; set; } = null!;
    public string? RefreshToken { get; set; }
    public string? AccessToken { get; set; }
    public int? ExpiresAt { get; set; }
    public string? TokenType { get; set; }
    public string? Scope { get; set; }
    public string? IdToken { get; set; }
    public string? SessionState { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}