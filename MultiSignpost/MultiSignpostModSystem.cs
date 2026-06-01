using MultiSignpost.Blocks;
using MultiSignpost.Blocks.EntityMultiSignPost;
using MultiSignpost.Config;
using System;
using System.Reflection;
using Vintagestory.API.Common;

namespace MultiSignpost;

public class MultiSignpostModSystem : ModSystem
{
    private const string ConfigFileName = "multisignpost.json";

    public override void Start(ICoreAPI api)
    {
        base.Start(api);

        LoadConfig(api);
        TryRegisterConfigLib(api);

        api.RegisterBlockClass(Mod.Info.ModID + ".blockMultiSignPost", typeof(BlockMultiSignPost));
        api.RegisterBlockClass(Mod.Info.ModID + ".blockMultiSignPostExtension", typeof(BlockMultiSignPostExtension));

        api.RegisterBlockEntityClass(Mod.Info.ModID + ".multiSignPost", typeof(BlockEntityMultiSignPost));
        api.RegisterBlockEntityClass(Mod.Info.ModID + ".multiSignPostExtension", typeof(BlockEntityMultiSignPostExtension));
    }

    private static void LoadConfig(ICoreAPI api)
    {
        try
        {
            MultiSignpostConfig.Current = api.LoadModConfig<MultiSignpostConfig>(ConfigFileName)
                                          ?? new MultiSignpostConfig();
        }
        catch (Exception)
        {
            MultiSignpostConfig.Current = new MultiSignpostConfig();
        }

        MultiSignpostConfig.Current.Sanitize();

        api.StoreModConfig(MultiSignpostConfig.Current, ConfigFileName);
    }

    private void TryRegisterConfigLib(ICoreAPI api)
    {
        ModSystem configLibSystem = api.ModLoader.GetModSystem("ConfigLib.ConfigLibModSystem");

        if (configLibSystem == null)
        {
            return;
        }

        MethodInfo registerMethod = configLibSystem.GetType().GetMethod(
            "RegisterCustomManagedConfig",
            new[]
            {
                typeof(string),
                typeof(object),
                typeof(string),
                typeof(Action),
                typeof(Action<string>),
                typeof(Action)
            }
        );

        if (registerMethod == null)
        {
            api.Logger.Warning("[MultiSignpost] ConfigLib is loaded, but RegisterCustomManagedConfig was not found.");
            return;
        }

        Action sanitize = () =>
        {
            MultiSignpostConfig.Current.Sanitize();
        };

        Action<string> sanitizeChangedSetting = _ =>
        {
            MultiSignpostConfig.Current.Sanitize();
        };

        try
        {
            registerMethod.Invoke(
                configLibSystem,
                new object[]
                {
                Mod.Info.ModID,
                MultiSignpostConfig.Current,
                ConfigFileName,
                sanitize,
                sanitizeChangedSetting,
                sanitize
                }
            );
        }
        catch (Exception exception)
        {
            api.Logger.Warning("[MultiSignpost] Failed to register ConfigLib integration. Continuing without ConfigLib.");
            api.Logger.VerboseDebug(exception.ToString());
        }
    }
}