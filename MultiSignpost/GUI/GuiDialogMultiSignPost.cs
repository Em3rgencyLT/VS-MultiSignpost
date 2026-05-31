using Cairo;
using MultiSignpost.Blocks;
using MultiSignpost.Config;
using MultiSignpost.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace MultiSignpost.GUI;

public class GuiDialogMultiSignPost : GuiDialogGeneric
{
    private const double ContentWidth = 520;
    private const double InputWidth = 420;

    private const double DirectionTitleY = 20;
    private const double InputAreaY = 45;
    private const double InputAreaHeight = 300;

    private const double InputRowHeight = 30;
    private const double InputHeight = 24;

    private const int VisibleInputRows = 10;

    private readonly int maxExtensions;
    private readonly BlockPos blockEntityPos;
    private readonly CairoFont signPostFont;
    private readonly Func<List<string>[], float, bool> canSaveValidator;

    private readonly List<string>[] workingTextByDirection;

    public Action<List<string>[], float> OnTextChanged;
    public Action OnCloseCancel;

    private bool didSave;
    private bool ignoreChange;
    private bool suppressInputSync;
    private int currentDirectionIndex;
    private int firstVisibleInputIndex;

    private readonly float minScale;
    private readonly float maxScale;
    private float currentScale;

    private bool HasScaleSlider => MultiSignpostConfig.Current.HasScaleSlider();

    private static readonly string[] DirectionLabelKeys =
    {
        "multisignpost:direction-north",
        "multisignpost:direction-northeast",
        "multisignpost:direction-east",
        "multisignpost:direction-southeast",
        "multisignpost:direction-south",
        "multisignpost:direction-southwest",
        "multisignpost:direction-west",
        "multisignpost:direction-northwest"
    };

    public GuiDialogMultiSignPost(
        string dialogTitle,
        BlockPos blockEntityPos,
        List<string>[] textByDirection,
        ICoreClientAPI capi,
        CairoFont signPostFont,
        Func<List<string>[], float, bool> canSaveValidator,
        int maxExtensions,
        float minScale,
        float maxScale,
        float currentScale) : base(dialogTitle, capi)
    {
        this.maxExtensions = maxExtensions;
        this.blockEntityPos = blockEntityPos;
        this.signPostFont = signPostFont;
        this.canSaveValidator = canSaveValidator;

        this.minScale = Math.Min(minScale, maxScale);
        this.maxScale = Math.Max(minScale, maxScale);
        this.currentScale = ClampGuiScale(currentScale);

        workingTextByDirection = BlockEntityMultiSignPost.CloneTextByDirection(textByDirection);

        ComposeDialog();
    }

