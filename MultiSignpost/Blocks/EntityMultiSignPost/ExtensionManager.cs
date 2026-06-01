using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace MultiSignpost.Blocks.EntityMultiSignPost
{
    public class ExtensionManager
    {
        private readonly ICoreAPI api;
        private readonly BlockPos basePos;

        public ExtensionManager(ICoreAPI api, BlockPos basePos)
        {
            this.api = api ?? throw new ArgumentNullException(nameof(api));
            this.basePos = basePos?.Copy() ?? throw new ArgumentNullException(nameof(basePos));
        }

        public bool CanTextFitWithoutExtensionCollision(
            List<string>[] textByDirection,
            float scale,
            int maxTotalHeightBlocks)
        {
            maxTotalHeightBlocks = Math.Max(1, maxTotalHeightBlocks);

            int requiredTotalHeightBlocks =
                Geometry.GetRequiredTotalHeightBlocks(textByDirection, scale);

            if (requiredTotalHeightBlocks > maxTotalHeightBlocks)
            {
                return false;
            }

            int requiredExtraBlocks =
                Geometry.GetRequiredExtraBlocks(textByDirection, scale);

            if (requiredExtraBlocks > Constants.HardMaxManagedExtensionBlocks)
            {
                return false;
            }

            Block extensionBlock = GetExtensionBlock();
            if (extensionBlock == null)
            {
                return false;
            }

            for (int extensionIndex = 0; extensionIndex < requiredExtraBlocks; extensionIndex++)
            {
                BlockPos checkPos = GetExtensionBlockPos(extensionIndex);

                if (!IsInsideWorldHeight(checkPos))
                {
                    return false;
                }

                if (IsOwnedExtensionBlock(checkPos))
                {
                    continue;
                }

                Block block = api.World.BlockAccessor.GetBlock(checkPos);

                if (!block.IsReplacableBy(extensionBlock))
                {
                    return false;
                }
            }

            return true;
        }

        public void SyncExtensionBlocksToText(
            List<string>[] textByDirection,
            float scale,
            int maxTotalHeightBlocks)
        {
            if (api.Side != EnumAppSide.Server)
            {
                return;
            }

            Block extensionBlock = GetExtensionBlock();
            if (extensionBlock == null)
            {
                return;
            }

            int requiredExtraBlocks =
                Geometry.GetRequiredExtraBlocks(textByDirection, scale);

            int maxAllowedExtraBlocks = GetMaxAllowedExtraBlocks(maxTotalHeightBlocks);

            int blocksToPlace = Math.Min(requiredExtraBlocks, maxAllowedExtraBlocks);

            for (
                int extensionIndex = 0;
                extensionIndex < Constants.HardMaxManagedExtensionBlocks;
                extensionIndex++)
            {
                BlockPos extensionPos = GetExtensionBlockPos(extensionIndex);

                if (extensionIndex < blocksToPlace)
                {
                    EnsureExtensionBlockAt(extensionPos, extensionBlock);
                    continue;
                }

                if (IsOwnedExtensionBlock(extensionPos))
                {
                    api.World.BlockAccessor.SetBlock(0, extensionPos);
                }
            }
        }

        public void RemoveOwnedExtensionBlocks()
        {
            if (api.Side != EnumAppSide.Server)
            {
                return;
            }

            for (
                int extensionIndex = 0;
                extensionIndex < Constants.HardMaxManagedExtensionBlocks;
                extensionIndex++)
            {
                BlockPos extensionPos = GetExtensionBlockPos(extensionIndex);

                if (!IsInsideWorldHeight(extensionPos))
                {
                    continue;
                }

                if (IsOwnedExtensionBlock(extensionPos))
                {
                    api.World.BlockAccessor.SetBlock(0, extensionPos);
                }
            }
        }

        private void EnsureExtensionBlockAt(BlockPos extensionPos, Block extensionBlock)
        {
            if (!IsInsideWorldHeight(extensionPos))
            {
                return;
            }

            if (IsOwnedExtensionBlock(extensionPos))
            {
                return;
            }

            Block existingBlock = api.World.BlockAccessor.GetBlock(extensionPos);

            if (!existingBlock.IsReplacableBy(extensionBlock))
            {
                return;
            }

            api.World.BlockAccessor.SetBlock(extensionBlock.BlockId, extensionPos);

            if (api.World.BlockAccessor.GetBlockEntity(extensionPos) is BlockEntityMultiSignPostExtension extensionBe)
            {
                extensionBe.SetBasePos(basePos);
            }
        }

        private bool IsOwnedExtensionBlock(BlockPos pos)
        {
            Block block = api.World.BlockAccessor.GetBlock(pos);

            if (!(block is BlockMultiSignPostExtension))
            {
                return false;
            }

            BlockEntityMultiSignPostExtension extensionBe =
                api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityMultiSignPostExtension;

            return extensionBe != null && extensionBe.IsOwnedBy(basePos);
        }

        private Block GetExtensionBlock()
        {
            return api.World.GetBlock(Constants.ExtensionBlockCode);
        }

        private BlockPos GetExtensionBlockPos(int extensionIndex)
        {
            return basePos.UpCopy(Constants.BaseOccupiedHeightBlocks + extensionIndex);
        }

        private bool IsInsideWorldHeight(BlockPos pos)
        {
            return pos.Y >= 0 && pos.Y < api.World.BlockAccessor.MapSizeY;
        }

        private static int GetMaxAllowedExtraBlocks(int maxTotalHeightBlocks)
        {
            return Math.Max(
                0,
                Math.Min(
                    Constants.HardMaxManagedExtensionBlocks,
                    maxTotalHeightBlocks - Constants.BaseOccupiedHeightBlocks
                )
            );
        }
    }
}
