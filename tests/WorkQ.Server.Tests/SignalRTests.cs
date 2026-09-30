using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using WorkQ.Server.Dtos;

namespace WorkQ.Server.Tests;

public sealed class SignalRTests
{
    [Fact]
    public async Task Hub_DeliversMessagesToChannelMembers()
    {
        using var factory = new WorkQApiFactory();
        var clientA = factory.CreateClient();
        var clientB = factory.CreateClient();
        var userA = await ApiTestHelper.RegisterAsync(clientA, "alice");
        var userB = await ApiTestHelper.RegisterAsync(clientB, "bob");
        var channel = await ApiTestHelper.CreateDirectChannelAsync(clientA, userB.User.Id);

        var received = new TaskCompletionSource<MessageDto>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var connectionB = NewConnection(factory, userB.Token);
        connectionB.On<MessageDto>("ReceiveMessage", message => received.TrySetResult(message));
        await connectionB.StartAsync();
        await connectionB.InvokeAsync("JoinChannel", channel.Id);

        var connectionA = NewConnection(factory, userA.Token);
        await connectionA.StartAsync();
        await connectionA.InvokeAsync("JoinChannel", channel.Id);

        var clientId = Guid.NewGuid().ToString();
        var result = await connectionA.InvokeAsync<SendMessageResultDto>(
            "SendMessage",
            channel.Id,
            "hello via hub",
            clientId);

        var delivered = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal("hello via hub", delivered.Content);
        Assert.Equal(userA.User.Id, delivered.SenderId);
        Assert.Equal(channel.Id, delivered.ChannelId);
        Assert.Equal(clientId, delivered.ClientId);
        Assert.True(result.Message.Id > 0);

        await connectionA.DisposeAsync();
        await connectionB.DisposeAsync();
    }

    [Fact]
    public async Task Hub_BroadcastsPresenceToChannelPeers()
    {
        using var factory = new WorkQApiFactory();
        var clientA = factory.CreateClient();
        var clientB = factory.CreateClient();
        var userA = await ApiTestHelper.RegisterAsync(clientA, "alice");
        var userB = await ApiTestHelper.RegisterAsync(clientB, "bob");
        var channel = await ApiTestHelper.CreateDirectChannelAsync(clientA, userB.User.Id);

        var online = new TaskCompletionSource<UserPresenceDto>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var offline = new TaskCompletionSource<UserPresenceDto>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var connectionA = NewConnection(factory, userA.Token);
        connectionA.On<UserPresenceDto>("UserPresenceChanged", presence =>
        {
            if (presence.IsOnline)
            {
                online.TrySetResult(presence);
            }
            else
            {
                offline.TrySetResult(presence);
            }
        });
        await connectionA.StartAsync();
        await connectionA.InvokeAsync("JoinChannel", channel.Id);

        var connectionB = NewConnection(factory, userB.Token);
        await connectionB.StartAsync();

        var onlinePresence = await online.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(userB.User.Id, onlinePresence.UserId);
        Assert.True(onlinePresence.IsOnline);

        await connectionB.StopAsync();
        var offlinePresence = await offline.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(userB.User.Id, offlinePresence.UserId);
        Assert.False(offlinePresence.IsOnline);

        await connectionB.DisposeAsync();
        await connectionA.DisposeAsync();
    }

    private static HubConnection NewConnection(WorkQApiFactory factory, string token)
    {
        var url = new Uri(factory.Server.BaseAddress, "/hubs/chat");
        return new HubConnectionBuilder()
            .WithUrl(url, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.ServerSentEvents;
            })
            .Build();
    }
}
