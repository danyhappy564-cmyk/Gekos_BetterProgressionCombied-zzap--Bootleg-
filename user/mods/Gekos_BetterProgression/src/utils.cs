using SPTarkov.Server.Core.Utils.Json;
using GekosBetterProgression.AlgoRebalance;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Hideout;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;
using static GekosBetterProgression.AdvancedConfig;
//using gekosbetterprogression.AlgoRebalancing.Types;

namespace GekosBetterProgression;

public static class Utils
{
    public static readonly HashSet<string> Currencies = new()
    {
        "5449016a4bdc2d6f028b456f", // Roubles
        "5696686a4bdc2da3298b456a", // Dollars
        "569668774bdc2da2298b4568", // Euros
        "5d235b4d86f7742e017bc88a", // GP Coin
        "6656560053eaaa7a23349c86"  // Lega Medal
    };

    private static readonly Dictionary<string, int> Purchasability = new();

    private static readonly Dictionary<string, List<Item>> CachedAttachments = new();
    private static readonly HashSet<string> MissingTemplateWarned = new();

    // ---------------------------------------------
    // DOGTAGS
    // ---------------------------------------------

    public static HashSet<MongoId> GetDogtagsList(Context context)
    {
        HashSet<MongoId> list = new();

        foreach (var kvp in context.tables.Templates.Items)
        {
            // for some reason apollo and malboro cigs have dogtagqualities set, but false
            // nullables are good for programming yes yes
            if (kvp.Value.Properties?.DogTagQualities != null && kvp.Value.Properties.DogTagQualities == true)
            {
                list.Add(kvp.Key);
            }
        }

        return list;
    }

    // ---------------------------------------------
    // TRADES
    // ---------------------------------------------

    public static List<(Trader trader, Item trade)> FindTrades(string itemId, Context context)
    {
        List<(Trader, Item)> found = new();

        foreach (var trader in context.tables.Traders.Values)
        {
            if (trader.Assort == null)
                continue;

            foreach (var trade in trader.Assort.Items)
            {
                if (trade.Template == itemId)
                {
                    found.Add((trader, trade));
                }
            }
        }

        return found;
    }

    public static bool IsCurrency(MongoId template)
    {
        return Currencies.Contains(template);
    }

    public static List<string> GetDefaultAttachments(string weaponId, Context context)
    {
        var presets = context.presetHelper.GetDefaultWeaponPresets();

        if (!presets.TryGetValue(weaponId, out var preset) || preset == null)
        {
            return new();
        }

        return preset.Items.Select(x => (string)x.Template).ToList();
    }

    // ---------------------------------------------
    // ATTACHMENTS
    // ---------------------------------------------

    public static List<Item> UnrollAttachments(Item item, List<Item> assort)
    {

        if (CachedAttachments.TryGetValue(item.Id.ToString(), out var cached))
        {
            return cached;
        }

        List<Item> attachments = new();

        var children = assort
            .Where(x =>
            {
                var pid = x.ParentId.ToString();
                var iid = item.Id.ToString();
                return !string.IsNullOrEmpty(pid) && pid == iid && x.Template != item.Template;
            })
            .ToList();

        attachments.AddRange(children);

        foreach (var att in children)
        {
            attachments.AddRange(UnrollAttachments(att, assort));
        }

        CachedAttachments[item.Id.ToString()] = attachments;
        return attachments;
    }

    public static bool ContainsAttachment(Item item, List<Item> assort, string attachmentId, Context context)
    {
        if (!context.tables.Templates.Items.TryGetValue(item.Template, out var template))
        {
            // Warn only once per missing template to avoid flooding logs when other mods reference
            // non-existent items repeatedly.
            if (!MissingTemplateWarned.Contains(item.Template))
            {
                MissingTemplateWarned.Add(item.Template);
                context.logger.Warning(
                    $"Trader item {item.Id} with table ID {item.Template} couldn't be found in the tables!"
                );
            }
        }
        else
        {
            if (template.Properties?.Slots == null || !template.Properties.Slots.Any())
            {
                return false;
            }
        }

        return UnrollAttachments(item, assort)
            .Any(x => x.Template == attachmentId);
    }

