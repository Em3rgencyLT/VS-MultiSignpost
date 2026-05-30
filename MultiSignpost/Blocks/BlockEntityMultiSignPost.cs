using MultiSignpost.Config;
using MultiSignpost.Enums;
using MultiSignpost.GUI;
using System;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace MultiSignpost.Blocks;

public class BlockEntityMultiSignPost : BlockEntity
{
    public const int DirectionCount = 8;

    public const int BaseOccupiedHeightBlocks = 2;
    public const int BaseFreeArrowCount = 5;
    public const int ArrowsPerExtraBlock = 5;

    public const int HardMaxManagedExtensionBlocks = 128;

    public static readonly AssetLocation ExtensionBlockCode =
        new AssetLocation("multisignpost", "multisignpost-extension");

    public readonly List<string>[] TextByDirection = CreateEmptyTextByDirection();

    private List<string>[] previewTextByDirection;

    private BlockEntityMultiSignPostRenderer signRenderer;
    private int color;
    private int tempColor;
    private ItemStack tempStack;

    private MeshData signMesh;

    private GuiDialogMultiSignPost dlg;
    private bool isBreakingWholeStructure;

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);

        if (api is ICoreClientAPI capi)
        {
            CairoFont font = new CairoFont(20, GuiStyle.StandardFontName, new double[] { 0, 0, 0, 0.8 });

            signRenderer = new BlockEntityMultiSignPostRenderer(Pos, capi, font);
            signRenderer.SetNewText(TextByDirection, color == 0 ? ColorUtil.BlackArgb : color);

            Shape signShape = Shape.TryGet(api, new AssetLocation("game", "shapes/block/wood/signpost/sign.json"));
            if (signShape != null)
            {
                capi.Tesselator.TesselateShape(Block, signShape, out signMesh);
            }
        }

        if (api.Side == EnumAppSide.Server)
        {
            api.Event.EnqueueMainThreadTask(SyncExtensionBlocksToSavedText, "sync-multisignpost-extension-blocks");
        }
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);

        color = tree.GetInt("color");
        if (color == 0)
        {
            color = ColorUtil.BlackArgb;
        }

        ReadFromTree(tree);
        ClearPreviewText();

        signRenderer?.SetNewText(TextByDirection, color);
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);

        tree.SetInt("color", color);

        List<string>[] normalized = NormalizeTextByDirection(TextByDirection);

        for (int directionIndex = 0; directionIndex < DirectionCount; directionIndex++)
        {
            tree.SetInt("dir" + directionIndex + "Count", normalized[directionIndex].Count);

            for (int slotIndex = 0; slotIndex < normalized[directionIndex].Count; slotIndex++)
            {
                tree.SetString("dir" + directionIndex + "text" + slotIndex, normalized[directionIndex][slotIndex]);
            }
        }
    }

    private void ReadFromTree(ITreeAttribute tree)
    {
        for (int directionIndex = 0; directionIndex < DirectionCount; directionIndex++)
        {
            TextByDirection[directionIndex].Clear();

            int count = tree.GetInt("dir" + directionIndex + "Count", 0);

            for (int slotIndex = 0; slotIndex < count; slotIndex++)
            {
                string text = tree.GetString("dir" + directionIndex + "text" + slotIndex, "");
                TextByDirection[directionIndex].Add(text ?? "");
            }
        }
    }

    public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
    {
        if (packetid == (int)MultiSignPostPacketId.SaveText)
        {
            List<string>[] proposedText;

            using (MemoryStream ms = new MemoryStream(data))
            {
                BinaryReader reader = new BinaryReader(ms);
                proposedText = ReadTextByDirection(reader);
            }

            proposedText = NormalizeTextByDirection(proposedText);

            if (!CanTextFitWithoutExtensionCollision(proposedText))
            {
                if (tempStack != null)
                {
                    player.InventoryManager.TryGiveItemstack(tempStack);
                    tempStack = null;
                }

                if (player is IServerPlayer serverPlayer)
                {
                    int requiredExtraBlocks = GetRequiredExtraBlocks(proposedText);

                    string message = requiredExtraBlocks > MultiSignpostConfig.Current.MaxExtensions
                        ? Lang.Get("multisignpost:chat-extension-limit")
                        : Lang.Get("multisignpost:chat-not-enough-space");

                    serverPlayer.SendMessage(
                        GlobalConstants.GeneralChatGroup,
                        message,
                        EnumChatType.Notification
                    );
                }

                return;
            }

            ReplaceSavedTextByDirection(proposedText);
            ClearPreviewText();

            color = tempColor == 0 ? color : tempColor;
            if (color == 0)
            {
                color = ColorUtil.BlackArgb;
            }

            SyncExtensionBlocksToSavedText();

            MarkDirty(true);

            Api.World.BlockAccessor.GetChunkAtBlockPos(Pos).MarkModified();

            if (tempStack != null && Api.World.Rand.NextDouble() < 0.85)
            {
                player.InventoryManager.TryGiveItemstack(tempStack);
            }

            tempStack = null;
        }

        if (packetid == (int)MultiSignPostPacketId.CancelEdit && tempStack != null)
        {
            player.InventoryManager.TryGiveItemstack(tempStack);
            tempStack = null;
        }
    }

    public override void OnReceivedServerPacket(int packetid, byte[] data)
    {
        if (packetid == (int)MultiSignPostPacketId.OpenDialog)
        {
            using (MemoryStream ms = new MemoryStream(data))
            {
                BinaryReader reader = new BinaryReader(ms);

                string dialogTitle = reader.ReadString();
                int serverMaxExtensions = reader.ReadInt32();
                List<string>[] textByDirection = ReadTextByDirection(reader);

                ReplaceSavedTextByDirection(textByDirection);
                ClearPreviewText();

                ICoreClientAPI capi = Api as ICoreClientAPI;
                CairoFont font = new CairoFont(20, GuiStyle.StandardFontName, new double[] { 0, 0, 0, 0.8 });

                if (dlg != null && dlg.IsOpened())
                {
                    return;
                }

                dlg = new GuiDialogMultiSignPost(
                    dialogTitle,
                    Pos,
                    TextByDirection,
                    capi,
                    font,
                    text => CanTextFitWithoutExtensionCollision(text, serverMaxExtensions),
                    serverMaxExtensions
                );

                dlg.OnTextChanged = DidChangeTextClientSide;

                dlg.OnCloseCancel = () =>
                {
                    ClearPreviewText();
                    signRenderer?.SetNewText(TextByDirection, color);
                    MarkDirty(true);

                    capi.Network.SendBlockEntityPacket(Pos, (int)MultiSignPostPacketId.CancelEdit, null);
                };

                dlg.OnClosed += () =>
                {
                    dlg.Dispose();
                    dlg = null;
                };

                dlg.TryOpen();
            }
        }

        if (packetid == (int)MultiSignPostPacketId.NowText)
        {
            using (MemoryStream ms = new MemoryStream(data))
            {
                BinaryReader reader = new BinaryReader(ms);
                List<string>[] textByDirection = ReadTextByDirection(reader);

                ReplaceSavedTextByDirection(textByDirection);
                ClearPreviewText();

                signRenderer?.SetNewText(TextByDirection, color);
                MarkDirty(true);
            }
        }
    }

    private void DidChangeTextClientSide(List<string>[] previewText)
    {
        previewTextByDirection = CloneTextByDirection(previewText);

        signRenderer?.SetNewText(previewTextByDirection, tempColor == 0 ? color : tempColor);
        MarkDirty(true);
    }

    public void OnRightClick(IPlayer byPlayer)
    {
        if (byPlayer?.Entity?.Controls?.ShiftKey != true)
        {
            return;
        }

        ItemSlot hotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

        if (!TryGetWritingColor(hotbarSlot, out int writingColor))
        {
            return;
        }

        tempColor = writingColor;
        tempStack = hotbarSlot.TakeOut(1);
        hotbarSlot.MarkDirty();

        if (Api.World is IServerWorldAccessor && byPlayer is IServerPlayer serverPlayer)
        {
            byte[] data;

            using (MemoryStream ms = new MemoryStream())
            {
                BinaryWriter writer = new BinaryWriter(ms);

                writer.Write(Lang.Get("multisignpost:dialog-title"));
                writer.Write(MultiSignpostConfig.Current.MaxExtensions);
                WriteTextByDirection(writer, TextByDirection);

                data = ms.ToArray();
            }

            ((ICoreServerAPI)Api).Network.SendBlockEntityPacket(
                serverPlayer,
                Pos,
                (int)MultiSignPostPacketId.OpenDialog,
                data
            );
        }
    }

    public void BreakEntireSignpost(IPlayer byPlayer, float dropQuantityMultiplier = 1)
    {
        if (isBreakingWholeStructure)
        {
            return;
        }

        isBreakingWholeStructure = true;

        try
        {
            RemoveOwnedExtensionBlocks();

            Block block = Api.World.BlockAccessor.GetBlock(Pos);
            if (block.Id != 0)
            {
                block.OnBlockBroken(Api.World, Pos, byPlayer, dropQuantityMultiplier);
            }
        }
        finally
        {
            isBreakingWholeStructure = false;
        }
    }

    public override void OnBlockRemoved()
    {
        base.OnBlockRemoved();

        RemoveOwnedExtensionBlocks();

        signRenderer?.Dispose();
        signRenderer = null;
    }

    public override void OnBlockUnloaded()
    {
        base.OnBlockUnloaded();

        signRenderer?.Dispose();
        signRenderer = null;
    }

    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
    {
        List<string>[] textToRender = GetTextForRendering();

        if (signMesh == null)
        {
            return false;
        }

        for (int directionIndex = 0; directionIndex < DirectionCount; directionIndex++)
        {
            for (int slotIndex = 0; slotIndex < textToRender[directionIndex].Count; slotIndex++)
            {
                string text = textToRender[directionIndex][slotIndex];

                if (!IsRenderedText(text))
                {
                    continue;
                }

                float rotY = GetArrowRotationY(directionIndex);
                float yOffset = GetVerticalOffset(slotIndex);

                MeshData mesh = signMesh.Clone();
                mesh.Rotate(0, rotY * GameMath.DEG2RAD, 0);
                mesh.Translate(0, yOffset, 0);

                mesher.AddMeshData(mesh);
            }
        }

        return false;
    }

    private List<string>[] GetTextForRendering()
    {
        return previewTextByDirection ?? TextByDirection;
    }

    private void ClearPreviewText()
    {
        previewTextByDirection = null;
    }

    public bool CanTextFitWithoutExtensionCollision(List<string>[] textByDirection)
    {
        return CanTextFitWithoutExtensionCollision(
            textByDirection,
            MultiSignpostConfig.Current.MaxExtensions
        );
    }

    public bool CanTextFitWithoutExtensionCollision(List<string>[] textByDirection, int maxExtensions)
    {
        int requiredExtraBlocks = GetRequiredExtraBlocks(textByDirection);

        if (requiredExtraBlocks > maxExtensions)
        {
            return false;
        }

        if (requiredExtraBlocks > HardMaxManagedExtensionBlocks)
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

            Block block = Api.World.BlockAccessor.GetBlock(checkPos);

            if (!block.IsReplacableBy(extensionBlock))
            {
                return false;
            }
        }

        return true;
    }

    public void SyncExtensionBlocksToSavedText()
    {
        if (Api?.Side != EnumAppSide.Server)
        {
            return;
        }

        Block extensionBlock = GetExtensionBlock();
        if (extensionBlock == null)
        {
            return;
        }

        int requiredExtraBlocks = GetRequiredExtraBlocks(TextByDirection);
        int maxExtensions = MultiSignpostConfig.Current.MaxExtensions;

        int blocksToPlace = Math.Min(requiredExtraBlocks, maxExtensions);

        for (int extensionIndex = 0; extensionIndex < HardMaxManagedExtensionBlocks; extensionIndex++)
        {
            BlockPos extensionPos = GetExtensionBlockPos(extensionIndex);

            if (extensionIndex < blocksToPlace)
            {
                EnsureExtensionBlockAt(extensionPos, extensionBlock);
                continue;
            }

            if (IsOwnedExtensionBlock(extensionPos))
            {
                Api.World.BlockAccessor.SetBlock(0, extensionPos);
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

        Block existingBlock = Api.World.BlockAccessor.GetBlock(extensionPos);

        if (!existingBlock.IsReplacableBy(extensionBlock))
        {
            return;
        }

        Api.World.BlockAccessor.SetBlock(extensionBlock.BlockId, extensionPos);

        if (Api.World.BlockAccessor.GetBlockEntity(extensionPos) is BlockEntityMultiSignPostExtension extensionBe)
        {
            extensionBe.SetBasePos(Pos);
        }
    }

    private void RemoveOwnedExtensionBlocks()
    {
        if (Api?.Side != EnumAppSide.Server)
        {
            return;
        }

        for (int extensionIndex = 0; extensionIndex < HardMaxManagedExtensionBlocks; extensionIndex++)
        {
            BlockPos extensionPos = GetExtensionBlockPos(extensionIndex);

            if (!IsInsideWorldHeight(extensionPos))
            {
                continue;
            }

            if (IsOwnedExtensionBlock(extensionPos))
            {
                Api.World.BlockAccessor.SetBlock(0, extensionPos);
            }
        }
    }

    private bool IsOwnedExtensionBlock(BlockPos pos)
    {
        Block block = Api.World.BlockAccessor.GetBlock(pos);

        if (!(block is BlockMultiSignPostExtension))
        {
            return false;
        }

        BlockEntityMultiSignPostExtension extensionBe =
            Api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityMultiSignPostExtension;

        return extensionBe != null && extensionBe.IsOwnedBy(Pos);
    }

    private Block GetExtensionBlock()
    {
        return Api.World.GetBlock(ExtensionBlockCode);
    }

    public static int GetRequiredExtraBlocks(List<string>[] textByDirection)
    {
        int highestRenderedSlotCount = GetHighestRenderedSlotCount(textByDirection);

        if (highestRenderedSlotCount <= BaseFreeArrowCount)
        {
            return 0;
        }

        return (int)Math.Ceiling((highestRenderedSlotCount - BaseFreeArrowCount) / (double)ArrowsPerExtraBlock);
    }

    public static int GetHighestRenderedSlotCount(List<string>[] textByDirection)
    {
        int max = 0;

        for (int directionIndex = 0; directionIndex < DirectionCount; directionIndex++)
        {
            List<string> texts = textByDirection[directionIndex];

            for (int slotIndex = 0; slotIndex < texts.Count; slotIndex++)
            {
                if (IsRenderedText(texts[slotIndex]))
                {
                    max = Math.Max(max, slotIndex + 1);
                }
            }
        }

        return max;
    }

    public static int CountRenderedTexts(List<string> texts)
    {
        int count = 0;

        foreach (string text in texts)
        {
            if (IsRenderedText(text))
            {
                count++;
            }
        }

        return count;
    }

    public static bool IsRenderedText(string text)
    {
        return !string.IsNullOrWhiteSpace(text);
    }

    public static float GetVerticalOffset(int slotIndex)
    {
        return (slotIndex - 4) * 0.2f;
    }

    public static float GetArrowRotationY(int directionIndex)
    {
        switch (directionIndex)
        {
            case 0: return 180; // North
            case 1: return 135; // Northeast
            case 2: return 90;  // East
            case 3: return 45;  // Southeast
            case 4: return 0;   // South
            case 5: return 315; // Southwest
            case 6: return 270; // West
            case 7: return 225; // Northwest
            default: return 0;
        }
    }

    public static float GetTextRotationY(int directionIndex)
    {
        switch (directionIndex)
        {
            case 0: return 90;  // North
            case 1: return 45;  // Northeast
            case 2: return 0;   // East
            case 3: return 315; // Southeast
            case 4: return 270; // South
            case 5: return 225; // Southwest
            case 6: return 180; // West
            case 7: return 135; // Northwest
            default: return 0;
        }
    }

    public static List<string>[] CreateEmptyTextByDirection()
    {
        List<string>[] result = new List<string>[DirectionCount];

        for (int directionIndex = 0; directionIndex < DirectionCount; directionIndex++)
        {
            result[directionIndex] = new List<string>();
        }

        return result;
    }

    public static List<string>[] CloneTextByDirection(List<string>[] source)
    {
        List<string>[] result = CreateEmptyTextByDirection();

        for (int directionIndex = 0; directionIndex < DirectionCount; directionIndex++)
        {
            if (source?[directionIndex] == null)
            {
                continue;
            }

            result[directionIndex].AddRange(source[directionIndex]);
        }

        return result;
    }

    public static List<string>[] NormalizeTextByDirection(List<string>[] source)
    {
        List<string>[] result = CreateEmptyTextByDirection();

        for (int directionIndex = 0; directionIndex < DirectionCount; directionIndex++)
        {
            if (source?[directionIndex] == null)
            {
                continue;
            }

            List<string> texts = new List<string>();

            foreach (string text in source[directionIndex])
            {
                texts.Add(text ?? "");
            }

            while (texts.Count > 0 && !IsRenderedText(texts[texts.Count - 1]))
            {
                texts.RemoveAt(texts.Count - 1);
            }

            result[directionIndex].AddRange(texts);
        }

        return result;
    }

    private void ReplaceSavedTextByDirection(List<string>[] source)
    {
        List<string>[] normalized = NormalizeTextByDirection(source);

        for (int directionIndex = 0; directionIndex < DirectionCount; directionIndex++)
        {
            TextByDirection[directionIndex].Clear();
            TextByDirection[directionIndex].AddRange(normalized[directionIndex]);
        }
    }

    public static void WriteTextByDirection(BinaryWriter writer, List<string>[] textByDirection)
    {
        List<string>[] normalized = NormalizeTextByDirection(textByDirection);

        for (int directionIndex = 0; directionIndex < DirectionCount; directionIndex++)
        {
            writer.Write(normalized[directionIndex].Count);

            foreach (string text in normalized[directionIndex])
            {
                writer.Write(text ?? "");
            }
        }
    }

    public static List<string>[] ReadTextByDirection(BinaryReader reader)
    {
        List<string>[] result = CreateEmptyTextByDirection();

        for (int directionIndex = 0; directionIndex < DirectionCount; directionIndex++)
        {
            int count = reader.ReadInt32();

            for (int slotIndex = 0; slotIndex < count; slotIndex++)
            {
                string text = reader.ReadString();
                result[directionIndex].Add(text ?? "");
            }
        }

        return result;
    }

    private BlockPos GetExtensionBlockPos(int extensionIndex)
    {
        return Pos.UpCopy(BaseOccupiedHeightBlocks + extensionIndex);
    }

    private bool IsInsideWorldHeight(BlockPos pos)
    {
        return pos.Y >= 0 && pos.Y < Api.World.BlockAccessor.MapSizeY;
    }

    private static bool TryGetWritingColor(ItemSlot hotbarSlot, out int writingColor)
    {
        writingColor = ColorUtil.BlackArgb;

        ItemStack stack = hotbarSlot?.Itemstack;
        if (stack == null)
        {
            return false;
        }

        JsonObject colorJson = stack.ItemAttributes?["pigment"]?["color"];

        if (colorJson?.Exists == true)
        {
            int r = colorJson["red"].AsInt();
            int g = colorJson["green"].AsInt();
            int b = colorJson["blue"].AsInt();

            writingColor = ColorUtil.ToRgba(255, r, g, b);
            return true;
        }

        return false;
    }
}