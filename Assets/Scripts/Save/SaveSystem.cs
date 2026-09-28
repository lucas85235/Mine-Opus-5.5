using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Mine
{
    [Serializable]
    public sealed class SaveData
    {
        public int Version = 1;
        public int Seed;
        public float TimeOfDay;
        public float PlayerX, PlayerY, PlayerZ;
        public float Yaw, Pitch;
        public bool Flying;
        public bool Creative;
        public int Selected;
        public int[] SlotIds;
        public int[] SlotCounts;
        public string SavedAt;
    }

    /// <summary>
    /// World persistence: level.json (seed, player, inventory, time) plus chunks.bin with every modified chunk
    /// (run-length encoded). Unmodified chunks are regenerated from the seed.
    /// </summary>
    public static class SaveSystem
    {
        const uint Magic = 0x454E494D; // "MINE"
        const int ChunkFormat = 1;

        public static string SaveDirectory => Path.Combine(Application.persistentDataPath, "saves", "world");
        static string LevelPath => Path.Combine(SaveDirectory, "level.json");
        static string ChunksPath => Path.Combine(SaveDirectory, "chunks.bin");

        public static bool HasSave()
        {
            return File.Exists(LevelPath);
        }

        public static string DescribeSave()
        {
            try
            {
                if (!HasSave()) return null;
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(LevelPath));
                return "Seed " + data.Seed + " - " + data.SavedAt;
            }
            catch
            {
                return null;
            }
        }

        public static void Save(SaveData data, Dictionary<long, byte[]> chunks)
        {
            Directory.CreateDirectory(SaveDirectory);
            data.SavedAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

            string tmpChunks = ChunksPath + ".tmp";
            using (var fs = new FileStream(tmpChunks, FileMode.Create, FileAccess.Write))
            using (var w = new BinaryWriter(fs))
            {
                w.Write(Magic);
                w.Write(ChunkFormat);
                w.Write(chunks.Count);
                foreach (KeyValuePair<long, byte[]> kv in chunks)
                {
                    byte[] rle = Rle.Encode(kv.Value);
                    w.Write(kv.Key);
                    w.Write(rle.Length);
                    w.Write(rle);
                }
            }
            string tmpLevel = LevelPath + ".tmp";
            File.WriteAllText(tmpLevel, JsonUtility.ToJson(data, true));

            Replace(tmpChunks, ChunksPath);
            Replace(tmpLevel, LevelPath);
        }

        static void Replace(string tmp, string target)
        {
            if (File.Exists(target)) File.Delete(target);
            File.Move(tmp, target);
        }

        public static bool TryLoad(out SaveData data, out Dictionary<long, byte[]> chunks, out string error)
        {
            data = null;
            chunks = new Dictionary<long, byte[]>();
            error = null;
            try
            {
                if (!HasSave())
                {
                    error = "Nenhum mundo salvo.";
                    return false;
                }
                data = JsonUtility.FromJson<SaveData>(File.ReadAllText(LevelPath));
                if (File.Exists(ChunksPath))
                {
                    using (var fs = new FileStream(ChunksPath, FileMode.Open, FileAccess.Read))
                    using (var r = new BinaryReader(fs))
                    {
                        if (r.ReadUInt32() != Magic) throw new InvalidDataException("Arquivo de chunks inválido.");
                        int format = r.ReadInt32();
                        if (format != ChunkFormat) throw new InvalidDataException("Versão de chunks desconhecida: " + format);
                        int count = r.ReadInt32();
                        for (int i = 0; i < count; i++)
                        {
                            long key = r.ReadInt64();
                            int len = r.ReadInt32();
                            byte[] rle = r.ReadBytes(len);
                            chunks[key] = Rle.Decode(rle, Chunk.Volume);
                        }
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                Debug.LogException(e);
                return false;
            }
        }
    }

    /// <summary>Byte run-length encoding: pairs of (run length 1-255, value).</summary>
    public static class Rle
    {
        public static byte[] Encode(byte[] data)
        {
            var output = new List<byte>(data.Length / 8);
            int i = 0;
            while (i < data.Length)
            {
                byte v = data[i];
                int run = 1;
                while (i + run < data.Length && data[i + run] == v && run < 255) run++;
                output.Add((byte)run);
                output.Add(v);
                i += run;
            }
            return output.ToArray();
        }

        public static byte[] Decode(byte[] rle, int size)
        {
            var data = new byte[size];
            int o = 0;
            for (int i = 0; i + 1 < rle.Length && o < size; i += 2)
            {
                int run = rle[i];
                byte v = rle[i + 1];
                for (int k = 0; k < run && o < size; k++) data[o++] = v;
            }
            return data;
        }
    }
}
