using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using Zstandard.Net; //Non-standard library, get it via NuGet
using System.Web.Script.Serialization; //Add reference to System.Web.Extensions for this

namespace Spelunky2LeaderboardGenerator
{
    public class LeaderboardData
    {
        //Spec as per https://old.reddit.com/r/spelunky/comments/j4wbfk/spelunky_2_leaderboard_format/
        const int LENGTH_DATA = 67000008;
        const int START_IDS = 0x0000008;
        const int START_NAMES = 0x07A1208;
        const int START_META = 0x2719C48;
        const int START_RUNDATA = 0x029020C8;
        const int START_SCORES = 0x30A32C8;
        const int START_BLOCK7 = 0x38444C0;
        const int START_BLOCK8 = 0x3C14DC0;
        const int LENGTH_NAME = 0x21;

        public PlayerEntry[] allEntries = null;

        public LeaderboardData() { }

        public static LeaderboardData FromDownload(int year, int month, int day)
        {
            string url = Program.config.dataurl
                .Replace("{YEAR}", Convert.ToString(year))
                .Replace("{MONTH}", Convert.ToString(month).PadLeft(2, '0'))
                .Replace("{DAY}", Convert.ToString(day).PadLeft(2, '0'));
            Program.Log("Downloading data from '" + url + "'...");
            WebClient wc = new WebClient();
            wc.Headers[HttpRequestHeader.UserAgent] = Program.config.useragent;
            byte[] buffer = wc.DownloadData(url);
            Program.Log("Data downloaded.");
            return FromCompressedData(buffer);
        }

        public static LeaderboardData FromCompressedDataFile(string filename)
        {
            if (!File.Exists(filename))
            {
                Program.Log("Could not load data from file '" + filename + "': File not found.");
                return null;
            }
            Program.Log("Loading data from file '" + filename + "'.");
            FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
            byte[] buffer = new byte[fs.Length];
            fs.Read(buffer, 0, (int)fs.Length);
            fs.Close();
            return FromCompressedData(buffer);
        }

        public static LeaderboardData FromCompressedData(byte[] compressedBuffer)
        {
            Program.Log("Decompressing...");
            byte[] data = new byte[LENGTH_DATA];
            MemoryStream ms = new MemoryStream(compressedBuffer);
            ZstandardStream zs = new ZstandardStream(ms, System.IO.Compression.CompressionMode.Decompress);
            int read = zs.Read(data, 0, LENGTH_DATA);
            if (zs.ReadByte() != -1) throw new Exception("Decompressed data continues beyond LENGTH_DATA!");
            zs.Close();
            ms.Close();
            Program.Log("Finished decompression.");
            return FromRawData(data);
        }

        public static LeaderboardData FromRawData(byte[] data)
        {
            LeaderboardData toReturn = new LeaderboardData();
            toReturn.InitializeWithRawData(data);
            return toReturn;
        }

        public static LeaderboardData FromJSONFile(string filename)
        {
            if (!File.Exists(filename))
            {
                Program.Log("Could not load JSON from file '" + filename + "': File not found.");
                return null;
            }
            Program.Log("Loading JSON from file: " + filename);
            StreamReader sr = new StreamReader(filename);
            string json = sr.ReadToEnd();
            sr.Close();
            return FromJSON(json);
        }

        public static LeaderboardData FromJSON(string json)
        {
            LeaderboardData toReturn = new LeaderboardData();
            toReturn.InitializeWithJson(json);
            return toReturn;
        }

        public void InitializeWithRawData(byte[] data)
        {
            //Read num entries
            MemoryStream ms = new MemoryStream(data);
            BinaryReader br = new BinaryReader(ms);
            int numEntries = br.ReadInt32() - 2;
            Program.Log("Number of entries: " + numEntries);

            //Prepare entries
            allEntries = new PlayerEntry[numEntries];
            for (int i = 0; i < numEntries; i++)
            {
                allEntries[i] = new PlayerEntry();
            }

            //Read data
            Program.Log("Reading IDs...");
            ReadBlock(ms, br, DataBlocks.IDs);
            Program.Log("Reading names...");
            ReadBlock(ms, br, DataBlocks.Names);
            Program.Log("Reading metadata...");
            ReadBlock(ms, br, DataBlocks.Meta);
            Program.Log("Reading run data...");
            ReadBlock(ms, br, DataBlocks.RunData);
            Program.Log("Reading scores...");
            ReadBlock(ms, br, DataBlocks.Scores);
            Program.Log("Reading unknown data #1...");
            ReadBlock(ms, br, DataBlocks.Block7);
            Program.Log("Reading unknown data #2...");
            ReadBlock(ms, br, DataBlocks.Block8);
            br.Close();
            ms.Close();
        }

