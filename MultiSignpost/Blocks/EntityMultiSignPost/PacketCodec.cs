using MultiSignpost.Config;
using System.Collections.Generic;
using System.IO;

namespace MultiSignpost.Blocks.EntityMultiSignPost
{
    public sealed class MultiSignPostOpenDialogPacket
    {
        public string DialogTitle { get; set; }
        public int MaxExtensions { get; set; }
        public float MinScale { get; set; }
        public float MaxScale { get; set; }
        public float CurrentScale { get; set; }
        public List<string>[] TextByDirection { get; set; }
        public string CurrentFont { get; set; }
        public string[] AllowedFonts { get; set; }
    }

    public sealed class MultiSignPostSaveTextPacket
    {
        public List<string>[] TextByDirection { get; set; }
        public float Scale { get; set; }
        public string Font { get; set; }
    }

    public sealed class MultiSignPostNowTextPacket
    {
        public List<string>[] TextByDirection { get; set; }
        public string Font { get; set; }
    }

    public static class PacketCodec
    {
        public static byte[] WriteOpenDialog(
            string dialogTitle,
            int maxExtensions,
            float minScale,
            float maxScale,
            float currentScale,
            List<string>[] textByDirection,
            string currentFont,
            string[] allowedFonts)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                BinaryWriter writer = new BinaryWriter(ms);

                writer.Write(dialogTitle ?? "");
                writer.Write(maxExtensions);
                writer.Write(minScale);
                writer.Write(maxScale);
                writer.Write(currentScale);
                WriteTextByDirection(writer, textByDirection);
                writer.Write(currentFont ?? "");
                WriteStringArray(writer, allowedFonts);

                return ms.ToArray();
            }
        }

        public static MultiSignPostOpenDialogPacket ReadOpenDialog(byte[] data)
        {
            using (MemoryStream ms = new MemoryStream(data))
            {
                BinaryReader reader = new BinaryReader(ms);

                MultiSignPostOpenDialogPacket packet = new MultiSignPostOpenDialogPacket
                {
                    DialogTitle = reader.ReadString(),
                    MaxExtensions = reader.ReadInt32(),
                    MinScale = reader.ReadSingle(),
                    MaxScale = reader.ReadSingle(),
                    CurrentScale = reader.ReadSingle(),
                    TextByDirection = ReadTextByDirection(reader)
                };

                packet.CurrentFont = reader.BaseStream.Position < reader.BaseStream.Length
                    ? reader.ReadString()
                    : MultiSignpostConfig.Current.GetDefaultFontName();

                packet.AllowedFonts = reader.BaseStream.Position < reader.BaseStream.Length
                    ? ReadStringArray(reader)
                    : MultiSignpostConfig.Current.AllowedFonts;

                return packet;
            }
        }

        public static byte[] WriteSaveText(
            List<string>[] textByDirection,
            float scale,
            string font)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                BinaryWriter writer = new BinaryWriter(ms);

                WriteTextByDirection(writer, textByDirection);
                writer.Write(scale);
                writer.Write(font ?? "");

                return ms.ToArray();
            }
        }

        public static MultiSignPostSaveTextPacket ReadSaveText(
            byte[] data,
            float fallbackScale,
            string fallbackFont)
        {
            using (MemoryStream ms = new MemoryStream(data))
            {
                BinaryReader reader = new BinaryReader(ms);

                MultiSignPostSaveTextPacket packet = new MultiSignPostSaveTextPacket
                {
                    TextByDirection = ReadTextByDirection(reader)
                };

                packet.Scale = reader.BaseStream.Position < reader.BaseStream.Length
                    ? reader.ReadSingle()
                    : fallbackScale;

                packet.Font = reader.BaseStream.Position < reader.BaseStream.Length
                    ? reader.ReadString()
                    : fallbackFont;

                return packet;
            }
        }

        public static byte[] WriteNowText(
            List<string>[] textByDirection,
            string font = null)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                BinaryWriter writer = new BinaryWriter(ms);

                WriteTextByDirection(writer, textByDirection);

                if (font != null)
                {
                    writer.Write(font);
                }

                return ms.ToArray();
            }
        }

        public static MultiSignPostNowTextPacket ReadNowText(byte[] data)
        {
            using (MemoryStream ms = new MemoryStream(data))
            {
                BinaryReader reader = new BinaryReader(ms);

                MultiSignPostNowTextPacket packet = new MultiSignPostNowTextPacket
                {
                    TextByDirection = ReadTextByDirection(reader)
                };

                if (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    packet.Font = reader.ReadString();
                }

                return packet;
            }
        }

        public static void WriteTextByDirection(BinaryWriter writer, List<string>[] textByDirection)
        {
            List<string>[] normalized = TextData.Normalize(textByDirection);

            for (int directionIndex = 0; directionIndex < Constants.DirectionCount; directionIndex++)
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
            List<string>[] result = TextData.CreateEmpty();

            for (int directionIndex = 0; directionIndex < Constants.DirectionCount; directionIndex++)
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

        private static void WriteStringArray(BinaryWriter writer, string[] values)
        {
            string[] sanitized = MultiSignpostConfig.SanitizeAllowedFonts(values);

            writer.Write(sanitized.Length);

            foreach (string value in sanitized)
            {
                writer.Write(value ?? "");
            }
        }

        private static string[] ReadStringArray(BinaryReader reader)
        {
            int count = reader.ReadInt32();

            if (count < 0 || count > 64)
            {
                return new[] { MultiSignpostConfig.FallbackFontName };
            }

            string[] values = new string[count];

            for (int i = 0; i < count; i++)
            {
                values[i] = reader.ReadString();
            }

            return values;
        }
    }
}
