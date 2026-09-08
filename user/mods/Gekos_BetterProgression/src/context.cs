using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services.Locales;
using SPTarkov.Server.Core.Utils;

namespace GekosBetterProgression;

/// <summary>
/// SPT 4.1 dissolved <c>DatabaseTables</c> into one injectable record per table. This keeps the
/// old <c>tables.Something</c> shape so every change script still reads the way it did on 4.0.
/// </summary>
public sealed class DatabaseTablesView(
    TemplateTable templates,
    TradersTable traders,
    HideoutTable hideout,
    GlobalTable globals,
    LocaleTable locales)
{
    public TemplateTable Templates { get; } = templates;
    public TradersTable Traders { get; } = traders;
    public HideoutTable Hideout { get; } = hideout;
    public GlobalTable Globals { get; } = globals;
    public LocaleTable Locales { get; } = locales;
}

[Injectable]
public class Context
{
    public DatabaseTablesView tables = null!;
    public ItemHelper itemHelper = null!;
    public PresetHelper presetHelper = null!;
    public ProfileHelper profileHelper = null!;
    public QuestConfig questConfig = null!;
    public HashUtil hashUtil = null!;
    public GekoConfig config = null!;
    public AdvancedConfig advancedConfig = null!;
    public ILoggerWrapper logger = null!;
    public LocaleService localeService = null!;

    public bool IsInitialized => config != null;

    public void PreInitialize(
        ItemHelper _itemHelper,
        PresetHelper _presetHelper,
        ProfileHelper _profileHelper,
        QuestConfig _questConfig,
        HashUtil _hashUtil,
        GekoConfig _config,
        AdvancedConfig _advancedConfig,
        ILoggerWrapper _logger
    )
    {
        this.itemHelper = _itemHelper;
        this.presetHelper = _presetHelper;
        this.profileHelper = _profileHelper;
        this.questConfig = _questConfig;
        this.hashUtil = _hashUtil;
        this.config = _config;
        this.advancedConfig = _advancedConfig;
        this.logger = _logger;
    }

    public void PostInitialize(
        DatabaseTablesView _tables,
        ILoggerWrapper _logger,
        LocaleService _localeService
    )
    {
        this.tables = _tables;
        this.logger = _logger;
        this.localeService = _localeService;
    }
}
