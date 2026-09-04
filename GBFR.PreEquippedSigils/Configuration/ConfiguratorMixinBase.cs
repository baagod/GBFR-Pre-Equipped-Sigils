using Reloaded.Mod.Interfaces;

namespace GBFR.PreEquippedSigils.Configuration;

/// <summary>
/// Declares the mod's configuration entries and optional custom launcher
/// behavior. Copied from the official Reloaded-II mod template.
/// </summary>
public class ConfiguratorMixinBase
{
    public virtual IUpdatableConfigurable[] MakeConfigurations(string configFolder)
    {
        return new IUpdatableConfigurable[]
        {
            HotkeyConfig.FromFile(
                Path.Combine(configFolder, HotkeyConfig.FileName),
                HotkeyConfig.ConfigurationName),
        };
    }

    public virtual bool TryRunCustomConfiguration(Configurator configurator)
    {
        return false;
    }

    public virtual void Migrate(string oldDirectory, string newDirectory)
    {
    }
}
