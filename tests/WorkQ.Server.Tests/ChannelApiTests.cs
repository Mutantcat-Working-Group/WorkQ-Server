using System.Net;
using System.Net.Http.Json;
using WorkQ.Server.Dtos;

namespace WorkQ.Server.Tests;

public sealed class ChannelApiTests
{
    [Fact]
    public async Task DirectChannel_IsReused_AndTracksReadState()
    {
        using var factory = new WorkQApiFactory();
        var clientA = factory.CreateClient();
        var clientB = factory.CreateClient();
        var userA = await ApiTestHelper.RegisterAsync(clientA, "alice");
        var userB = await ApiTestHelper.RegisterAsync(clientB, "bob");

        var direct = await ApiTestHelper.CreateDirectChannelAsync(clientA, userB.User.Id);
        Assert.Equal(2, direct.Members.Count);

        var reversed = await ApiTestHelper.CreateDirectChannelAsync(clientB, userA.User.Id);
        Assert.Equal(direct.Id, reversed.Id);

        var clientId = Guid.NewGuid().ToString();
        var sent = await ApiTestHelper.SendMessageAsync(
            clientB,
            direct.Id,
            "hi alice",
            clientId);
        Assert.False(sent.Duplicate);

        var duplicate = await ApiTestHelper.SendMessageAsync(
            clientB,
            direct.Id,
            "hi alice",
            clientId);
        Assert.True(duplicate.Duplicate);
        Assert.Equal(sent.Message.Id, duplicate.Message.Id);

        var historyResponse = await clientA.GetAsync($"/api/channels/{direct.Id}/messages");
        historyResponse.EnsureSuccessStatusCode();
        var history = await ApiTestHelper.ReadJsonAsync<MessagePageDto>(historyResponse);
        Assert.Single(history!.Messages);
        Assert.Equal("hi alice", history.Messages[0].Content);

        var listResponse = await clientA.GetAsync("/api/channels");
        listResponse.EnsureSuccessStatusCode();
        var summaries = await ApiTestHelper.ReadJsonAsync<List<ChannelSummaryDto>>(listResponse);
        var summary = Assert.Single(summaries!);
        Assert.Equal(1, summary.UnreadCount);

        var readResponse = await clientA.PostAsync($"/api/channels/{direct.Id}/read", null);
        readResponse.EnsureSuccessStatusCode();

        listResponse = await clientA.GetAsync("/api/channels");
        summaries = await ApiTestHelper.ReadJsonAsync<List<ChannelSummaryDto>>(listResponse);
        Assert.Equal(0, Assert.Single(summaries!).UnreadCount);
    }

    [Fact]
    public async Task GroupChannel_SupportsMembers_Messages_AndCleanup()
    {
        using var factory = new WorkQApiFactory();
        var clientA = factory.CreateClient();
        var clientB = factory.CreateClient();
        var clientC = factory.CreateClient();
        var userA = await ApiTestHelper.RegisterAsync(clientA, "alice");
        var userB = await ApiTestHelper.RegisterAsync(clientB, "bob");
        var userC = await ApiTestHelper.RegisterAsync(clientC, "carol");

        var group = await ApiTestHelper.CreateGroupChannelAsync(
            clientA,
            "team-alpha",
            [userB.User.Id]);
        Assert.Equal(2, group.Members.Count);

        var addResponse = await clientA.PostAsJsonAsync(
            $"/api/channels/{group.Id}/members",
            new { userIds = new[] { userC.User.Id } });
        addResponse.EnsureSuccessStatusCode();
        var updated = await ApiTestHelper.ReadJsonAsync<ChannelDetailDto>(addResponse);

        Assert.Equal(3, updated!.Members.Count);
        Assert.Contains(updated.Members, m => m.Id == userC.User.Id);

        await ApiTestHelper.SendMessageAsync(clientA, group.Id, "welcome to the team");

        var historyResponse = await clientC.GetAsync($"/api/channels/{group.Id}/messages");
        historyResponse.EnsureSuccessStatusCode();
        var history = await ApiTestHelper.ReadJsonAsync<MessagePageDto>(historyResponse);
        Assert.Equal("welcome to the team", Assert.Single(history!.Messages).Content);

        var leaveResponse = await clientC.PostAsync($"/api/channels/{group.Id}/leave", null);
        Assert.Equal(HttpStatusCode.NoContent, leaveResponse.StatusCode);

        var deleteResponse = await clientA.DeleteAsync($"/api/channels/{group.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var goneResponse = await clientB.GetAsync($"/api/channels/{group.Id}");
        Assert.Equal(HttpStatusCode.NotFound, goneResponse.StatusCode);
    }
}