        public void InitializeWithJson(string json)
        {
            JavaScriptSerializer jss = new JavaScriptSerializer();
            jss.MaxJsonLength = 100 * 1024 * 1024;
            Program.Log("Deserializing...");
            allEntries = jss.Deserialize<PlayerEntry[]>(json);
            if (allEntries == null) throw new Exception("Could not deserialize JSON!");
            Program.Log("Deserialization successful.");
        }

        public void ExportJson(string filename)
        {
            Program.Log("Writing JSON to " + filename + "...");
            JavaScriptSerializer jss = new JavaScriptSerializer();
            jss.MaxJsonLength = 100 * 1024 * 1024;
            string json = jss.Serialize(allEntries);
            Program.WriteFile(filename, json);
            Program.Log("Finished writing JSON.");
        }

        public void FilterPirates()
        {
#if DEBUG
            Program.Log("Skipping pirate filter due to debug mode");
            //DEBUG
            /*Log("Filtering only to Goldberg instead!");
            List<PlayerEntry> neEntries = new List<PlayerEntry>(allEntries);
            for (int i = 0; i < neEntries.Count; i++)
            {
                switch (neEntries[i].name)
                {
                    case "Goldberg":
                        
                        break;
                    default:
                        neEntries.RemoveAt(i);
                        i--;
                        break;
                }
            }
            allEntries = neEntries.ToArray();*/
            return;
#endif
            Program.Log("Filtering obvious pirates...");
            List<PlayerEntry> newEntries = new List<PlayerEntry>(allEntries);
            for (int i = 0; i < newEntries.Count; i++)
            {
                switch (newEntries[i].name)
                {
                    case "chronos":
                    case "goldberg":
                    case "Goldberg":
                    case "VALVE":
                        newEntries.RemoveAt(i);
                        i--;
                        break;
                }
            }
            allEntries = newEntries.ToArray();
            Program.Log("Finished filtering obvious pirates.");
        }

        public void SortByDepth(string desc)
        {
            Program.Log("Sorting " + desc + " by depth...");
            Array.Sort(allEntries, (x, y) => y.level == x.level ? y.score.CompareTo(x.score) : y.level.CompareTo(x.level));
        }

        public void SortByScore(string desc)
        {
            Program.Log("Sorting " + desc + " by score...");
            Array.Sort(allEntries, (x, y) => y.score.CompareTo(x.score));
        }

        public void SortByTime(string desc)
        {
            Program.Log("Sorting " + desc + " by time...");
            Array.Sort(allEntries, (x, y) => x.runframes.CompareTo(y.runframes));
        }

        private void ReadBlock(MemoryStream ms, BinaryReader br, DataBlocks block)
        {
            int count = allEntries.Length;
            int start = -1;
            switch (block)
            {
                case DataBlocks.IDs: start = START_IDS + 2 * 8; break;
                case DataBlocks.Names: start = START_NAMES + 2 * LENGTH_NAME; break;
                case DataBlocks.Meta: start = START_META + 2 * 2; break;
                case DataBlocks.RunData: start = START_RUNDATA + 2 * 8; break;
                case DataBlocks.Scores: start = START_SCORES + 2 * 8; break;
                case DataBlocks.Block7: start = START_BLOCK7 + 2 * 4; break;
                case DataBlocks.Block8: start = START_BLOCK8 + 2 * 4; break;
            }
            ms.Seek(start, SeekOrigin.Begin);
            for (int i = 0; i < count; i++)
            {
                switch (block)
                {
                    case DataBlocks.IDs: allEntries[i].id = br.ReadUInt64(); break;
                    case DataBlocks.Names:
                        {
                            byte[] nameBuffer = new byte[LENGTH_NAME];
                            ms.Read(nameBuffer, 0, LENGTH_NAME);
                            allEntries[i].name = Encoding.UTF8.GetString(nameBuffer).TrimEnd(new char[] { '\0' });
                            break;
                        }
                    case DataBlocks.Meta:
                        {
                            allEntries[i].platform = (Platform)br.ReadByte();
                            allEntries[i].character = (Character)br.ReadByte();
                            break;
                        }
                    case DataBlocks.RunData:
                        {
                            allEntries[i].runframes = br.ReadInt32();
                            allEntries[i].runend = (RunEndCause)br.ReadInt32();
                        }
                        break;
                    case DataBlocks.Scores:
                        {
                            allEntries[i].score = br.ReadInt32();
                            allEntries[i].level = br.ReadInt32();
                            break;
                        }
                    case DataBlocks.Block7: allEntries[i].unknown1 = br.ReadUInt32(); break;
                    case DataBlocks.Block8: allEntries[i].unknown2 = br.ReadUInt32(); break;
                }
            }
        }
    }
}
