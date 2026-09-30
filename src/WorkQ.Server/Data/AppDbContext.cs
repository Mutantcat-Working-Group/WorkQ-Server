using Microsoft.EntityFrameworkCore;
using WorkQ.Server.Models;

namespace WorkQ.Server.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Channel> Channels => Set<Channel>();
    public DbSet<ChannelMember> ChannelMembers => Set<ChannelMember>();
    public DbSet<Message> Messages => Set<Message>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(user =>
        {
            user.HasIndex(u => u.Username).IsUnique();
            user.Property(u => u.Username).HasMaxLength(32);
            user.Property(u => u.DisplayName).HasMaxLength(64);
            user.Property(u => u.PasswordHash).HasMaxLength(128);
            user.Property(u => u.PasswordSalt).HasMaxLength(64);
        });

        modelBuilder.Entity<Channel>(channel =>
        {
            channel.Property(c => c.Name).HasMaxLength(64);
            channel.HasIndex(c => new { c.OwnerId, c.Kind });
        });

        modelBuilder.Entity<ChannelMember>(member =>
        {
            member.HasKey(m => new { m.ChannelId, m.UserId });
            member.HasOne(m => m.Channel)
                .WithMany(c => c.Members)
                .HasForeignKey(m => m.ChannelId)
                .OnDelete(DeleteBehavior.Cascade);
            member.HasOne(m => m.User)
                .WithMany(u => u.Memberships)
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Message>(message =>
        {
            message.HasIndex(m => new { m.ChannelId, m.SentAtUtc });
            message.HasIndex(m => new { m.SenderId, m.ClientId }).IsUnique();
            message.Property(m => m.Content).HasMaxLength(4000);
            message.HasOne(m => m.Channel)
                .WithMany(c => c.Messages)
                .HasForeignKey(m => m.ChannelId)
                .OnDelete(DeleteBehavior.Cascade);
            message.HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
