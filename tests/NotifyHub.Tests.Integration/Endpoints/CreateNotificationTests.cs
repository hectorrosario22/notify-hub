using System.Net;
using System.Net.Http.Json;
using NotifyHub.Contracts.Requests;
using NotifyHub.Contracts.Responses;
using NotifyHub.Tests.Integration.Fixtures;

namespace NotifyHub.Tests.Integration.Endpoints;

[Collection(IntegrationTestCollection.Name)]
public class CreateNotificationTests(NotifyHubApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Post_ValidRequest_Returns202WithResponse()
    {
        var request = new CreateNotificationRequest(
            Guid.NewGuid(),
            "Test Title",
            "Test Body",
            new Dictionary<string, string> { { "push", "device-token-123" } });

        var response = await _client.PostAsJsonAsync("/notifications", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var notification = await response.Content.ReadFromJsonAsync<NotificationResponse>();
        Assert.NotNull(notification);
        Assert.Equal(request.Title, notification.Title);
        Assert.Equal(request.Body, notification.Body);
        Assert.Equal(request.RecipientUserId, notification.RecipientUserId);
        Assert.Single(notification.Deliveries);
        Assert.Equal("push", notification.Deliveries[0].Channel);
    }

    [Fact]
    public async Task Post_InvalidRequest_Returns400()
    {
        var request = new CreateNotificationRequest(
            Guid.Empty,
            "",
            "",
            new Dictionary<string, string>());

        var response = await _client.PostAsJsonAsync("/notifications", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_CreatedNotification_CanBeRetrievedById()
    {
        var request = new CreateNotificationRequest(
            Guid.NewGuid(),
            "Persisted Title",
            "Persisted Body",
            new Dictionary<string, string> { { "push", "device-token" } });

        var createResponse = await _client.PostAsJsonAsync("/notifications", request);
        var created = await createResponse.Content.ReadFromJsonAsync<NotificationResponse>();

        var getResponse = await _client.GetAsync($"/notifications/{created!.Id}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = await getResponse.Content.ReadFromJsonAsync<NotificationResponse>();
        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal("Persisted Title", fetched.Title);
    }

    [Fact]
    public async Task Post_PushDelivery_MarkedAsSent()
    {
        var request = new CreateNotificationRequest(
            Guid.NewGuid(),
            "Push Test",
            "Push Body",
            new Dictionary<string, string> { { "push", "device-token" } });

        var response = await _client.PostAsJsonAsync("/notifications", request);
        var notification = await response.Content.ReadFromJsonAsync<NotificationResponse>();

        Assert.Equal("sent", notification!.Deliveries[0].Status);
    }
}
