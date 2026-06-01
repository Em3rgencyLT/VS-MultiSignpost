namespace MultiSignpost.Blocks.EntityMultiSignPost
{
    public sealed record Direction(
        int Index,
        string LangKey,
        float ArrowRotationY,
        float TextRotationY
    );

    public static class Directions
    {
        public static readonly Direction[] All =
        {
            new(0, "multisignpost:direction-north",     180, 90),
            new(1, "multisignpost:direction-northeast", 135, 45),
            new(2, "multisignpost:direction-east",       90, 0),
            new(3, "multisignpost:direction-southeast",  45, 315),
            new(4, "multisignpost:direction-south",       0, 270),
            new(5, "multisignpost:direction-southwest", 315, 225),
            new(6, "multisignpost:direction-west",      270, 180),
            new(7, "multisignpost:direction-northwest", 225, 135)
        };

        public static float GetArrowRotationY(int directionIndex)
        {
            return IsValid(directionIndex) ? All[directionIndex].ArrowRotationY : 0;
        }

        public static float GetTextRotationY(int directionIndex)
        {
            return IsValid(directionIndex) ? All[directionIndex].TextRotationY : 0;
        }

        public static string GetLangKey(int directionIndex)
        {
            return IsValid(directionIndex) ? All[directionIndex].LangKey : All[0].LangKey;
        }

        private static bool IsValid(int directionIndex)
        {
            return directionIndex >= 0 && directionIndex < All.Length;
        }
    }
}
