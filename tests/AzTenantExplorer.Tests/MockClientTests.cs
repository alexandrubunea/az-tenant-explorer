using AzTenantExplorer.Core.Models;
using AzTenantExplorer.Infrastructure.Clients;
using Xunit;

namespace AzTenantExplorer.Tests;

public class MockClientTests
{
    // Point the client directly to the MockData folder in the Infrastructure project
    private readonly MockAzureClient _client = new("../../../../../src/AzTenantExplorer.Infrastructure/MockData");

    [Fact]
    public async Task GetSubscriptions_MergesCommerceAndAppliesQuotaHackCorrectly()
    {
        var subscriptions = (await _client.GetSubscriptionsAsync()).ToList();

        Assert.Equal(3, subscriptions.Count);

        // MOSP PlayGround Sub -> Got explicit MS-AZR-0063P
        var mospSub = subscriptions.First(s => s.SubscriptionGUID == "11111111-1111-1111-1111-111111111111");
        Assert.Equal("MS-AZR-0063P", mospSub.OfferId);
        Assert.Equal(BillingPlatform.MOSP, mospSub.GetBillingPlatform);
        Assert.False(mospSub.IsDevTest);

        // MCA Disabled Sub -> PayAsYouGo quota, no billing offer -> defaulted to MS-AZR-0017G
        var mcaSub = subscriptions.First(s => s.SubscriptionGUID == "22222222-2222-2222-2222-222222222222");
        Assert.Equal("MS-AZR-0017G", mcaSub.OfferId);
        Assert.Equal(BillingPlatform.MCA, mcaSub.GetBillingPlatform);
        Assert.False(mcaSub.IsDevTest);

        // MOSP DevTest Sub -> Got explicit MS-AZR-0023P
        var devTestSub = subscriptions.First(s => s.SubscriptionGUID == "33333333-3333-3333-3333-333333333333");
        Assert.Equal("MS-AZR-0023P", devTestSub.OfferId);
        Assert.Equal(BillingPlatform.MOSP, devTestSub.GetBillingPlatform);
        Assert.True(devTestSub.IsDevTest);
    }

    [Fact]
    public async Task GetBillingAccounts_ParsesMcaAndMospAccounts()
    {
        var accounts = (await _client.GetBillingAccountsAsync()).ToList();

        Assert.Equal(2, accounts.Count);
        Assert.Contains(accounts, a => a.GetBillingPlatform == BillingPlatform.MCA);
        Assert.Contains(accounts, a => a.GetBillingPlatform == BillingPlatform.MOSP);
    }
}
