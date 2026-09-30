using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using WorkQ.Server.Dtos;

namespace WorkQ.Server.Tests;

internal static class ApiTestHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    internal static async Task<AuthResponse> RegisterAsync(
        HttpClient client,
        string username,
        string password = "Password123!")
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { username, password, displayName = username });
        response.EnsureSuccessStatusCode();

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        Assert.NotNull(auth);
        SetAuth(client, auth.Token);
        return auth;
    }

    internal static void SetAuth(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    internal static async Task<ChannelDetailDto> CreateDirectChannelAsync(
        HttpClient client,
        long otherUserId)
    {
        var response = await client.PostAsJsonAsync(
            "/api/channels",
            new { kind = "direct", memberIds = new[] { otherUserId } });
        response.EnsureSuccessStatusCode();

        var channel = await response.Content.ReadFromJsonAsync<ChannelDetailDto>(JsonOptions);
        Assert.NotNull(channel);
        return channel;
    }

    internal static async Task<ChannelDetailDto> CreateGroupChannelAsync(
        HttpClient client,
        string name,
        long[]? memberIds = null)
    {
        var response = await client.PostAsJsonAsync(
            "/api/channels",
            new { kind = "group", name, memberIds });
        response.EnsureSuccessStatusCode();

        var channel = await response.Content.ReadFromJsonAsync<ChannelDetailDto>(JsonOptions);
        Assert.NotNull(channel);
        return channel;
    }

    internal static async Task<SendMessageResultDto> SendMessageAsync(
        HttpClient client,
        Guid channelId,
        string content,
        string? clientId = null)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/channels/{channelId}/messages",
            new { content, clientId });
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<SendMessageResultDto>(JsonOptions);
        Assert.NotNull(result);
        return result;
    }

    internal static Task<T?> ReadJsonAsync<T>(HttpResponseMessage response) =>
        response.Content.ReadFromJsonAsync<T>(JsonOptions);
}
