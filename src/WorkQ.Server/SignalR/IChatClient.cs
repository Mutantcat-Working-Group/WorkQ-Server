using WorkQ.Server.Dtos;

namespace WorkQ.Server.SignalR;

public interface IChatClient
{
    Task ReceiveMessage(MessageDto message);
    Task MessageSent(SendMessageResultDto result);
    Task JoinedChannel(Guid channelId);
    Task LeftChannel(Guid channelId);
    Task UserTyping(UserTypingDto typing);
    Task UserPresenceChanged(UserPresenceDto presence);
}
