namespace BenvilHacks.Api;

public sealed record PlanEntry(string Title, decimal Price);

public static class PlanCatalog
{
    private static readonly Dictionary<string, PlanEntry> Map = Build(StringComparer.OrdinalIgnoreCase);

    public static bool TryGet(string planId, out PlanEntry entry) =>
        Map.TryGetValue(planId, out entry!);

    private static void Add(Dictionary<string, PlanEntry> d, string id, string title, decimal price) =>
        d[id] = new PlanEntry(title, price);

    private static Dictionary<string, PlanEntry> Build(StringComparer comparer)
    {
        var d = new Dictionary<string, PlanEntry>(comparer);

        void ff(string platform, string productTitle)
        {
            Add(d, $"ff-{platform}-7", $"{productTitle} — 7 dias", 50);
            Add(d, $"ff-{platform}-30", $"{productTitle} — 30 dias", 100);
            Add(d, $"ff-{platform}-90", $"{productTitle} — 3 meses", 200);
        }

        ff("android", "Apk Android Painel");
        ff("ios", "Painel iOS");
        ff("pc", "Painel PC");

        void ff1real(string platform, string productTitle)
        {
            Add(d, $"ff-1real-{platform}-7", $"{productTitle} — 7 dias", 1m);
            Add(d, $"ff-1real-{platform}-30", $"{productTitle} — 30 dias", 1m);
            Add(d, $"ff-1real-{platform}-90", $"{productTitle} — 3 meses", 1m);
        }

        ff1real("android", "Apk Android Painel");
        ff1real("ios", "Painel iOS");
        ff1real("pc", "Painel PC");

        void val(string prefix, string productTitle)
        {
            Add(d, $"{prefix}-7", $"{productTitle} — 7 dias", 50);
            Add(d, $"{prefix}-30", $"{productTitle} — 30 dias", 100);
            Add(d, $"{prefix}-90", $"{productTitle} — 3 meses", 200);
        }

        val("val-aimbot", "Valorant Aimbot");
        val("val-aimbot-esp", "Valorant Aimbot + ESP");

        void aimPainel(string prefix, string gameName)
        {
            val($"{prefix}-aimbot", $"{gameName} Aimbot");
            val($"{prefix}-painel", $"{gameName} Painel");
        }

        aimPainel("cs2", "Counter-Strike 2");
        aimPainel("fn", "Fortnite");
        aimPainel("apex", "Apex Legends");
        aimPainel("wz", "Warzone");

        void tierNamed(string prefix, string productTitle, (string suffix, string label, decimal price)[] tiers)
        {
            foreach (var (suffix, label, price) in tiers)
                Add(d, $"{prefix}-{suffix}", $"{productTitle} — {label}", price);
        }

        var robloxTiers = new[]
        {
            ("7", "7 dias", 39.9m), ("30", "1 mês", 59.9m), ("60", "2 meses", 99.9m),
        };
        tierNamed("roblox", "Painel multi função", robloxTiers);

        var brawlTiers = new[]
        {
            ("7", "7 dias", 39.9m), ("30", "1 mês", 59.9m), ("90", "3 meses", 149.9m),
        };
        tierNamed("brawl-pc-painel", "Painel (PC)", brawlTiers);
        tierNamed("brawl-mobile-painel", "Painel (Mobile)", brawlTiers);

        tierNamed("pogo-android", "Mapa + Teleporte 100% anti ban", robloxTiers);
        tierNamed("pogo-ios", "Mapa + Teleporte 100% anti ban", robloxTiers);

        var clashTiers = new[]
        {
            ("7", "7 dias", 79.9m), ("30", "1 mês", 119.9m), ("60", "2 meses", 199.9m),
        };
        tierNamed("clash", "Elixir infinito", clashTiers);

        Add(d, "starter", "Plano Starter — Benvil Hacks", 29.9m);
        Add(d, "pro", "Plano Pro — Benvil Hacks", 59.9m);
        Add(d, "elite", "Plano Elite — Benvil Hacks", 99.9m);

        return d;
    }
}
