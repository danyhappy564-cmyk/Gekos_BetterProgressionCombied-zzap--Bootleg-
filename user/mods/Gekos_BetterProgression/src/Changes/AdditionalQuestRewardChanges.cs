namespace GekosBetterProgression.Changes;

public class AdditionalQuestRewardChanges
{
    public static bool Apply(Context context)
    {
        int added = Utils.ApplyAdditionalQuestRewards(context, context.advancedConfig.additionalQuestRewards);
        context.logger.Info($"Added {added} additional quest rewards");

        return true;
    }
}