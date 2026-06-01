using Vintagestory.API.Common;

namespace MultiSignpost.Blocks.EntityMultiSignPost
{
    public static class Constants
    {
        public const int DirectionCount = 8;
        public const int BaseOccupiedHeightBlocks = 1;
        public const int HardMaxManagedExtensionBlocks = 128;

        public const float VanillaPoleHeight = 2f;
        public const float VanillaArrowTopY = 31.5f / 16f;
        public const float PoleHalfWidth = 1f / 16f;
        public const float HeightEpsilon = 0.0001f;
        public const float PoleTopPadding = 1f / 32f;

        public static readonly AssetLocation ExtensionBlockCode =
            new AssetLocation("multisignpost", "multisignpost-extension");
    }
}
