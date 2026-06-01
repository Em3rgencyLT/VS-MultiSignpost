using System;
using System.Collections.Generic;
using Vintagestory.API.MathTools;

namespace MultiSignpost.Blocks.EntityMultiSignPost
{
    public static class Geometry
    {
        public static int GetRequiredTotalHeightBlocks(List<string>[] textByDirection, float scale)
        {
            float requiredVisualHeight = GetRequiredVisualHeight(textByDirection, scale);

            return Math.Max(
                1,
                (int)Math.Ceiling(requiredVisualHeight - Constants.HeightEpsilon)
            );
        }

        public static int GetRequiredExtraBlocks(List<string>[] textByDirection, float scale)
        {
            int totalBlocksNeeded = GetRequiredTotalHeightBlocks(textByDirection, scale);

            return Math.Max(
                0,
                totalBlocksNeeded - Constants.BaseOccupiedHeightBlocks
            );
        }

        public static float GetRequiredVisualHeight(List<string>[] textByDirection, float scale)
        {
            scale = Math.Max(0.01f, scale);

            int highestRenderedSlotCount = GetHighestRenderedSlotCount(textByDirection);

            if (highestRenderedSlotCount <= 0)
            {
                return GetScaledPoleHeight(scale);
            }

            return GetHighestArrowTop(textByDirection, scale) + Constants.PoleTopPadding * scale;
        }

        public static float GetVerticalOffset(int slotIndex, float scale = 1f)
        {
            return GetUnscaledVerticalOffset(slotIndex) * Math.Max(0.01f, scale);
        }

        public static int GetMaxAllowedExtraBlocks(int maxTotalHeightBlocks)
        {
            return Math.Max(
                0,
                Math.Min(Constants.HardMaxManagedExtensionBlocks, maxTotalHeightBlocks - Constants.BaseOccupiedHeightBlocks)
            );
        }

        public static Cuboidf[] CreatePoleBoxes(float scale, float visualHeight, float segmentStartY, float segmentHeight)
        {
            scale = Math.Max(0.01f, scale);

            float localY2 = Math.Min(segmentHeight, visualHeight - segmentStartY);

            if (localY2 <= Constants.HeightEpsilon)
            {
                return new Cuboidf[0];
            }

            float halfWidth = Constants.PoleHalfWidth * scale;

            halfWidth = Math.Max(0.03125f, Math.Min(0.5f, halfWidth));

            return new[]
            {
                new Cuboidf(
                    0.5f - halfWidth,
                    0f,
                    0.5f - halfWidth,
                    0.5f + halfWidth,
                    localY2,
                    0.5f + halfWidth
                )
            };
        }

        public static Cuboidf[] InflateSelectionBoxes(Cuboidf[] boxes)
        {
            if (boxes == null || boxes.Length == 0)
            {
                return boxes;
            }

            const float inflate = 0.002f;

            Cuboidf[] result = new Cuboidf[boxes.Length];

            for (int i = 0; i < boxes.Length; i++)
            {
                Cuboidf box = boxes[i];

                result[i] = new Cuboidf(
                    Math.Max(-inflate, box.X1 - inflate),
                    Math.Max(-inflate, box.Y1 - inflate),
                    Math.Max(-inflate, box.Z1 - inflate),
                    Math.Min(1f + inflate, box.X2 + inflate),
                    Math.Min(1f + inflate, box.Y2 + inflate),
                    Math.Min(1f + inflate, box.Z2 + inflate)
                );
            }

            return result;
        }

        public static Cuboidf FirstOrDefaultBox(Cuboidf[] boxes)
        {
            if (boxes != null && boxes.Length > 0)
            {
                return boxes[0];
            }

            return new Cuboidf(0.45f, 0f, 0.45f, 0.55f, 0.1f, 0.55f);
        }

        private static int GetHighestRenderedSlotCount(List<string>[] textByDirection)
        {
            int max = 0;

            for (int directionIndex = 0; directionIndex < Constants.DirectionCount; directionIndex++)
            {
                List<string> texts = textByDirection[directionIndex];

                for (int slotIndex = 0; slotIndex < texts.Count; slotIndex++)
                {
                    if (TextData.IsRendered(texts[slotIndex]))
                    {
                        max = Math.Max(max, slotIndex + 1);
                    }
                }
            }

            return max;
        }

        private static float GetUnscaledVerticalOffset(int slotIndex)
        {
            return (slotIndex - 4) * 0.2f;
        }

        private static float GetScaledPoleHeight(float scale)
        {
            return Constants.VanillaPoleHeight * Math.Max(0.01f, scale);
        }

        private static float GetHighestArrowTop(List<string>[] textByDirection, float scale)
        {
            int highestRenderedSlotCount = GetHighestRenderedSlotCount(textByDirection);

            if (highestRenderedSlotCount <= 0)
            {
                return 0f;
            }

            int highestSlotIndex = highestRenderedSlotCount - 1;

            return GetArrowTop(highestSlotIndex, scale);
        }

        private static float GetArrowTop(int slotIndex, float scale)
        {
            scale = Math.Max(0.01f, scale);

            return (Constants.VanillaArrowTopY + GetUnscaledVerticalOffset(slotIndex)) * scale;
        }
    }
}
