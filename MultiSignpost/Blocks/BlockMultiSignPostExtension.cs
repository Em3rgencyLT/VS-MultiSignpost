using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace MultiSignpost.Blocks;

public class BlockMultiSignPostExtension : Block
{
    public override bool TryPlaceBlock(
        IWorldAccessor world,
        IPlayer byPlayer,
        ItemStack itemstack,
        BlockSelection blockSel,
        ref string failureCode)
    {
        // Prevent direct/manual placement. These are managed only by the base signpost.
        failureCode = "cant-place-multisignpost-extension";
        return false;
    }

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        PlacedPriorityInteract = true;
    }

    public override void OnBlockBroken(
        IWorldAccessor world,
        BlockPos pos,
        IPlayer byPlayer,
        float dropQuantityMultiplier = 1)
    {
        BlockEntityMultiSignPostExtension extensionBe =
            world.BlockAccessor.GetBlockEntity(pos) as BlockEntityMultiSignPostExtension;

        BlockEntityMultiSignPost baseBe = extensionBe?.GetBaseEntity(world);

        if (baseBe != null)
        {
            baseBe.BreakEntireSignpost(byPlayer, dropQuantityMultiplier);
            return;
        }

        // Orphaned extension block. Remove it without drops.
        world.BlockAccessor.SetBlock(0, pos);
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        BlockEntityMultiSignPostExtension extensionBe =
            world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityMultiSignPostExtension;

        BlockEntityMultiSignPost baseBe = extensionBe?.GetBaseEntity(world);

        if (baseBe != null)
        {
            baseBe.OnRightClick(byPlayer);
            return true;
        }

        return true;
    }

    public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
    {
        BlockEntityMultiSignPostExtension extensionBe =
            world.BlockAccessor.GetBlockEntity(pos) as BlockEntityMultiSignPostExtension;

        BlockEntityMultiSignPost baseBe = extensionBe?.GetBaseEntity(world);

        if (baseBe?.Block != null)
        {
            return baseBe.Block.OnPickBlock(world, baseBe.Pos);
        }

        return base.OnPickBlock(world, pos);
    }

    public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
    {
        BlockEntityMultiSignPostExtension extensionBe =
            blockAccessor.GetBlockEntity(pos) as BlockEntityMultiSignPostExtension;

        BlockEntityMultiSignPost baseBe = extensionBe?.GetBaseEntity(blockAccessor.GetBlockEntity(pos)?.Api.World);

        if (baseBe != null)
        {
            return baseBe.GetExtensionPoleBoxes(pos);
        }

        return base.GetCollisionBoxes(blockAccessor, pos);
    }

    public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
    {
        BlockEntityMultiSignPostExtension extensionBe =
            blockAccessor.GetBlockEntity(pos) as BlockEntityMultiSignPostExtension;

        BlockEntityMultiSignPost baseBe = extensionBe?.GetBaseEntity(blockAccessor.GetBlockEntity(pos)?.Api.World);

        if (baseBe != null)
        {
            return baseBe.GetExtensionPoleBoxes(pos);
        }

        return base.GetSelectionBoxes(blockAccessor, pos);
    }

    public override Cuboidf GetParticleBreakBox(IBlockAccessor blockAccess, BlockPos pos, BlockFacing facing)
    {
        BlockEntityMultiSignPostExtension extensionBe =
            blockAccess.GetBlockEntity(pos) as BlockEntityMultiSignPostExtension;

        BlockEntityMultiSignPost baseBe = extensionBe?.GetBaseEntity(blockAccess.GetBlockEntity(pos)?.Api.World);

        if (baseBe != null)
        {
            return baseBe.GetExtensionPoleParticleBreakBox(pos);
        }

        return base.GetParticleBreakBox(blockAccess, pos, facing);
    }
}