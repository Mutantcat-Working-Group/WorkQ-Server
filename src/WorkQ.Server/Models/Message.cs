namespace WorkQ.Server.Models;

public sealed class Message
{
    public long Id { get; set; }
    public Guid ChannelId { get; set; }
    public long SenderId { get; set; }
    public required string Content { get; set; }
    public string? ClientId { get; set; }
    public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;

    public Channel? Channel { get; set; }
    public User? Sender { get; set; }
}
