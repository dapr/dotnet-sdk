using Xunit;

namespace Dapr.Client.Test.TestData;

public class TryRewriteUriWithNoAppIdRewritesUriToDaprInvokeTestData : TheoryData<string, string, string>
{
    public TryRewriteUriWithNoAppIdRewritesUriToDaprInvokeTestData()
    {
        Add(null, "http://bank", "https://some.host:3499/v1.0/invoke/bank/method/");
        Add("bank", "http://bank", "https://some.host:3499/v1.0/invoke/bank/method/");
        Add("Bank", "http://bank", "https://some.host:3499/v1.0/invoke/Bank/method/");
        Add("invalid", "http://bank", "https://some.host:3499/v1.0/invoke/bank/method/");
        Add(null, "http://Bank", "https://some.host:3499/v1.0/invoke/bank/method/");
        Add("Bank", "http://Bank", "https://some.host:3499/v1.0/invoke/Bank/method/");
        Add("bank", "http://Bank", "https://some.host:3499/v1.0/invoke/bank/method/");
        Add("invalid", "http://Bank", "https://some.host:3499/v1.0/invoke/bank/method/");
        Add(null, "http://bank:3939", "https://some.host:3499/v1.0/invoke/bank/method/");
        Add("bank", "http://bank:3939", "https://some.host:3499/v1.0/invoke/bank/method/");
        Add("invalid", "http://bank:3939", "https://some.host:3499/v1.0/invoke/bank/method/");
        Add(null, "http://Bank:3939", "https://some.host:3499/v1.0/invoke/bank/method/");
        Add("Bank", "http://Bank:3939", "https://some.host:3499/v1.0/invoke/Bank/method/");
        Add("invalid", "http://Bank:3939", "https://some.host:3499/v1.0/invoke/bank/method/");
        Add(null, "http://app-id.with.dots", "https://some.host:3499/v1.0/invoke/app-id.with.dots/method/");
        Add("app-id.with.dots", "http://app-id.with.dots",
            "https://some.host:3499/v1.0/invoke/app-id.with.dots/method/");
        Add("invalid", "http://app-id.with.dots", "https://some.host:3499/v1.0/invoke/app-id.with.dots/method/");
        Add(null, "http://App-id.with.dots", "https://some.host:3499/v1.0/invoke/app-id.with.dots/method/");
        Add("App-id.with.dots", "http://App-id.with.dots",
            "https://some.host:3499/v1.0/invoke/App-id.with.dots/method/");
        Add("invalid", "http://App-id.with.dots", "https://some.host:3499/v1.0/invoke/app-id.with.dots/method/");
        Add(null, "http://bank:3939/", "https://some.host:3499/v1.0/invoke/bank/method/");
        Add("bank", "http://bank:3939/", "https://some.host:3499/v1.0/invoke/bank/method/");
        Add("invalid", "http://bank:3939/", "https://some.host:3499/v1.0/invoke/bank/method/");
        Add(null, "http://Bank:3939/", "https://some.host:3499/v1.0/invoke/bank/method/");
        Add("Bank", "http://Bank:3939/", "https://some.host:3499/v1.0/invoke/Bank/method/");
        Add("invalid", "http://Bank:3939/", "https://some.host:3499/v1.0/invoke/bank/method/");
        Add(null, "http://bank:3939/some/path", "https://some.host:3499/v1.0/invoke/bank/method/some/path");
        Add("bank", "http://bank:3939/some/path", "https://some.host:3499/v1.0/invoke/bank/method/some/path");
        Add("invalid", "http://bank:3939/some/path", "https://some.host:3499/v1.0/invoke/bank/method/some/path");
        Add(null, "http://Bank:3939/some/path", "https://some.host:3499/v1.0/invoke/bank/method/some/path");
        Add("Bank", "http://Bank:3939/some/path", "https://some.host:3499/v1.0/invoke/Bank/method/some/path");
        Add("invalid", "http://Bank:3939/some/path", "https://some.host:3499/v1.0/invoke/bank/method/some/path");
        Add(null, "http://bank:3939/some/path?q=test&p=another#fragment",
            "https://some.host:3499/v1.0/invoke/bank/method/some/path?q=test&p=another#fragment");
        Add("bank", "http://bank:3939/some/path?q=test&p=another#fragment",
            "https://some.host:3499/v1.0/invoke/bank/method/some/path?q=test&p=another#fragment");
        Add("invalid", "http://bank:3939/some/path?q=test&p=another#fragment",
            "https://some.host:3499/v1.0/invoke/bank/method/some/path?q=test&p=another#fragment");
        Add(null, "http://Bank:3939/some/path?q=test&p=another#fragment",
            "https://some.host:3499/v1.0/invoke/bank/method/some/path?q=test&p=another#fragment");
        Add("Bank", "http://Bank:3939/some/path?q=test&p=another#fragment",
            "https://some.host:3499/v1.0/invoke/Bank/method/some/path?q=test&p=another#fragment");
        Add("invalid", "http://Bank:3939/some/path?q=test&p=another#fragment",
            "https://some.host:3499/v1.0/invoke/bank/method/some/path?q=test&p=another#fragment");
    }
}
