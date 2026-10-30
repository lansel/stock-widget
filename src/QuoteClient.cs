using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

public static class QuoteClient
{
    private static HttpClient _client;
    private static readonly object _lock = new object();

    public static string BuildUrl(IEnumerable<string> symbols)
    {
        return "https://qt.gtimg.cn/q=" + string.Join(",", symbols);
    }

    public static string DecodeGbk(byte[] bytes)
    {
        return Encoding.GetEncoding(936).GetString(bytes);
    }

    public static string FetchRaw(IEnumerable<string> symbols)
    {
        HttpClient client = GetClient();
        string url = BuildUrl(symbols);
        byte[] bytes = client.GetByteArrayAsync(url).Result;
        return DecodeGbk(bytes);
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
