using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;
using LanguageExt.UnitsOfMeasure;
using TairitsuSora.Core;
using TairitsuSora.Utils;

namespace TairitsuSora.Commands;

[RegisterCommand]
public class Heytea : Command
{
    public override CommandInfo Info => new()
    {
        Trigger = "ht",
        Summary = "喝喜茶吗？"
    };

    [MessageHandler(Description = "随机喜茶推荐")]
    public async ValueTask<string> RandomTea()
    {
        try
        {
            var item = (await GetItems()).Sample();
            return $"要不要试试 {item.Category} 分类下的 {item.Name}？";
        }
        catch { return "获取喜茶信息失败"; }
    }

    [SuppressMessage("ReSharper", "NotAccessedPositionalProperty.Local")]
    private record Item(
        string Name,
        string Category,
        string MainLabel,
        string[] Labels
    );

    private static readonly TimeSpan UpdateInterval = 8.Hours();
    private HttpClient _client = new();
    private AsyncLock _lock = new();
    private Item[]? _cache;
    private DateTime _lastUpdateTime = DateTime.MinValue;

    private async ValueTask<Item[]> GetItems()
    {
        using var _ = await _lock.LockAsync();
        if (_cache is not null && DateTime.Now - _lastUpdateTime <= UpdateInterval) return _cache;
        _cache = await UpdateItems();
        _lastUpdateTime = DateTime.Now;
        return _cache;
    }

    private async ValueTask<Item[]> UpdateItems()
    {
        const string apiUrl =
            "https://go.heytea.com/api/service-menu/vip/grayapi/v7/shop/categories?shopId=555&menuType=0&from=0&isTakeaway=0";
        var response = await _client.GetAsync(apiUrl);
        response.EnsureSuccessStatusCode();
        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        var categories = json["data"]!["categories"]!.AsArray();
        Dictionary<string, Item> items = [];
        foreach (var category in categories)
        {
            string categoryName = category!["name"]!.GetValue<string>();
            var products = category["products"]!.AsArray();
            foreach (var product in products)
            {
                var skus = product!["skus"]!.AsArray();
                if (skus.Count != 1) continue;
                string itemId = skus[0]!["no"]!.GetValue<string>();
                if (items.ContainsKey(itemId) ||
                    product["is_single"]!.GetValue<bool>() ||
                    product["type"]!.GetValue<int>() != 0)
                    continue;
                string mainLabel = product["label"]!.GetValue<string>();
                string itemName = skus[0]!["name"]!.GetValue<string>();
                string[] labels = product["labels"]!.AsArray().Select(obj => obj!["name"]!.GetValue<string>()).ToArray();
                items[itemId] = new Item(itemName, categoryName, mainLabel, labels);
            }
        }
        return items.Values.ToArray();
    }
}
