using System;
using System.Net.Http;

public static class StockSearchClient
{
    private static HttpClient _client;
    private static readonly object _lock = new object();

    public static string BuildUrl(string query)
    {
        return "https://smartbox.gtimg.cn/s3/?v=2&q=" +
            Uri.EscapeDataString(query ?? "") + "&t=all";
    }

    public static string FetchRaw(string query)
    {
        HttpClient client = GetClient();
        byte[] bytes = client.GetByteArrayAsync(BuildUrl(query)).Result;
        return QuoteClient.DecodeGbk(bytes);
    }

    private static HttpClient GetClient()
    {
        if (_client == null)
        {
            lock (_lock)
            {
                if (_client == null)
                {
                    var c = new HttpClient();
                    c.Timeout = TimeSpan.FromMilliseconds(3000);
                    _client = c;
                }
            }
        }
        return _client;
    }
}
