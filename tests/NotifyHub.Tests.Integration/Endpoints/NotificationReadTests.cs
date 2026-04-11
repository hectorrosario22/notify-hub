using System.Net;
using System.Net.Http.Json;
using NotifyHub.Contracts.Requests;
using NotifyHub.Contracts.Responses;
using NotifyHub.Tests.Integration.Fixtures;

namespace NotifyHub.Tests.Integration.Endpoints;

[Collection(IntegrationTestCollection.Name)]
public class NotificationReadTests(NotifyHubApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<NotificationResponse> CreateNotificationAsync(Guid? userId = null)
    {
        var request = new CreateNotificationRequest(
            userId ?? Guid.NewGuid(),
            "Test Title",
            "Test Body",
            new Dictionary<string, string> { { "push", "device-token" } });

        var response = await _client.PostAsJsonAsync("/notifications", request);
        return (await response.Content.ReadFromJsonAsync<NotificationResponse>())!;
    }

    [Fact]
    public async Task GetList_ReturnsPaginatedNotifications()
    {
        var userId = Guid.NewGuid();
        await CreateNotificationAsync(userId);
        await CreateNotificationAsync(userId);

        var response = await _client.GetAsync($"/notifications?userId={userId}&page=1&pageSize=10&unreadOnly=false");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paged = await response.Content.ReadFromJsonAsync<PagedResponse<NotificationResponse>>();
        Assert.NotNull(paged);
        Assert.Equal(2, paged.TotalCount);
        Assert.Equal(2, paged.Items.Count);
    }

    [Fact]
    public async Task GetUnreadCount_ReturnsCorrectCount()
    {
        var userId = Guid.NewGuid();
        await CreateNotificationAsync(userId);
        await CreateNotificationAsync(userId);

        var response = await _client.GetAsync($"/notifications/unread-count?userId={userId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<UnreadCountResponse>();
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task PatchRead_MarksNotificationAsRead()
    {
        var notification = await CreateNotificationAsync();

        var patchResponse = await _client.PatchAsync($"/notifications/{notification.Id}/read", null);
        Assert.Equal(HttpStatusCode.NoContent, patchResponse.StatusCode);

        var getResponse = await _client.GetAsync($"/notifications/{notification.Id}");
        var updated = await getResponse.Content.ReadFromJsonAsync<NotificationResponse>();
        Assert.True(updated!.IsRead);
        Assert.NotNull(updated.ReadAt);
    }

    [Fact]
    public async Task PatchReadAll_MarksAllAsRead()
    {
        var userId = Guid.NewGuid();
        await CreateNotificationAsync(userId);
        await CreateNotificationAsync(userId);

        var patchResponse = await _client.PatchAsync($"/notifications/read-all?userId={userId}", null);
        Assert.Equal(HttpStatusCode.NoContent, patchResponse.StatusCode);

        var countResponse = await _client.GetAsync($"/notifications/unread-count?userId={userId}");
        var result = await countResponse.Content.ReadFromJsonAsync<UnreadCountResponse>();
        Assert.Equal(0, result!.Count);
    }

    [Fact]
    public async Task GetById_NonExistent_Returns404()
    {
        var response = await _client.GetAsync($"/notifications/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
