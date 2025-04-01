using System.Collections.Generic;
using System.Net.Http;
using Xunit;

namespace Dapr.Client.Test.Extensions;

public class HttpExtensionTest
{
    [Fact]
    public void AddQueryParameters_ReturnsEmptyQueryStringWithNullParameters()
    {
        const string uri = "https://localhost/mypath";
        var httpRq = new HttpRequestMessage(HttpMethod.Get, uri);
        var updatedUri = httpRq.RequestUri.AddQueryParameters(null);
        Assert.Equal(uri, updatedUri.AbsoluteUri);
    }

    [Fact]
    public void AddQueryParameters_ReturnsOriginalQueryStringWithNullParameters()
    {
        const string uri = "https://localhost/mypath?a=0&b=1";
        var httpRq = new HttpRequestMessage(HttpMethod.Get, uri);
        var updatedUri = httpRq.RequestUri.AddQueryParameters(null);
        Assert.Equal(uri, updatedUri.AbsoluteUri);
    }

    [Fact]
    public void AddQueryParameters_BuildsQueryString()
    {
        var httpRq = new HttpRequestMessage(HttpMethod.Get, "https://localhost/mypath?a=0");
        var updatedUri = httpRq.RequestUri.AddQueryParameters(new List<KeyValuePair<string,string>>
        {
            new("test", "value")
        });
        Assert.Equal("https://localhost/mypath?a=0&test=value", updatedUri.AbsoluteUri);
    }

    [Fact]
    public void AddQueryParameters_BuildQueryStringWithDuplicateKeys()
    {
        var httpRq = new HttpRequestMessage(HttpMethod.Get, "https://localhost/mypath");
        var updatedUri = httpRq.RequestUri.AddQueryParameters(new List<KeyValuePair<string,string>>
        {
            new("test", "1"),
            new("test", "2"),
            new("test", "3")
        });
        Assert.Equal("https://localhost/mypath?test=1&test=2&test=3", updatedUri.AbsoluteUri);
    }

    [Fact]
    public void AddQueryParameters_EscapeSpacesInValues()
    {
        var httpRq = new HttpRequestMessage(HttpMethod.Get, "https://localhost/mypath");
        var updatedUri = httpRq.RequestUri.AddQueryParameters(new List<KeyValuePair<string,string>>
        {
            new("name1", "John Doe"),
            new("name2", "Jane Doe")
        });
        Assert.Equal("https://localhost/mypath?name1=John%20Doe&name2=Jane%20Doe", updatedUri.AbsoluteUri);
    }
}