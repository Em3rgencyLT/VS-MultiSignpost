using System;
using System.Collections.Generic;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace MultiSignpost.Blocks.EntityMultiSignPost
{
    public sealed class MultiSignPostSavedState
    {
        public int Color { get; init; }
        public float Scale { get; init; }
        public string Font { get; init; }
        public List<string>[] TextByDirection { get; init; }
    }

    public static class TreeSerializer
    {
        private const string ColorKey = "color";
        private const string ScaleKey = "signScale";
        private const string FontKey = "signFont";

        public static MultiSignPostSavedState Read(
            ITreeAttribute tree,
            float defaultScale,
            string defaultFont)
        {
            if (tree == null)
            {
                throw new ArgumentNullException(nameof(tree));
            }

            return new MultiSignPostSavedState
            {
                Color = ReadColor(tree),
                Scale = tree.GetFloat(ScaleKey, defaultScale),
                Font = tree.GetString(FontKey, defaultFont),
                TextByDirection = ReadTextByDirection(tree)
            };
        }

        public static void Write(ITreeAttribute tree, MultiSignPostSavedState state)
        {
            if (tree == null)
            {
                throw new ArgumentNullException(nameof(tree));
            }

            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            tree.SetInt(ColorKey, NormalizeColor(state.Color));
            tree.SetFloat(ScaleKey, state.Scale);
            tree.SetString(FontKey, state.Font ?? "");

            WriteTextByDirection(tree, state.TextByDirection);
        }

        private static int ReadColor(ITreeAttribute tree)
        {
            return NormalizeColor(tree.GetInt(ColorKey));
        }

        private static int NormalizeColor(int color)
        {
            return color == 0 ? ColorUtil.BlackArgb : color;
        }

        private static List<string>[] ReadTextByDirection(ITreeAttribute tree)
        {
            List<string>[] result = TextData.CreateEmpty();

            for (int directionIndex = 0; directionIndex < Constants.DirectionCount; directionIndex++)
            {
                int count = tree.GetInt(DirectionCountKey(directionIndex), 0);

                for (int slotIndex = 0; slotIndex < count; slotIndex++)
                {
                    string text = tree.GetString(DirectionTextKey(directionIndex, slotIndex), "");
                    result[directionIndex].Add(text ?? "");
                }
            }

            return result;
        }

        private static void WriteTextByDirection(ITreeAttribute tree, List<string>[] textByDirection)
        {
            List<string>[] normalized = TextData.Normalize(textByDirection);

            for (int directionIndex = 0; directionIndex < Constants.DirectionCount; directionIndex++)
            {
                tree.SetInt(DirectionCountKey(directionIndex), normalized[directionIndex].Count);

                for (int slotIndex = 0; slotIndex < normalized[directionIndex].Count; slotIndex++)
                {
                    tree.SetString(
                        DirectionTextKey(directionIndex, slotIndex),
                        normalized[directionIndex][slotIndex]
                    );
                }
            }
        }

        private static string DirectionCountKey(int directionIndex)
        {
            return "dir" + directionIndex + "Count";
        }

        private static string DirectionTextKey(int directionIndex, int slotIndex)
        {
            return "dir" + directionIndex + "text" + slotIndex;
        }
    }
}
