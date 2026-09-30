namespace WorkQ.Server.Models;

public sealed class User
{
    public long Id { get; set; }
    public required string Username { get; set; }
    public required string DisplayName { get; set; }
    public required string PasswordHash { get; set; }
    public required string PasswordSalt { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<ChannelMember> Memberships { get; } = [];
}
