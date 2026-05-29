using Cairo;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace MultiSignpost.Blocks;

public class BlockEntityMultiSignPostRenderer : IRenderer
{
    private const int TextWidth = 200;
    private const int TextHeight = 25;

    private const float QuadWidth = 0.7f;
    private const float QuadHeight = 0.1f;

    private readonly BlockPos pos;
    private readonly ICoreClientAPI api;
    private readonly CairoFont font;
    private readonly double fontSize;

    private LoadedTexture loadedTexture;
    private MeshRef quadModelRef;

    public Matrixf ModelMat = new Matrixf();

    private List<string>[] textByDirection = BlockEntityMultiSignPost.CreateEmptyTextByDirection();

    public double RenderOrder => 0.5;

    public int RenderRange => 48;

    public BlockEntityMultiSignPostRenderer(BlockPos pos, ICoreClientAPI api, CairoFont font)
    {
        this.api = api;
        this.pos = pos;
        this.font = font;
        fontSize = font.UnscaledFontsize;

        api.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "multisignpost");
    }

    public void SetNewText(List<string>[] textByDirection, int color)
    {
        this.textByDirection = BlockEntityMultiSignPost.CloneTextByDirection(textByDirection);

        font.WithColor(ColorUtil.ToRGBADoubles(color));
        font.UnscaledFontsize = fontSize / RuntimeEnv.GUIScale;

        int lines = CountRenderedTexts(this.textByDirection);

        if (lines == 0)
        {
            loadedTexture?.Dispose();
            loadedTexture = null;

            quadModelRef?.Dispose();
            quadModelRef = null;

            return;
        }

        ImageSurface surface = new ImageSurface(Format.Argb32, TextWidth, TextHeight * lines);
        Context ctx = new Context(surface);

        font.SetupContext(ctx);

        int line = 0;

        for (int directionIndex = 0; directionIndex < BlockEntityMultiSignPost.DirectionCount; directionIndex++)
        {
            foreach (string text in this.textByDirection[directionIndex])
            {
                if (!BlockEntityMultiSignPost.IsRenderedText(text))
                {
                    continue;
                }

                double lineWidth = font.GetTextExtents(text).Width;

                ctx.MoveTo((TextWidth - lineWidth) / 2, line * TextHeight + ctx.FontExtents.Ascent);
                ctx.ShowText(text);

                line++;
            }
        }

        if (loadedTexture == null)
        {
            loadedTexture = new LoadedTexture(api);
        }

        api.Gui.LoadOrUpdateCairoTexture(surface, true, ref loadedTexture);

        surface.Dispose();
        ctx.Dispose();

        GenerateMesh();
    }

    private void GenerateMesh()
    {
        MeshData allMeshes = new MeshData(4, 6);

        int signCount = CountRenderedTexts(textByDirection);

        if (signCount == 0)
        {
            quadModelRef?.Dispose();
            quadModelRef = null;
            return;
        }

        int signNumber = 0;

        for (int directionIndex = 0; directionIndex < BlockEntityMultiSignPost.DirectionCount; directionIndex++)
        {
            for (int slotIndex = 0; slotIndex < textByDirection[directionIndex].Count; slotIndex++)
            {
                string text = textByDirection[directionIndex][slotIndex];

                if (!BlockEntityMultiSignPost.IsRenderedText(text))
                {
                    continue;
                }

                float rotY = BlockEntityMultiSignPost.GetTextRotationY(directionIndex);
                float yOffset = BlockEntityMultiSignPost.GetVerticalOffset(slotIndex);

                MeshData modelData = QuadMeshUtil.GetQuad();

                float vStart = signNumber / (float)signCount;
                float vEnd = (signNumber + 1) / (float)signCount;

                signNumber++;

                modelData.Uv = new float[]
                {
                    1, vEnd,
                    0, vEnd,
                    0, vStart,
                    1, vStart
                };

                modelData.Rgba = new byte[4 * 4];
                modelData.Rgba.Fill((byte)255);

                modelData.Translate(1.6f, 0, 0.375f);

                MeshData front = modelData.Clone();

                front.Scale(0.5f * QuadWidth, 0.4f * QuadHeight, 0.5f * QuadWidth);
                front.Rotate(0, rotY * GameMath.DEG2RAD, 0);
                front.Translate(0, 1.39f + yOffset, 0);

                allMeshes.AddMeshData(front);

                MeshData back = modelData;

                back.Uv = new float[]
                {
                    0, vEnd,
                    1, vEnd,
                    1, vStart,
                    0, vStart
                };

                back.Translate(0, 0, 0.26f);
                back.Scale(0.5f * QuadWidth, 0.4f * QuadHeight, 0.5f * QuadWidth);
                back.Rotate(0, rotY * GameMath.DEG2RAD, 0);
                back.Translate(0, 1.39f + yOffset, 0);

                allMeshes.AddMeshData(back);
            }
        }

        quadModelRef?.Dispose();
        quadModelRef = api.Render.UploadMesh(allMeshes);
    }

    private static int CountRenderedTexts(List<string>[] textByDirection)
    {
        int count = 0;

        for (int directionIndex = 0; directionIndex < BlockEntityMultiSignPost.DirectionCount; directionIndex++)
        {
            count += BlockEntityMultiSignPost.CountRenderedTexts(textByDirection[directionIndex]);
        }

        return count;
    }

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (loadedTexture == null || quadModelRef == null)
        {
            return;
        }

        IRenderAPI rpi = api.Render;
        Vec3d camPos = api.World.Player.Entity.CameraPos;

        rpi.GlDisableCullFace();
        rpi.GlToggleBlend(true, EnumBlendMode.PremultipliedAlpha);

        IStandardShaderProgram prog = rpi.PreparedStandardShader(pos.X, pos.Y, pos.Z);

        prog.Tex2D = loadedTexture.TextureId;
        prog.ModelMatrix = ModelMat
            .Identity()
            .Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z)
            .Values;

        prog.ViewMatrix = rpi.CameraMatrixOriginf;
        prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
        prog.NormalShaded = 0;
        prog.ExtraGodray = 0;
        prog.SsaoAttn = 0;
        prog.AlphaTest = 0.05f;
        prog.OverlayOpacity = 0;

        rpi.RenderMesh(quadModelRef);

        prog.Stop();

        rpi.GlToggleBlend(true, EnumBlendMode.Standard);
    }

    public void Dispose()
    {
        api.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);

        loadedTexture?.Dispose();
        quadModelRef?.Dispose();
    }
}