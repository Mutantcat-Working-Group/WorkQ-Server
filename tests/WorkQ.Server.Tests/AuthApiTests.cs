using System.Net;
using System.Net.Http.Json;
using WorkQ.Server.Dtos;

namespace WorkQ.Server.Tests;

public sealed class AuthApiTests
{
    [Fact]
    public async Task Register_Login_And_Me_Work()
    {
        using var factory = new WorkQApiFactory();
        var client = factory.CreateClient();

        var registered = await ApiTestHelper.RegisterAsync(client, "Alice");

        Assert.NotEmpty(registered.Token);
        Assert.Equal("alice", registered.User.Username);
        Assert.Equal("Alice", registered.User.DisplayName);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = "ALICE", password = "Password123!" });
        loginResponse.EnsureSuccessStatusCode();
        var login = await ApiTestHelper.ReadJsonAsync<AuthResponse>(loginResponse);

        Assert.NotNull(login);
        Assert.NotEmpty(login.Token);

        ApiTestHelper.SetAuth(client, login.Token);
        var meResponse = await client.GetAsync("/api/users/me");
        meResponse.EnsureSuccessStatusCode();
        var me = await ApiTestHelper.ReadJsonAsync<UserDto>(meResponse);

        Assert.Equal(registered.User.Id, me!.Id);
    }

    [Fact]
    public async Task ProtectedEndpoints_RejectMissingToken()
    {
        using var factory = new WorkQApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DuplicateUsername_And_WrongPassword_AreRejected()
    {
        using var factory = new WorkQApiFactory();
        var client = factory.CreateClient();
        await ApiTestHelper.RegisterAsync(client, "duplicate");

        var duplicateResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { username = "DUPLICATE", password = "Password123!" });
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        var wrongLogin = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = "duplicate", password = "WrongPassword!" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrongLogin.StatusCode);
    }
}
