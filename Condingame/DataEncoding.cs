using System;
using System.Linq;
using System.Text;
using System.IO;

namespace OthelloAI.Condingame
{
    public class DataEncoding
    {
        public static void ConvertByteWeight()
        {
            string log_dir = "codingame";
            string path = log_dir + "/weight_8x4.dat";

            using var reader = new BinaryReader(new FileStream(path, FileMode.Open));

            int n = (int)Math.Pow(3, 8) * 3 * 4;

            float[] data_raw = new float[n];

            for (int i = 0; i < n; i++)
            {
                data_raw[i] = reader.ReadSingle();
            }

            string encoded = Encode(data_raw, 4);

            File.WriteAllText(log_dir + "/encoded.txt", encoded);
            File.WriteAllText(log_dir + "/e.csv", string.Join(Environment.NewLine, data_raw));
        }

        public static string Encode(float[] data, float range)
        {
            byte ConvertTo5b(float x)
            {
                return (byte)Math.Round(Math.Clamp(x / range * 15.5 + 15.5, 0, 31));
            }

            byte[] data_b = data.Select(ConvertTo5b).ToArray();

            byte[] encoded_data = new byte[data_b.Length];

            for(int i = 0; i < data_b.Length / 3; i++)
            {
                ushort us = (ushort) ((data_b[i * 3] << 10) | (data_b[i * 3 + 1] << 5) | data_b[i * 3 + 2]);
                Encode(us, encoded_data, i * 3);
                ushort us2 = Decode(encoded_data, i * 3);

                if (us != us2)
                    Console.WriteLine($"Error : {us:X}, {us2:X}");
            }

            return Encoding.UTF8.GetString(encoded_data);
        }

        public static void Encode(ushort i, byte[] dst, int offset)
        {
            if ((i & 0x8000) != 0)
                Console.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");

            i += 0x4000;

            if (i >= 0xA000)
                i += 0x1000;

            dst[offset + 0] = (byte)(0xE0 | (i >> 12));
            dst[offset + 1] = (byte)(0x80 | ((i >> 6) & 0b111111));
            dst[offset + 2] = (byte)(0x80 | (i & 0b111111));
        }

        public static byte[] Decode(string encoded)
        {
            byte[] data = Encoding.UTF8.GetBytes(encoded);

            byte[] decoded = new byte[data.Length];

            for(int i = 0; i <data.Length / 3; i++)
            {
                ushort us = Decode(data, i * 3);

                decoded[i * 3] = (byte)((us & 0b11111_00000_00000) >> 10);
                decoded[i * 3] = (byte)((us & 0b11111_00000) >> 5);
                decoded[i * 3] = (byte)(us & 0b11111);
            }

            return decoded;
        }

        public static ushort Decode(byte[] src, int offset)
        {
            byte b1 = src[offset];
            byte b2 = src[offset + 1];
            byte b3 = src[offset + 2];

            ushort i = (ushort)(((b1 & 0xF) << 12) | ((b2 & 0b111111) << 6) | (b3 & 0b111111));

            if (i >= 0xB000)
                i -= 0x1000;

            i -= 0x4000;

            return i;
        }
    }
}
