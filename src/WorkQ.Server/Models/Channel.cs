namespace WorkQ.Server.Models;

public enum ChannelKind
{
    Direct,
    Group
}

public sealed class Channel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public ChannelKind Kind { get; set; }
    public long OwnerId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<ChannelMember> Members { get; } = [];
    public List<Message> Messages { get; } = [];
}