    // ---------------------------------------------
    // INDEXING
    // ---------------------------------------------

    
    public static Dictionary<string, ChangedItem> IndexById(
        Dictionary<int, List<ChangedItem>> byTier)
    {
        Dictionary<string, ChangedItem> byId = new();

        foreach (var items in byTier.Values)
        {
            foreach (var item in items)
            {
                byId[item.trade.Id.ToString()] = item;
            }
        }

        return byId;
    }
    

    // ---------------------------------------------
    // PURCHASABILITY
    // ---------------------------------------------

    
    public static bool CanBePurchased(
        string itemId,
        bool excludeBarters,
        bool excludeQuestlocks,
        int tierCutoff,
        List<string> skip,
        Dictionary<string, ChangedItem> tierOverrides,
        Context context)
    {
        if (Purchasability.TryGetValue(itemId, out var cached)
            && cached <= tierCutoff)
        {
            return true;
        }

        foreach (var trader in context.tables.Traders.Values)
        {
            if (trader.Assort == null)
                continue;

            foreach (var trade in trader.Assort.Items)
            {
                int loyalty;
                var tradeKey = trade.Id.ToString();

                // Skip trades which reference table templates that don't exist.
                if (string.IsNullOrEmpty(trade.Template) || !context.tables.Templates.Items.ContainsKey(trade.Template))
                {
                    if (!MissingTemplateWarned.Contains(trade.Template))
                    {
                        MissingTemplateWarned.Add(trade.Template);
                        context.logger.Warning($"Trader item {trade.Id} from trader {trader.Base.Name} ({trader.Base.Id}) with table ID {trade.Template} couldn't be found in the tables!");
                    }
                    continue;
                }

                if (tierOverrides.ContainsKey(tradeKey))
                {
                    loyalty = LoyaltyFromScore(tierOverrides[tradeKey].score, context.config.algorithmicalRebalancing.clampToMaxLevel);
                }
                else
                {
                    if (!trader.Assort.LoyalLevelItems.TryGetValue(tradeKey, out loyalty))
                    {
                        //ToDo: If loyalty mapping was not found directly it could be an attachment for an item for sale, check parent recursively.
                        //      For now we just consider the item as not for sale and continue.
                        //      Careful to also then change the whole infrastructure to account for the fact that this function might find the attachments on the very
                        //      gun we're dissecting to tell if it has any advanced attachments
                        continue;
                    }
                }

                if (LoyaltyFromScore(
                        loyalty,
                        context.config.algorithmicalRebalancing.clampToMaxLevel)
                    > tierCutoff)
                {
                    continue;
                }

                bool match =
                    trade.Template == itemId &&
                    trader.Assort.BarterScheme.ContainsKey(trade.Id)
                    || ContainsAttachment(trade, trader.Assort.Items, itemId, context);

                if (!match)
                    continue;

                if (excludeBarters && IsBarterTrade(trade, trader)) continue;
                if (excludeQuestlocks && IsQuestLocked(trade, trader, context)) continue;
                if (skip.Contains(trade.Id)) continue;

                Purchasability[itemId] = Purchasability.ContainsKey(itemId)
                    ? Math.Min(Purchasability[itemId], loyalty)
                    : loyalty;

                return true;
            }
        }

        return false;
    }

    public static bool CanAllAttachmentsBePurchased(
        Item item,
        List<Item> assort,
        bool excludeBarters,
        bool excludeQuestlocks,
        int tierCutoff,
        List<string> skip,
        Dictionary<string, ChangedItem> tierOverrides,
        Context context)
    {
        var attachments = UnrollAttachments(item, assort);

        foreach (var att in attachments)
        {
            if (skip.Contains(att.Template)) continue;

            if (!CanBePurchased(
                    att.Template,
                    excludeBarters,
                    excludeQuestlocks,
                    tierCutoff,
                    new() { item.Id },
                    tierOverrides,
                    context))
            {
                return false;
            }
        }

        return true;
    }
    

    // ---------------------------------------------
    // QUEST / HIDEOUT
    // ---------------------------------------------

