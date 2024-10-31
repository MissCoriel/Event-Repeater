using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using System;
using System.Collections.Generic;

namespace EventRepeater.Framework;
internal static class AssetManager
{
    internal static readonly string MailToRepeatName = PathUtilities.NormalizeAssetName("Mods/EventRepeater/MailToRepeat");
    private static readonly string EventsToRepeatName = PathUtilities.NormalizeAssetName("Mods/EventRepeater/EventsToRepeat");
    private static readonly string ResponsesToRepeatName = PathUtilities.NormalizeAssetName("Mods/EventRepeater/ResponsesToRepeat");

    private static IAssetName EventsToRepeatAsset = null!;
    private static IAssetName ResponsesToRepeatAsset = null!;

    private static IMonitor Monitor = null!;

    internal static Lazy<HashSet<string>> EventsToForget { get; private set; } = new(() => PopulateAsset(EventsToRepeatName));
    internal static Lazy<HashSet<string>> ResponseToForget { get; private set; } = new(() => PopulateAsset(ResponsesToRepeatName));

    internal static void Initialize(IGameContentHelper helper, IMonitor monitor)
    {
        EventsToRepeatAsset = helper.ParseAssetName(EventsToRepeatName);
        ResponsesToRepeatAsset = helper.ParseAssetName(ResponsesToRepeatName);

        Monitor = monitor;
    }

    internal static void Apply(AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo(MailToRepeatName)
            || e.NameWithoutLocale.IsEquivalentTo(EventsToRepeatAsset)
            || e.NameWithoutLocale.IsEquivalentTo(ResponsesToRepeatAsset))
        {
            e.LoadFrom(static () => new Dictionary<string, string>(), AssetLoadPriority.Exclusive);
        }
    }

    internal static void Reset(IReadOnlySet<IAssetName>? assets)
    {
        if ((assets is null || assets.Contains(EventsToRepeatAsset)) && EventsToForget.IsValueCreated)
            EventsToForget = new(() => PopulateAsset(EventsToRepeatName));
        if ((assets is null || assets.Contains(ResponsesToRepeatAsset)) && ResponseToForget.IsValueCreated)
            ResponseToForget = new(() => PopulateAsset(ResponsesToRepeatName));
    }

    private static HashSet<string> PopulateAsset(string assetName)
    {
        var asset = Game1.content.Load<Dictionary<string, string>>(assetName);
        return new HashSet<string>(asset.Keys);
    }
}
