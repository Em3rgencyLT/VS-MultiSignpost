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

namespace MultiSignpost.Blocks.EntityMultiSignPost;

public class BlockEntityMultiSignPost : BlockEntity
{
    private static readonly Vec3f MeshScaleOrigin = new Vec3f(0.5f, 0f, 0.5f);

    public float SignScale => signScale;

    public readonly List<string>[] TextByDirection = TextData.CreateEmpty();

    private List<string>[] previewTextByDirection;

    private float signScale = 1f;
    private float previewScale = -1f;
    private float activeDialogMinScale = 0.1f;
    private float activeDialogMaxScale = 8f;
    private float RenderScale => previewScale > 0 ? previewScale : signScale;
    private string signFont = MultiSignpostConfig.FallbackFontName;
    private string previewFont;
    private string RenderFont => previewFont ?? signFont;

    private BlockEntityMultiSignPostRenderer signRenderer;
    private ExtensionManager extensionManager;
    private MeshFactory poleMeshFactory;
    private int color;

    private MeshData signMesh;
    private MeshData postMesh;

    private GuiDialogMultiSignPost dlg;
    private bool isBreakingWholeStructure;

    private readonly Dictionary<string, EditSession> editSessions = new Dictionary<string, EditSession>();

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);

        extensionManager = new ExtensionManager(api, Pos);
        poleMeshFactory = new MeshFactory(
            Pos,
            () => TextByDirection,
            () => signScale
        );

        signScale = ClampScale(signScale, MultiSignpostConfig.Current.MinScale, MultiSignpostConfig.Current.MaxScale);
        signFont = MultiSignpostConfig.Current.NormalizeFontName(signFont);

        if (api is ICoreClientAPI capi)
        {
            CairoFont font = CreateSignFont(signFont);

            signRenderer = new BlockEntityMultiSignPostRenderer(Pos, capi, font);
            signRenderer.SetNewText(TextByDirection, color == 0 ? ColorUtil.BlackArgb : color, RenderScale, RenderFont);

            Shape signShape = Shape.TryGet(api, new AssetLocation("game", "shapes/block/wood/signpost/sign.json"));
            if (signShape != null)
            {
                capi.Tesselator.TesselateShape(Block, signShape, out signMesh);
            }

            Shape postShape = Shape.TryGet(api, new AssetLocation("game", "shapes/block/wood/signpost/post.json"));
            if (postShape != null)
            {
                capi.Tesselator.TesselateShape(Block, postShape, out postMesh);
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

        MultiSignPostSavedState state = TreeSerializer.Read(
            tree,
            MultiSignpostConfig.Current.DefaultScale,
            MultiSignpostConfig.Current.GetDefaultFontName()
        );

        color = state.Color;
        signScale = ClampScale(state.Scale, MultiSignpostConfig.Current.MinScale, MultiSignpostConfig.Current.MaxScale);
        signFont = MultiSignpostConfig.Current.NormalizeFontName(state.Font);

        LoadSavedTextByDirection(state.TextByDirection);
        ClearPreviewText();

        signRenderer?.SetNewText(TextByDirection, color, RenderScale, RenderFont);
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);

        TreeSerializer.Write(
            tree,
            new MultiSignPostSavedState
            {
                Color = color,
                Scale = signScale,
                Font = signFont,
                TextByDirection = TextByDirection
            }
        );
    }

    private void LoadSavedTextByDirection(List<string>[] source)
    {
        for (int directionIndex = 0; directionIndex < Constants.DirectionCount; directionIndex++)
        {
            TextByDirection[directionIndex].Clear();

            if (source?[directionIndex] == null)
            {
                continue;
            }

            TextByDirection[directionIndex].AddRange(source[directionIndex]);
        }
    }

    public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
    {
        if (packetid == (int)MultiSignPostPacketId.SaveText)
        {
            EditSession editSession = GetEditSession(player);

            List<string>[] proposedText;
            float proposedScale;
            string proposedFont;

            using (MemoryStream ms = new MemoryStream(data))
            {
                BinaryReader reader = new BinaryReader(ms);

                proposedText = PacketCodec.ReadTextByDirection(reader);
                proposedScale = reader.BaseStream.Position < reader.BaseStream.Length
                    ? reader.ReadSingle()
                    : signScale;
                proposedFont = reader.BaseStream.Position < reader.BaseStream.Length
                    ? reader.ReadString()
                    : signFont;
            }

            proposedText = TextData.Normalize(proposedText);
            proposedScale = ClampScale(proposedScale, MultiSignpostConfig.Current.MinScale, MultiSignpostConfig.Current.MaxScale);
            proposedFont = MultiSignpostConfig.Current.NormalizeFontName(proposedFont);

            if (!GetExtensionManager().CanTextFitWithoutExtensionCollision(proposedText, proposedScale, MultiSignpostConfig.Current.MaxExtensions))
            {
                editSession?.Reject(player);
                RemoveEditSession(editSession);

                if (player is IServerPlayer serverPlayer)
                {
                    int requiredTotalHeightBlocks = Geometry.GetRequiredTotalHeightBlocks(
                        proposedText,
                        proposedScale
                    );

                    string message = requiredTotalHeightBlocks > MultiSignpostConfig.Current.MaxExtensions
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

            signScale = proposedScale;
            signFont = proposedFont;

            if (editSession != null)
            {
                color = editSession.ResolveSavedColor(color);
            }
            else if (color == 0)
            {
                color = ColorUtil.BlackArgb;
            }

            SyncExtensionBlocksToSavedText();

            MarkDirty(true);

            Api.World.BlockAccessor.GetChunkAtBlockPos(Pos).MarkModified();

            editSession?.CompleteSave(player, Api.World);
            RemoveEditSession(editSession);
        }

        if (packetid == (int)MultiSignPostPacketId.CancelEdit)
        {
            EditSession editSession = GetEditSession(player);

            editSession?.Cancel(player);
            RemoveEditSession(editSession);
        }
    }

    public override void OnReceivedServerPacket(int packetid, byte[] data)
    {
        if (packetid == (int)MultiSignPostPacketId.OpenDialog)
        {
            MultiSignPostOpenDialogPacket packet = PacketCodec.ReadOpenDialog(data);

            string[] serverAllowedFonts = MultiSignpostConfig.SanitizeAllowedFonts(packet.AllowedFonts);
            string currentFont = MultiSignpostConfig.NormalizeFontName(packet.CurrentFont, serverAllowedFonts);

            ReplaceSavedTextByDirection(packet.TextByDirection);
            signScale = ClampScale(packet.CurrentScale, MultiSignpostConfig.Current.MinScale, MultiSignpostConfig.Current.MaxScale);
            signFont = currentFont;
            ClearPreviewText();

            ICoreClientAPI capi = Api as ICoreClientAPI;
            CairoFont font = CreateSignFont(signFont);

            if (dlg != null && dlg.IsOpened())
            {
                return;
            }

            dlg = new GuiDialogMultiSignPost(
                packet.DialogTitle,
                Pos,
                TextByDirection,
                capi,
                font,
                (text, scale) => GetExtensionManager().CanTextFitWithoutExtensionCollision(text, scale, packet.MaxExtensions),
                packet.MaxExtensions,
                packet.MinScale,
                packet.MaxScale,
                signScale,
                signFont,
                serverAllowedFonts
            );

            dlg.OnTextChanged = DidChangeTextClientSide;

            dlg.OnCloseCancel = () =>
            {
                ClearPreviewText();
                signRenderer?.SetNewText(TextByDirection, color, RenderScale, RenderFont);
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

        if (packetid == (int)MultiSignPostPacketId.NowText)
        {
            MultiSignPostNowTextPacket packet = PacketCodec.ReadNowText(data);

            ReplaceSavedTextByDirection(packet.TextByDirection);
            ClearPreviewText();

            if (packet.Font != null)
            {
                signFont = MultiSignpostConfig.Current.NormalizeFontName(packet.Font);
            }

            signRenderer?.SetNewText(TextByDirection, color, RenderScale, RenderFont);
            MarkDirty(true);
        }
    }

    private void DidChangeTextClientSide(List<string>[] previewText, float scale, string fontName)
    {
        previewTextByDirection = TextData.Clone(previewText);
        previewScale = ClampScale(scale, activeDialogMinScale, activeDialogMaxScale);
        previewFont = string.IsNullOrWhiteSpace(fontName) ? signFont : fontName;

        signRenderer?.SetNewText(previewTextByDirection, GetPreviewColor(), RenderScale, RenderFont);
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

        signScale = ClampScale(signScale, MultiSignpostConfig.Current.MinScale, MultiSignpostConfig.Current.MaxScale);
        signFont = MultiSignpostConfig.Current.NormalizeFontName(signFont);

        BeginEditSession(byPlayer, hotbarSlot, writingColor);

        if (Api.World is IServerWorldAccessor && byPlayer is IServerPlayer serverPlayer)
        {
            byte[] data = PacketCodec.WriteOpenDialog(
                Lang.Get("multisignpost:dialog-title"),
                MultiSignpostConfig.Current.MaxExtensions,
                MultiSignpostConfig.Current.MinScale,
                MultiSignpostConfig.Current.MaxScale,
                signScale,
                TextByDirection,
                signFont,
                MultiSignpostConfig.Current.AllowedFonts
            );

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
            GetExtensionManager().RemoveOwnedExtensionBlocks();

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

        GetExtensionManager().RemoveOwnedExtensionBlocks();

        ClearPreviewText();

        signRenderer?.Dispose();
        signRenderer = null;
    }

    public override void OnBlockUnloaded()
    {
        base.OnBlockUnloaded();

        ClearPreviewText();

        signRenderer?.Dispose();
        signRenderer = null;
    }

    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
    {
        List<string>[] textToRender = GetTextForRendering();

        float scale = RenderScale;

        AddPostMesh(mesher, textToRender, scale);
        AddArrowMeshes(mesher, textToRender, scale);

        return false;
    }

    private void AddPostMesh(ITerrainMeshPool mesher, List<string>[] textToRender, float scale)
    {
        if (postMesh == null)
        {
            return;
        }

        float poleHeight = Geometry.GetRequiredVisualHeight(textToRender, scale);
        float yScale = poleHeight / Constants.VanillaPoleHeight;

        MeshData mesh = postMesh.Clone();

        mesh.Scale(MeshScaleOrigin, scale, yScale, scale);

        mesher.AddMeshData(mesh);
    }

    private void AddArrowMeshes(ITerrainMeshPool mesher, List<string>[] textToRender, float scale)
    {
        if (signMesh == null)
        {
            return;
        }

        for (int directionIndex = 0; directionIndex < Constants.DirectionCount; directionIndex++)
        {
            for (int slotIndex = 0; slotIndex < textToRender[directionIndex].Count; slotIndex++)
            {
                string text = textToRender[directionIndex][slotIndex];

                if (!TextData.IsRendered(text))
                {
                    continue;
                }

                float rotY = Directions.GetArrowRotationY(directionIndex);
                float yOffset = Geometry.GetVerticalOffset(slotIndex, scale);

                MeshData mesh = signMesh.Clone();

                mesh.Scale(MeshScaleOrigin, scale, scale, scale);
                mesh.Rotate(0, rotY * GameMath.DEG2RAD, 0);
                mesh.Translate(0, yOffset, 0);

                mesher.AddMeshData(mesh);
            }
        }
    }

    private List<string>[] GetTextForRendering()
    {
        return previewTextByDirection ?? TextByDirection;
    }

    private void ClearPreviewText()
    {
        previewTextByDirection = null;
        previewScale = -1f;
        previewFont = null;
    }

    public void SyncExtensionBlocksToSavedText()
    {
        signScale = ClampScale(signScale, MultiSignpostConfig.Current.MinScale, MultiSignpostConfig.Current.MaxScale);

        GetExtensionManager().SyncExtensionBlocksToText(
            TextByDirection,
            signScale,
            MultiSignpostConfig.Current.MaxExtensions
        );
    }

    public ExtensionManager GetExtensionManager()
    {
        if (extensionManager == null)
        {
            extensionManager = new ExtensionManager(Api, Pos);
        }

        return extensionManager;
    }

    public MeshFactory PoleMeshFactory
    {
        get
        {
            if (poleMeshFactory == null)
            {
                poleMeshFactory = new MeshFactory(
                    Pos,
                    () => TextByDirection,
                    () => signScale
                );
            }

            return poleMeshFactory;
        }
    }

    public static int CountRenderedTexts(List<string> texts)
    {
        int count = 0;

        foreach (string text in texts)
        {
            if (TextData.IsRendered(text))
            {
                count++;
            }
        }

        return count;
    }

    private static CairoFont CreateSignFont(string fontName)
    {
        return new CairoFont(
            20,
            string.IsNullOrWhiteSpace(fontName) ? MultiSignpostConfig.FallbackFontName : fontName,
            new double[] { 0, 0, 0, 0.8 }
        );
    }

    private void ReplaceSavedTextByDirection(List<string>[] source)
    {
        List<string>[] normalized = TextData.Normalize(source);

        for (int directionIndex = 0; directionIndex < Constants.DirectionCount; directionIndex++)
        {
            TextByDirection[directionIndex].Clear();
            TextByDirection[directionIndex].AddRange(normalized[directionIndex]);
        }
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
    
    private EditSession BeginEditSession(
    IPlayer player,
    ItemSlot hotbarSlot,
    int writingColor)
    {
        string playerUid = EditSession.GetPlayerUid(player);

        if (editSessions.TryGetValue(playerUid, out EditSession existingSession))
        {
            existingSession.Cancel(player);
            editSessions.Remove(playerUid);
        }

        EditSession session = EditSession.Start(
            player,
            hotbarSlot,
            writingColor
        );

        editSessions[playerUid] = session;

        return session;
    }

    private EditSession GetEditSession(IPlayer player)
    {
        string playerUid = EditSession.GetPlayerUid(player);

        editSessions.TryGetValue(playerUid, out EditSession session);

        return session;
    }

    private void RemoveEditSession(EditSession session)
    {
        if (session == null)
        {
            return;
        }

        editSessions.Remove(session.PlayerUid);
    }

    private int GetPreviewColor()
    {
        foreach (EditSession session in editSessions.Values)
        {
            return session.ResolvePreviewColor(color);
        }

        return color == 0
            ? ColorUtil.BlackArgb
            : color;
    }

    private static float ClampScale(float scale, float minScale, float maxScale)
    {
        float min = Math.Min(minScale, maxScale);
        float max = Math.Max(minScale, maxScale);

        return Math.Max(min, Math.Min(max, scale));
    }
}