using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace MultiSignpost.Blocks.EntityMultiSignPost
{
    public sealed class EditSession
    {
        public const double SavedPigmentReturnChance = 0.85;

        private ItemStack consumedStack;

        public string PlayerUid { get; }
        public int WritingColor { get; }
        public bool HasConsumedStack => consumedStack != null;
        public bool IsClosed { get; private set; }

        private EditSession(string playerUid, int writingColor, ItemStack consumedStack)
        {
            PlayerUid = playerUid;
            WritingColor = writingColor;
            this.consumedStack = consumedStack;
        }

        public static string GetPlayerUid(IPlayer player)
        {
            return player?.PlayerUID
                ?? player?.PlayerName
                ?? "";
        }

        public static EditSession Start(IPlayer player, ItemSlot hotbarSlot, int writingColor)
        {
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            if (hotbarSlot == null)
            {
                throw new ArgumentNullException(nameof(hotbarSlot));
            }

            ItemStack consumedStack = hotbarSlot.TakeOut(1);
            hotbarSlot.MarkDirty();

            return new EditSession(
                GetPlayerUid(player),
                writingColor,
                consumedStack
            );
        }

        public int ResolvePreviewColor(int savedColor)
        {
            return ResolveColor(savedColor);
        }

        public int ResolveSavedColor(int currentColor)
        {
            return ResolveColor(currentColor);
        }

        public void CompleteSave(IPlayer player, IWorldAccessor world)
        {
            TryReturnConsumedStackByChance(
                player,
                world,
                SavedPigmentReturnChance
            );

            IsClosed = true;
        }

        public void Cancel(IPlayer player)
        {
            TryReturnConsumedStack(player);
            IsClosed = true;
        }

        public void Reject(IPlayer player)
        {
            Cancel(player);
        }

        public void Discard()
        {
            consumedStack = null;
            IsClosed = true;
        }

        private int ResolveColor(int fallbackColor)
        {
            int resolvedColor = WritingColor == 0
                ? fallbackColor
                : WritingColor;

            return resolvedColor == 0
                ? ColorUtil.BlackArgb
                : resolvedColor;
        }

        private bool TryReturnConsumedStack(IPlayer player)
        {
            if (consumedStack == null)
            {
                return false;
            }

            player?.InventoryManager.TryGiveItemstack(consumedStack);
            consumedStack = null;

            return true;
        }

        private bool TryReturnConsumedStackByChance(
            IPlayer player,
            IWorldAccessor world,
            double returnChance)
        {
            if (consumedStack == null)
            {
                return false;
            }

            bool shouldReturn = world != null && world.Rand.NextDouble() < returnChance;

            if (shouldReturn)
            {
                player?.InventoryManager.TryGiveItemstack(consumedStack);
            }

            consumedStack = null;

            return shouldReturn;
        }
    }
}
