using Reloaded.Mod.Interfaces;

namespace GBFR.PreEquippedSigils.Configuration;

/// <summary>
/// Reloaded-II configuration page connector. The launcher discovers this class
/// (IConfiguratorV3) automatically and renders the declared configuration UI.
/// </summary>
public class Configurator : IConfiguratorV3
{
    private static readonly ConfiguratorMixinBase _configuratorMixin = new();

    public string? ModFolder { get; private set; }
    public string? ConfigFolder { get; private set; }
    public ConfiguratorContext Context { get; private set; }

    public IUpdatableConfigurable[] Configurations => _configurations ??= MakeConfigurations();
    private IUpdatableConfigurable[]? _configurations;

    private IUpdatableConfigurable[] MakeConfigurations()
    {
        var configurations = _configuratorMixin.MakeConfigurations(ConfigFolder!);

        // Keep the array in sync with the launcher's copy-on-update behavior.
        for (int x = 0; x < configurations.Length; x++)
        {
            var index = x;
            configurations[index].ConfigurationUpdated += configurable =>
            {
                configurations[index] = configurable;
            };
        }

        return configurations;
    }

    public Configurator()
    {
    }

    public Configurator(string configDirectory) : this()
    {
        ConfigFolder = configDirectory;
    }

    public void Migrate(string oldDirectory, string newDirectory) =>
        _configuratorMixin.Migrate(oldDirectory, newDirectory);

    public TType GetConfiguration<TType>(int index) => (TType)Configurations[index];

    public void SetConfigDirectory(string configDirectory) => ConfigFolder = configDirectory;

    public void SetContext(in ConfiguratorContext context) => Context = context;

    public IConfigurable[] GetConfigurations() => Configurations;

    public bool TryRunCustomConfiguration() =>
        _configuratorMixin.TryRunCustomConfiguration(this);

    public void SetModDirectory(string modDirectory) => ModFolder = modDirectory;
}
