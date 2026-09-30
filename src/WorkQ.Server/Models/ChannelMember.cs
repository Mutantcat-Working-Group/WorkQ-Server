namespace WorkQ.Server.Models;

public sealed class ChannelMember
{
    public Guid ChannelId { get; set; }
    public long UserId { get; set; }
    public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastReadAtUtc { get; set; }

    public Channel? Channel { get; set; }
    public User? User { get; set; }
}
