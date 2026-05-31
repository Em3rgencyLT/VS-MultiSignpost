using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace MultiSignpost.Blocks;

public class BlockMultiSignPost : Block
{
    private WorldInteraction[] interactions = new WorldInteraction[0];

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);

        PlacedPriorityInteract = true;

        if (api.Side != EnumAppSide.Client)
        {
            return;
        }

        interactions = ObjectCacheUtil.GetOrCreate(api, "multiSignPostInteractions", () =>
        {
            List<ItemStack> pigmentStacks = new List<ItemStack>();

            foreach (CollectibleObject collectible in api.World.Collectibles)
            {
                if (collectible.Attributes?["pigment"].Exists == true)
                {
                    pigmentStacks.Add(new ItemStack(collectible));
                }
            }

            return new[]
            {
                new WorldInteraction
                {
                    ActionLangCode = "blockhelp-sign-write",
                    HotKeyCode = "shift",
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = pigmentStacks.ToArray()
                }
            };
        });
    }

    public override bool TryPlaceBlock(
        IWorldAccessor world,
        IPlayer byPlayer,
        ItemStack itemstack,
        BlockSelection bs,
        ref string failureCode)
    {
        if (!CanPlaceBlock(world, byPlayer, bs, ref failureCode))
        {
            return false;
        }

        BlockPos supportingPos = bs.Position.DownCopy();
        Block supportingBlock = world.BlockAccessor.GetBlock(supportingPos);

        if (supportingBlock.CanAttachBlockAt(world.BlockAccessor, this, bs.Position, bs.Face)
            || supportingBlock.GetAttributes(world.BlockAccessor, bs.Position)?.IsTrue("partialAttachable") == true)
        {
            world.BlockAccessor.SetBlock(BlockId, bs.Position);

            if (world.BlockAccessor.GetBlockEntity(bs.Position) is BlockEntityMultiSignPost be)
            {
                be.SyncExtensionBlocksToSavedText();
            }

            return true;
        }

        return false;
    }

    public override void OnBlockPlaced(IWorldAccessor world, BlockPos pos, ItemStack byItemStack = null)
    {
        base.OnBlockPlaced(world, pos, byItemStack);

        if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityMultiSignPost be)
        {
            be.SyncExtensionBlocksToSavedText();
        }
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        BlockEntity be = world.BlockAccessor.GetBlockEntity(blockSel.Position);

        if (be is BlockEntityMultiSignPost signPostBe)
        {
            signPostBe.OnRightClick(byPlayer);
            return true;
        }

        return true;
    }

    public override AssetLocation GetHorizontallyFlippedBlockCode(EnumAxis axis)
    {
        BlockFacing facing = BlockFacing.FromCode(LastCodePart());

        if (facing != null && facing.Axis == axis)
        {
            return CodeWithParts(facing.Opposite.Code);
        }

        return Code;
    }

    public override WorldInteraction[] GetPlacedBlockInteractionHelp(
        IWorldAccessor world,
        BlockSelection selection,
        IPlayer forPlayer)
    {
        return interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
    }

    public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
    {
        if (blockAccessor.GetBlockEntity(pos) is BlockEntityMultiSignPost be)
        {
            return be.GetBasePoleBoxes();
        }

        return base.GetCollisionBoxes(blockAccessor, pos);
    }

    public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
    {
        if (blockAccessor.GetBlockEntity(pos) is BlockEntityMultiSignPost be)
        {
            return be.GetBasePoleSelectionBoxes();
        }

        return base.GetSelectionBoxes(blockAccessor, pos);
    }

    public override Cuboidf GetParticleBreakBox(IBlockAccessor blockAccess, BlockPos pos, BlockFacing facing)
    {
        if (blockAccess.GetBlockEntity(pos) is BlockEntityMultiSignPost be)
        {
            return be.GetBasePoleParticleBreakBox();
        }

        return base.GetParticleBreakBox(blockAccess, pos, facing);
    }

    public override void GetDecal(
        IWorldAccessor world,
        BlockPos pos,
        ITexPositionSource decalTexSource,
        ref MeshData decalModelData,
        ref MeshData blockModelData)
    {
        if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityMultiSignPost be)
        {
            blockModelData = be.GetBasePoleBlockModelMesh();
            decalModelData = be.GetBasePoleDecalMesh(decalTexSource);
            return;
        }

        base.GetDecal(world, pos, decalTexSource, ref decalModelData, ref blockModelData);
    }
}