    private void ComposeDialog()
    {
        suppressInputSync = true;

        try
        {
            SingleComposer?.Dispose();

            ClampFirstVisibleInputIndex();

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
                .WithAlignment(EnumDialogArea.LeftTop)
                .WithFixedAlignmentOffset(230 + GuiStyle.DialogToScreenPadding, GuiStyle.DialogToScreenPadding);

            GuiTab[] tabs = CreateTabs();

            int totalInputRows = GetTotalInputRowCount();
            int visibleInputCount = GetVisibleInputCount();

            double inputListHeight = Math.Max(InputAreaHeight, totalInputRows * InputRowHeight);

            double buttonsY = InputAreaY + InputAreaHeight + 10;
            double scaleY = buttonsY + 35;
            double warningY = HasScaleSlider ? scaleY + 50 : buttonsY + 35;
            double bottomButtonsY = warningY + 55;

            ElementBounds contentBounds = ElementBounds.Fixed(
                0,
                0,
                ContentWidth,
                bottomButtonsY + 35
            );

            ElementBounds tabBounds = ElementBounds.Fixed(-210, 35, 200, 360);

            ElementBounds inputClipBounds = ElementBounds.Fixed(
                0,
                InputAreaY,
                InputWidth,
                InputAreaHeight
            );

            ElementBounds inputInsetBounds = inputClipBounds.FlatCopy().FixedGrow(3);

            ElementBounds scrollbarBounds = ElementBounds.Fixed(
                InputWidth + 8,
                InputAreaY,
                20,
                InputAreaHeight
            );

            ElementBounds scaleLabelBounds = ElementBounds.Fixed(
                0,
                scaleY,
                180,
                20
            );

            ElementBounds scaleValueBounds = ElementBounds.Fixed(
                180,
                scaleY,
                120,
                20
            );

            ElementBounds scaleSliderBounds = ElementBounds.Fixed(
                0,
                scaleY + 22,
                InputWidth,
                20
            );

            bgBounds.WithChildren(contentBounds);

            CairoFont warningFont = CairoFont.WhiteDetailText().WithColor(new double[] { 1, 0.55, 0.35, 1 });

            SingleComposer = capi.Gui
                .CreateCompo("multisignpostdialog", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(DialogTitle, OnTitleBarClose)
                .AddVerticalTabs(
                    tabs,
                    tabBounds,
                    OnTabClicked,
                    "verticalTabs"
                )
                .BeginChildElements(bgBounds)
                    .AddStaticText(
                        DirectionLabel(currentDirectionIndex),
                        CairoFont.WhiteDetailText(),
                        ElementBounds.Fixed(0, DirectionTitleY, InputWidth, 22)
                    )
                    .AddInset(inputInsetBounds, 3)
                    .BeginClip(inputClipBounds);

            for (int visibleIndex = 0; visibleIndex < visibleInputCount; visibleIndex++)
            {
                SingleComposer.AddTextInput(
                    ElementBounds.Fixed(
                        0,
                        visibleIndex * InputRowHeight,
                        InputWidth,
                        InputHeight
                    ),
                    OnTextChangedDlg,
                    CairoFont.WhiteSmallText(),
                    "text" + visibleIndex
                );
            }

            SingleComposer
                    .EndClip()
                    .AddVerticalScrollbar(OnInputScrollbarValue, scrollbarBounds, "inputScrollbar")
                    .AddSmallButton(
                        Lang.Get("multisignpost:button-add-arrow"),
                        OnButtonAddArrow,
                        ElementBounds.Fixed(0, buttonsY, 130, 24),
                        EnumButtonStyle.Normal,
                        "addArrowButton"
                    )
                    .AddSmallButton(
                        Lang.Get("multisignpost:button-remove-empty"),
                        OnButtonRemoveEmpty,
                        ElementBounds.Fixed(145, buttonsY, 150, 24),
                        EnumButtonStyle.Normal,
                        "removeEmptyButton"
                    );

            if (HasScaleSlider)
            {
                SingleComposer
                    .AddStaticText(
                        Lang.Get("multisignpost:label-scale-slider"),
                        CairoFont.WhiteDetailText(),
                        scaleLabelBounds
                    )
                    .AddDynamicText(
                        "",
                        CairoFont.WhiteDetailText(),
                        scaleValueBounds,
                        "scaleValueText"
                    )
                    .AddSlider(
                        OnScaleSliderChanged,
                        scaleSliderBounds,
                        "scaleSlider"
                    );
            }

            SingleComposer
                    .AddDynamicText(
                        "",
                        warningFont,
                        ElementBounds.Fixed(0, warningY, InputWidth, 45),
                        "warningText"
                    )
                    .AddSmallButton(
                        Lang.Get("Cancel"),
                        OnButtonCancel,
                        ElementBounds.Fixed(0, bottomButtonsY, 120, 24)
                            .WithAlignment(EnumDialogArea.LeftFixed)
                            .WithFixedPadding(10, 2),
                        EnumButtonStyle.Normal,
                        "cancelButton"
                    )
                    .AddSmallButton(
                        Lang.Get("Save"),
                        OnButtonSave,
                        ElementBounds.Fixed(InputWidth - 120, bottomButtonsY, 120, 24)
                            .WithAlignment(EnumDialogArea.LeftFixed)
                            .WithFixedPadding(10, 2),
                        EnumButtonStyle.Normal,
                        "saveButton"
                    )
                .EndChildElements()
                .Compose();

            SingleComposer.GetVerticalTab("verticalTabs").SetValue(currentDirectionIndex, false);

            SingleComposer.GetScrollbar("inputScrollbar").SetHeights(
                (float)InputAreaHeight,
                (float)inputListHeight
            );

            if (HasScaleSlider)
            {
                SingleComposer.GetSlider("scaleSlider").SetValues(
                    ScaleToSliderValue(currentScale),
                    ScaleToSliderValue(minScale),
                    ScaleToSliderValue(maxScale),
                    1
                );

                UpdateScaleDynamicText();
            }

            RefreshVisibleInputValues();
            UpdateValidationState();
        }
        finally
        {
            suppressInputSync = false;
        }
    }

    private GuiTab[] CreateTabs()
    {
        GuiTab[] tabs = new GuiTab[BlockEntityMultiSignPost.DirectionCount];

        for (int directionIndex = 0; directionIndex < BlockEntityMultiSignPost.DirectionCount; directionIndex++)
        {
            tabs[directionIndex] = new GuiTab
            {
                DataInt = directionIndex,
                Name = DirectionLabel(directionIndex)
            };
        }

        return tabs;
    }

    private int GetTotalInputRowCount()
    {
        return Math.Max(1, workingTextByDirection[currentDirectionIndex].Count);
    }

    private int GetVisibleInputCount()
    {
        int totalRows = GetTotalInputRowCount();

        return Math.Max(
            1,
            Math.Min(VisibleInputRows, totalRows - firstVisibleInputIndex)
        );
    }

    private void ClampFirstVisibleInputIndex()
    {
        int totalRows = GetTotalInputRowCount();
        int maxFirstVisibleIndex = Math.Max(0, totalRows - VisibleInputRows);

        if (firstVisibleInputIndex < 0)
        {
            firstVisibleInputIndex = 0;
        }

        if (firstVisibleInputIndex > maxFirstVisibleIndex)
        {
            firstVisibleInputIndex = maxFirstVisibleIndex;
        }
    }

    private void OnTabClicked(int index, GuiTab tab)
    {
        SyncVisibleInputsToWorkingCopy();

        currentDirectionIndex = index;
        firstVisibleInputIndex = 0;

        ComposeDialog();
        OnTextChanged?.Invoke(BlockEntityMultiSignPost.CloneTextByDirection(workingTextByDirection), currentScale);
    }

    private void OnInputScrollbarValue(float value)
    {
        if (suppressInputSync)
        {
            return;
        }

        SyncVisibleInputsToWorkingCopy();

        int newFirstVisibleInputIndex = (int)Math.Round(value / InputRowHeight);

        firstVisibleInputIndex = newFirstVisibleInputIndex;
        ClampFirstVisibleInputIndex();

        RefreshVisibleInputValues();
    }

    private void RefreshVisibleInputValues()
    {
        if (SingleComposer == null)
        {
            return;
        }

        ignoreChange = true;

        try
        {
            int visibleInputCount = GetVisibleInputCount();

            for (int visibleIndex = 0; visibleIndex < visibleInputCount; visibleIndex++)
            {
                int backingIndex = firstVisibleInputIndex + visibleIndex;

                string value = "";

                if (backingIndex < workingTextByDirection[currentDirectionIndex].Count)
                {
                    value = workingTextByDirection[currentDirectionIndex][backingIndex] ?? "";
                }

                SingleComposer.GetTextInput("text" + visibleIndex).SetValue(value);
            }
        }
        finally
        {
            ignoreChange = false;
        }
    }

    private void OnTextChangedDlg(string text)
    {
        if (ignoreChange || suppressInputSync)
        {
            return;
        }

        ignoreChange = true;

        try
        {
            ClampVisibleInputsToTextWidth();
            SyncVisibleInputsToWorkingCopy();

            OnTextChanged?.Invoke(BlockEntityMultiSignPost.CloneTextByDirection(workingTextByDirection), currentScale);

            UpdateValidationState();
        }
        finally
        {
            ignoreChange = false;
        }
    }

    private void ClampVisibleInputsToTextWidth()
    {
        ImageSurface surface = new ImageSurface(Format.Argb32, 1, 1);
        Context ctx = new Context(surface);

        signPostFont.SetupContext(ctx);

        int visibleInputCount = GetVisibleInputCount();

        for (int visibleIndex = 0; visibleIndex < visibleInputCount; visibleIndex++)
        {
            GuiElementTextInput textInput = SingleComposer.GetTextInput("text" + visibleIndex);

            string currentText = textInput.GetText() ?? "";

            int safetyCounter = 0;
            while (ctx.TextExtents(currentText).Width > 185 && currentText.Length > 0 && safetyCounter++ < 100)
            {
                currentText = currentText.Substring(0, currentText.Length - 1);
            }

            textInput.SetValue(currentText);
        }

        ctx.Dispose();
        surface.Dispose();
    }

    private void SyncVisibleInputsToWorkingCopy()
    {
        if (SingleComposer == null || suppressInputSync)
        {
            return;
        }

        int visibleInputCount = GetVisibleInputCount();

        for (int visibleIndex = 0; visibleIndex < visibleInputCount; visibleIndex++)
        {
            int backingIndex = firstVisibleInputIndex + visibleIndex;

            EnsureWorkingRowExists(backingIndex);

            GuiElementTextInput textInput = SingleComposer.GetTextInput("text" + visibleIndex);
            workingTextByDirection[currentDirectionIndex][backingIndex] = textInput.GetText() ?? "";
        }
    }

    private void EnsureWorkingRowExists(int rowIndex)
    {
        while (workingTextByDirection[currentDirectionIndex].Count <= rowIndex)
        {
            workingTextByDirection[currentDirectionIndex].Add("");
        }
    }

    private bool OnButtonAddArrow()
    {
        SyncVisibleInputsToWorkingCopy();

        workingTextByDirection[currentDirectionIndex].Add("");

        firstVisibleInputIndex = Math.Max(
            0,
            workingTextByDirection[currentDirectionIndex].Count - VisibleInputRows
        );

        ComposeDialog();

        OnTextChanged?.Invoke(BlockEntityMultiSignPost.CloneTextByDirection(workingTextByDirection), currentScale);

        return true;
    }

    private bool OnButtonRemoveEmpty()
    {
        SyncVisibleInputsToWorkingCopy();

        List<string> cleaned = new List<string>();

        foreach (string text in workingTextByDirection[currentDirectionIndex])
        {
            if (BlockEntityMultiSignPost.IsRenderedText(text))
            {
                cleaned.Add(text);
            }
        }

        workingTextByDirection[currentDirectionIndex].Clear();
        workingTextByDirection[currentDirectionIndex].AddRange(cleaned);

        firstVisibleInputIndex = 0;

        ComposeDialog();

        OnTextChanged?.Invoke(BlockEntityMultiSignPost.CloneTextByDirection(workingTextByDirection), currentScale);

        return true;
    }

    private void UpdateValidationState()
    {
        List<string>[] normalized = BlockEntityMultiSignPost.NormalizeTextByDirection(workingTextByDirection);

        int requiredTotalHeightBlocks = BlockEntityMultiSignPost.GetRequiredTotalHeightBlocks(normalized, currentScale);
        bool canSave = canSaveValidator(normalized, currentScale);

        string warningText = "";

        if (requiredTotalHeightBlocks > maxExtensions)
        {
            warningText = Lang.Get(
                "multisignpost:warning-extension-limit",
                requiredTotalHeightBlocks,
                maxExtensions
            );
        }
        else if (!canSave)
        {
            warningText = Lang.Get(
                "multisignpost:warning-not-enough-space",
                requiredTotalHeightBlocks
            );
        }
        else if (maxExtensions > 0)
        {
            warningText = Lang.Get(
                "multisignpost:warning-will-create-extensions",
                requiredTotalHeightBlocks
            );
        }

        SingleComposer.GetDynamicText("warningText").SetNewText(warningText, true);
        SingleComposer.GetButton("saveButton").Enabled = canSave;
    }

    private void OnTitleBarClose()
    {
        OnButtonCancel();
    }

    private bool OnButtonSave()
    {
        SyncVisibleInputsToWorkingCopy();

        List<string>[] normalized = BlockEntityMultiSignPost.NormalizeTextByDirection(workingTextByDirection);

        if (!canSaveValidator(normalized, currentScale))
        {
            UpdateValidationState();
            return false;
        }

        byte[] data;

        using (MemoryStream ms = new MemoryStream())
        {
            BinaryWriter writer = new BinaryWriter(ms);

            BlockEntityMultiSignPost.WriteTextByDirection(writer, normalized);
            writer.Write(currentScale);

            data = ms.ToArray();
        }

        capi.Network.SendBlockEntityPacket(blockEntityPos, (int)MultiSignPostPacketId.SaveText, data);

        didSave = true;
        TryClose();

        return true;
    }

    private bool OnButtonCancel()
    {
        TryClose();
        return true;
    }

    public override void OnGuiClosed()
    {
        if (!didSave)
        {
            OnCloseCancel?.Invoke();
        }

        base.OnGuiClosed();
    }

    private static string DirectionLabel(int directionIndex)
    {
        return Lang.Get(DirectionLabelKeys[directionIndex]);
    }

    private bool OnScaleSliderChanged(int value)
    {
        if (suppressInputSync)
        {
            return true;
        }

        currentScale = ClampGuiScale(SliderValueToScale(value));

        UpdateScaleDynamicText();
        SyncVisibleInputsToWorkingCopy();

        OnTextChanged?.Invoke(BlockEntityMultiSignPost.CloneTextByDirection(workingTextByDirection), currentScale);

        UpdateValidationState();

        return true;
    }

    private void UpdateScaleDynamicText()
    {
        if (!HasScaleSlider || SingleComposer == null)
        {
            return;
        }

        SingleComposer.GetDynamicText("scaleValueText").SetNewText(
            currentScale.ToString("0.##") + "x",
            true
        );
    }

    private int ScaleToSliderValue(float scale)
    {
        return (int)Math.Round(scale * 100f);
    }

    private float SliderValueToScale(int value)
    {
        return value / 100f;
    }

    private float ClampGuiScale(float scale)
    {
        return Math.Max(minScale, Math.Min(maxScale, scale));
    }
}