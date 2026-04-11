namespace NotifyHub.Tests.Integration.Fixtures;

[CollectionDefinition(Name)]
public class IntegrationTestCollection : ICollectionFixture<NotifyHubApiFactory>
{
    public const string Name = "Integration";
}
