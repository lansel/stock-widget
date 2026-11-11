using System;
using System.Net;
using System.Net.Http;
using System.Text;

public static class ChartClient
{
    private static HttpClient _client;
    private static readonly object _lock = new object();

    static ChartClient()
    {
        try
        {
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
        }
        catch { }
    }

    public static string BuildMinuteUrl(string symbol)
    {
        return "https://ifzq.gtimg.cn/appstock/app/minute/query?code=" + symbol;
    }

    public static string BuildKlineUrl(string symbol, string period, int bars)
    {
        return "https://ifzq.gtimg.cn/appstock/app/fqkline/get?param=" +
            symbol + "," + period + ",,," + bars + ",qfq";
    }

    public static string BuildDailyUrl(string symbol, int bars)
    {
        return BuildKlineUrl(symbol, "day", bars);
    }

    public static string FetchMinute(string symbol)
    {
        return Fetch(BuildMinuteUrl(symbol));
    }

    public static string FetchKline(string symbol, string period, int bars)
    {
        return Fetch(BuildKlineUrl(symbol, period, bars));
    }

    public static string FetchDaily(string symbol, int bars)
    {
        return FetchKline(symbol, "day", bars);
    }

    private static string Fetch(string url)
    {
        HttpClient client = GetClient();
        byte[] bytes = client.GetByteArrayAsync(url).Result;
        return Encoding.UTF8.GetString(bytes);
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
                    c.Timeout = TimeSpan.FromMilliseconds(5000);
                    _client = c;
                }
            }
        }
        return _client;
    }
}