    /// <summary>
    /// Adds the configured rewards to the quest templates. Safe to call more than once: a reward whose id is
    /// already on the quest is skipped, so a late re-run only restores what another mod removed.
    /// </summary>
    /// <returns>How many rewards were added by this call.</returns>
    public static int ApplyAdditionalQuestRewards(Context context, AdditionalQuestRewards additionalQuestRewards)
    {
        // these are not typos, the quest reward keys are capitalized
        return AddQuestRewards(context, additionalQuestRewards.started, "Started")
            + AddQuestRewards(context, additionalQuestRewards.success, "Success");
    }

    private static int AddQuestRewards(Context context, Dictionary<string, Reward> questIdToReward, string rewardKey)
    {
        int added = 0;

        foreach (KeyValuePair<string, Reward> questIDToReward in questIdToReward)
        {
            // One missing quest (removed by another mod, typo in config) must not take the other rewards down with it
            if (!context.tables.Templates.Quests.TryGetValue(questIDToReward.Key, out Quest? quest))
            {
                context.logger.Warning($"Quest {questIDToReward.Key} not found, skipping its additional {rewardKey} reward");
                continue;
            }

            if (quest.Rewards is null || !quest.Rewards.TryGetValue(rewardKey, out List<Reward>? rewardList) || rewardList is null)
            {
                context.logger.Warning($"Quest {questIDToReward.Key} has no {rewardKey} reward list, skipping its additional reward");
                continue;
            }

            if (rewardList.Any(r => r.Id == questIDToReward.Value.Id))
            {
                continue;
            }

            rewardList.Add(questIDToReward.Value);
            added++;
        }

        return added;
    }

    /// <summary>
    /// Counts configured rewards that are not (or no longer) on their quest.
    /// </summary>
    public static int CountMissingQuestRewards(Context context, AdditionalQuestRewards additionalQuestRewards)
    {
        int CountMissing(Dictionary<string, Reward> questIdToReward, string rewardKey) =>
            questIdToReward.Count(kv =>
                !context.tables.Templates.Quests.TryGetValue(kv.Key, out Quest? quest)
                || quest.Rewards is null
                || !quest.Rewards.TryGetValue(rewardKey, out List<Reward>? list)
                || list is null
                || !list.Any(r => r.Id == kv.Value.Id));

        return CountMissing(additionalQuestRewards.started, "Started") + CountMissing(additionalQuestRewards.success, "Success");
    }

    public static void LockBehindQuest(
        Context context,
        string traderId,
        string trade,
        string itemId,
        QuestLock questLock)
    {
        LockBehindQuest(context, traderId, trade, questLock.quest, itemId, questLock.rewardId, questLock.targetId);
    }

    public static void LockBehindQuest(
        Context context,
        string traderId,
        string trade,
        string lockQuest,
        string itemId,
        string rewardId,
        string targetId)
    {
        var trader = context.tables.Traders[traderId];

        trader.QuestAssort["success"][trade] = lockQuest;

        var rewards = context.tables.Templates.Quests[lockQuest].Rewards?["Success"];

        rewards?.Add(new Reward
        {
            Type = RewardType.AssortmentUnlock,
            Index = rewards.Count,
            // 4.1 widened Reward.TraderId to a string-or-int union.
            TraderId = new StringOrInt(traderId, null),
            Target = targetId,
            Items = new()
            {
                new Item
                {
                    Id = targetId,
                    Template = itemId
                }
            },
            Id = rewardId
        });
    }

    public static void SetAreaLevelRequirement(HideoutProduction craft, int level)
    {
        foreach (var req in craft.Requirements)
        {
            req.RequiredLevel = level;
        }
    }

    public static bool IsQuestLockedCraft(HideoutProduction craft)
    {
        return craft.Requirements.Any(x => x.QuestId != null);
    }

    // ---------------------------------------------
    // LOCALES
    // ---------------------------------------------

    public static void AddToLocale(Context context, string id, CustomLocale customLocale)
    {
        AddToLocale(context, id, customLocale.Name, customLocale.ShortName, customLocale.Description);
    }

    public static void AddToLocale(
        Context context,
        string id,
        string name,
        string shortname,
        string description)
    {
        Dictionary<string, string> locale = context.localeService.GetLocaleDb();

        locale[$"{id} Name"] = name;
        locale[$"{id} ShortName"] = shortname;
        locale[$"{id} Description"] = description;
    }

