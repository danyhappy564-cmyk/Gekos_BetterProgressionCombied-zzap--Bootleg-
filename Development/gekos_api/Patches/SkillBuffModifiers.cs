using EFT;
using gekos_api.Helpers;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Reflection;

namespace gekos_api.Patches
{
    public static class SkillBuffMultiConfig
    {
        public static SkillsConfig config;

        static SkillBuffMultiConfig()
        {
            config = ConfigHandler.GetSkillsConfig();
        }
    }

    /// <summary>
    /// Skill buff values are produced by four little closures inside
    /// <see cref="SkillManager.FloatBuff"/> — one per rule kind (PerLevel, Max, Custom, Elite).
    /// Each stores the buff it belongs to plus its captured argument, and its method_0 is the
    /// rule body that writes <c>FloatBuff.Value</c>. We postfix each one and scale what it wrote.
    ///
    /// <para>On SPT 4.0 these were obfuscated as Class1425..Class1428; 4.1 deobfuscates them to
    /// CG_PerLevel / CG_Max / CG_Custom / CG_Elite, and renames their captured-this field from
    /// "SkillBuffClass" to "FloatBuff".</para>
    ///
    /// <para>Harmony cannot patch a generic base directly, so each rule kind still needs its own
    /// concrete subclass.</para>
    /// </summary>
    public abstract class SkillBuffMultiBase<T> : ModulePatch where T : class
    {
        static readonly SkillsConfig skillsConfig;

        /// <summary>The closure's captured "this" — the buff whose Value the rule just set.</summary>
        static readonly FieldInfo BuffField =
            AccessTools.Field(typeof(T), "FloatBuff");

        static SkillBuffMultiBase()
        {
            skillsConfig = ConfigHandler.GetSkillsConfig();
        }

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(T), "method_0");
        }

        protected static void DoPostfix(ref T __instance)
        {
            try
            {
                if (BuffField == null)
                {
                    Plugin.LogSource.LogWarning($"Could not find field 'FloatBuff' in type {typeof(T).Name}.");
                    return;
                }

                if (!(BuffField.GetValue(__instance) is SkillManager.FloatBuff buff))
                {
                    Plugin.LogSource.LogWarning("Null skill buff!");
                    return;
                }

                if (skillsConfig.BuffMultis.TryGetValue(buff.Id.ToString(), out float multi))
                {
                    buff.Value *= multi;
                }
            }
            catch (Exception e)
            {
                Plugin.LogSource.LogError("Something went wrong when trying to apply skill buff multipliers! Double check that the config is setup correctly!");
                Plugin.LogSource.LogError(e);
            }
        }
    }

    public class SkillBuffMulti1 : SkillBuffMultiBase<SkillManager.FloatBuff.CG_PerLevel>
    {
        [PatchPostfix]
        public static void Postfix(ref SkillManager.FloatBuff.CG_PerLevel __instance) => DoPostfix(ref __instance);
    }

    public class SkillBuffMulti2 : SkillBuffMultiBase<SkillManager.FloatBuff.CG_Max>
    {
        [PatchPostfix]
        public static void Postfix(ref SkillManager.FloatBuff.CG_Max __instance) => DoPostfix(ref __instance);
    }

    public class SkillBuffMulti3 : SkillBuffMultiBase<SkillManager.FloatBuff.CG_Custom>
    {
        [PatchPostfix]
        public static void Postfix(ref SkillManager.FloatBuff.CG_Custom __instance) => DoPostfix(ref __instance);
    }

    public class SkillBuffMulti4 : SkillBuffMultiBase<SkillManager.FloatBuff.CG_Elite>
    {
        [PatchPostfix]
        public static void Postfix(ref SkillManager.FloatBuff.CG_Elite __instance) => DoPostfix(ref __instance);
    }
}
