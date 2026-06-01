using System.Collections.Generic;

namespace MultiSignpost.Blocks.EntityMultiSignPost
{
    public static class TextData
    {
        public static List<string>[] CreateEmpty()
        {
            List<string>[] result = new List<string>[Constants.DirectionCount];

            for (int directionIndex = 0; directionIndex < result.Length; directionIndex++)
            {
                result[directionIndex] = new List<string>();
            }

            return result;
        }

        public static List<string>[] Clone(List<string>[] source)
        {
            List<string>[] result = CreateEmpty();

            for (int directionIndex = 0; directionIndex < Constants.DirectionCount; directionIndex++)
            {
                if (source?[directionIndex] == null)
                {
                    continue;
                }

                result[directionIndex].AddRange(source[directionIndex]);
            }

            return result;
        }

        public static List<string>[] Normalize(List<string>[] source)
        {
            List<string>[] result = CreateEmpty();

            for (int directionIndex = 0; directionIndex < Constants.DirectionCount; directionIndex++)
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

                while (texts.Count > 0 && !IsRendered(texts[texts.Count - 1]))
                {
                    texts.RemoveAt(texts.Count - 1);
                }

                result[directionIndex].AddRange(texts);
            }

            return result;
        }

        public static bool IsRendered(string text)
        {
            return !string.IsNullOrWhiteSpace(text);
        }

        public static int CountRendered(List<string> texts)
        {
            int count = 0;

            foreach (string text in texts)
            {
                if (IsRendered(text))
                {
                    count++;
                }
            }

            return count;
        }
    }
}
