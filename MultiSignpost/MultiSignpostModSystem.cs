using MultiSignpost.Blocks;
using MultiSignpost.Config;
using System;
using Vintagestory.API.Common;

namespace MultiSignpost;

public class MultiSignpostModSystem : ModSystem
{
    private const string ConfigFileName = "multisignpost.json";

    public override void Start(ICoreAPI api)
    {
        base.Start(api);

        LoadConfig(api);

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
}