using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace MultiSignpost.Blocks;

public class BlockEntityMultiSignPostExtension : BlockEntity
{
    private BlockPos basePos;

    public BlockPos BasePos => basePos;

    public void SetBasePos(BlockPos pos)
    {
        basePos = pos.Copy();
        MarkDirty(true);
    }

    public bool IsOwnedBy(BlockPos pos)
    {
        return basePos != null
               && basePos.X == pos.X
               && basePos.Y == pos.Y
               && basePos.Z == pos.Z;
    }

    public BlockEntityMultiSignPost GetBaseEntity(IWorldAccessor world)
    {
        if (basePos == null)
        {
            return null;
        }

        return world.BlockAccessor.GetBlockEntity(basePos) as BlockEntityMultiSignPost;
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);

        if (tree.GetInt("hasBasePos", 0) == 1)
        {
            basePos = new BlockPos(
                tree.GetInt("baseX", 0),
                tree.GetInt("baseY", 0),
                tree.GetInt("baseZ", 0)
            );
        }
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);

        if (basePos == null)
        {
            tree.SetInt("hasBasePos", 0);
            return;
        }

        tree.SetInt("hasBasePos", 1);
        tree.SetInt("baseX", basePos.X);
        tree.SetInt("baseY", basePos.Y);
        tree.SetInt("baseZ", basePos.Z);
    }
}