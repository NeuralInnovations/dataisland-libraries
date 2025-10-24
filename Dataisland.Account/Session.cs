using Dataisland.Organizations;
using Dataisland.Organizations.Users;

namespace Dataisland.Account;

[Serializable]
public class Session
{
    public string SessionToken { get; set; } = null!;
    public UserId UserId { get; set; }
    public DateTime Expires { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}