    // ---------------------------------------------
    // NICHE CHECK
    // ---------------------------------------------

    public static bool ShareSameNiche(
        Item a,
        Item b,
        Trader aTrader,
        Trader bTrader,
        Context context)
    {
        var c = context.config.algorithmicalRebalancing.weaponRules.upshiftRules;

        var aTempl = context.tables.Templates.Items[a.Template];
        var bTempl = context.tables.Templates.Items[b.Template];

        if (c.devideNicheByFiremode &&
            BestFiremode(aTempl) != BestFiremode(bTempl))
            return false;

        if (c.devideNicheByCaliber &&
            aTempl.Properties?.AmmoCaliber != bTempl.Properties?.AmmoCaliber)
            return false;

        if (c.devideNicheByBarterType &&
            IsBarterTrade(a, aTrader) != IsBarterTrade(b, bTrader))
            return false;

        if (c.devideNicheByQuestLock &&
            IsQuestLocked(a, aTrader, context) != IsQuestLocked(b, bTrader, context))
            return false;

        return true;
    }

    // ---------------------------------------------
    // LOYALTY
    // ---------------------------------------------

    public static int LoyaltyFromScore(float score, bool capToMax)
    {
        int max = capToMax ? 4 : 999;
        return Math.Max(1, Math.Min(max, (int)Math.Floor((double)score)));
    }

    public static void SetLoyalty(
        string itemId,
        float loyaltyScore,
        Trader trader,
        bool capToMax)
    {
        trader.Assort.LoyalLevelItems[itemId] =
            LoyaltyFromScore(loyaltyScore, capToMax);
    }

    // ---------------------------------------------
    // BARTER / QUEST LOCK
    // ---------------------------------------------

    public static bool IsBarterTrade(Item trade, Trader trader)
    {
        if (!trader.Assort.BarterScheme.TryGetValue(trade.Id, out var schemes))
            return false;

        foreach (var group in schemes)
        {
            foreach (var ask in group)
            {
                if (!Currencies.Contains(ask.Template))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static bool IsQuestLocked(
        Item trade,
        Trader trader,
        Context context)
    {
        try
        {
            var locks = trader.QuestAssort["success"].Keys
                .Concat(trader.QuestAssort["started"].Keys)
                .Concat(trader.QuestAssort["fail"].Keys);

            return locks.Contains(trade.Id);
        }
        catch (Exception ex)
        {
            context.logger.Warning(
                $"Failed to fetch quest locks for {trader.Base.Name} ({trader.Base.Id})"
            );

            if (context.config.dev.showFullError)
            {
                context.logger.Error(ex.ToString());
            }

            return false;
        }
    }

    // ---------------------------------------------
    // PATHS
    // ---------------------------------------------

    public static string GetModFolder()
    {
        return System.IO.Path.Combine(AppContext.BaseDirectory, "..");
    }

    // ---------------------------------------------
    // FIREMODE
    // ---------------------------------------------

    public static string BestFiremode(TemplateItem item)
    {
        return PickBestFiremode(
            item.Properties.WeapFireType.ToArray<string>(),
            (item.Properties.BoltAction ?? false) || (!item.Properties.CanQueueSecondShot ?? false));
    }

    public static string PickBestFiremode(
        string[] modes,
        bool isManual)
    {
        if (modes == null)
            return "";

        Dictionary<string, int> ranks = new()
        {
            ["none"] = -9999,
            ["manual"] = 0,
            ["doublet"] = 1,
            ["semiauto"] = 1,
            ["doubleaction"] = 1,
            ["single"] = isManual ? -100 : 1,
            ["burst"] = 2,
            ["fullauto"] = 3
        };

        string best = "none";

        foreach (var mode in modes)
        {
            if (ranks[best] < ranks[mode])
            {
                best = mode;
            }
        }

        if (isManual && ranks[best] < ranks["manual"])
        {
            best = "manual";
        }

        if (best == "manual" || best == "pumpaction" || (isManual && best == "single"))
            return "manual";

        if (best == "single" || best == "doubleaction" || best == "doublet")
            return "semiauto";

        return best;
    }
}
