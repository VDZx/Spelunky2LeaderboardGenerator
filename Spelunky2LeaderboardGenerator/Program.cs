/*
Spelunky 2 Leaderboard Generator - Tool to generate HTML pages based on Spelunky 2 leaderboard data
Written in 2020 by VDZ
To the extent possible under law, the author(s) have dedicated all copyright and related and neighboring rights to this software to the public domain worldwide. This software is distributed without any warranty.
You should have received a copy of the CC0 Public Domain Dedication along with this software. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.
*/
//TL;DR for above notice: You can do whatever you want with this including commercial use without any restrictions or requirements.


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Zstandard.Net; //Non-standard library, get it via NuGet
using System.Net;
using System.Web.Script.Serialization; //Add reference to System.Web.Extensions for this

namespace Spelunky2LeaderboardGenerator
{
    public class PlayerEntry
    {
        public ulong id = 0;
        public string name = "Unknown player";
        public int score = -1;
        public int level = 0;
        public Character character = Character.Unknown;
        public Platform platform = Platform.Unknown;
        public int runframes = 0;
        public RunEndCause runend = RunEndCause.Unknown;
        public uint unknown1;
        public uint unknown2;

        public string GetLevel()
        {
            return GetLevel(this.level);
        }

        public static string GetLevel(int level)
        {
            if (level < 5) return "1-" + level;
            else if (level < 9) return "2-" + (level - 4);
            else if (level == 9) return "3-1";
            else if (level < 14) return "4-" + (level - 9);
            else if (level == 14) return "5-1";
            else if (level < 19) return "6-" + (level - 14);
            else return "7-" + (level - 18);
        }

        public string GetScore()
        {
            return GetScore(this.score);
        }

        public static string GetScore(int score)
        {
            string scoreString = Convert.ToString(score);
            for (int iString = scoreString.Length - 3; iString > 0; iString -= 3) scoreString = scoreString.Substring(0, iString) + "," + scoreString.Substring(iString);
            return scoreString;
        }

        public string GetTime()
        {
            return GetTime(this.runframes);
        }

        public static string GetTime(int frames)
        {
            float realSeconds = (float)frames * 0.01666666666666666666666666666667f;
            int realMinutes = Convert.ToInt32(Math.Floor(realSeconds / 60f));
            int realHours = Convert.ToInt32(Math.Floor((float)realMinutes / 60f));
            realSeconds -= (float)realMinutes * 60f;
            realMinutes -= realHours * 60;
            string secondDisplay = Convert.ToString(Math.Round(realSeconds, 3));
            int pos = secondDisplay.IndexOf(".");
            if (pos == -1) { secondDisplay += ".000"; pos = secondDisplay.IndexOf("."); }
            while (secondDisplay.Substring(pos).Length < 4) secondDisplay += "0";
            if (pos == 1) secondDisplay = "0" + secondDisplay;
            return Convert.ToString(realHours).PadLeft(2, '0') + ":" + Convert.ToString(realMinutes).PadLeft(2, '0') + ":" + secondDisplay;
        }
    }

    public enum Character
    {
        Unknown = -1,
        Ana = 0x00,
        Margaret = 0x01,
        Colin = 0x02,
        Roffy = 0x03,
        Alto = 0x04,
        Liz = 0x05,
        Nekka = 0x06,
        LISE = 0x07,
        Coco = 0x08,
        Manfred = 0x09,
        Jay = 0x0A,
        Tina = 0x0B,
        Valerie = 0x0C,
        Au = 0x0D,
        Demi = 0x0E,
        Pilot = 0x0F,
        Airyn = 0x10,
        Dirk = 0x11,
        Guy = 0x12,
        ClassicGuy = 0x13
    }

    public enum Platform
    {
        Unknown = 0x00,
        PC = 0x12,
        PS4 = 0x30
    }

    public enum RunEndCause
    {
        Unknown = 0,
        COClear = 0x008A,
        HardClear = 0x01E9,
        NormalClear = 0x02ED
    }

    class Program
    {
        enum Blocks
        {
            IDs,
            Names,
            Meta,
            RunData,
            Scores,
            Block7,
            Block8
        }

        enum PageType
        {
            Depth,
            Score,
            Time,
            Stats
        }

        enum WorkType
        {
            Unknown,
            UpdateCurrent,
            GetDaily,
            Rebuild
        }

        enum DataSource
        {
            Unknown,
            Download,
            Json,
            LocalFile
        }

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

        const string FILE_CURRENT = "current.json";
        const int MAX_LEVEL = 117;
        const int NUM_CHARACTERS = 0x14;
        const int COSMIC_OCEAN = 23;
        //September 14th, 2020 was the first daily
        const int FIRSTRUN_YEAR = 2020;
        const int FIRSTRUN_MONTH = 9;
        const int FIRSTRUN_DAY = 14;

        static WorkType workType = WorkType.Unknown;
        static DataSource dataSource = DataSource.LocalFile;
        static byte[] data = new byte[LENGTH_DATA];
        static PlayerEntry[] allEntries = null;
        static bool silent = false;

        static MemoryStream ms = null;
        static BinaryReader br = null;

        static void Main(string[] args)
        {
            //Init
            DateTime date = DateTime.UtcNow;
            string path = string.Empty;
            string datePrefix = string.Empty;

            //Parse arguments
            for (int i = 0; i < args.Length; i++)
            {
                switch(args[i])
                {
                    case "--daily":
                        if (args.Length < i + 4)
                        {
                            Log("Insufficient arguments for --daily. Ignoring.");
                            continue;
                        }
                        workType = WorkType.GetDaily;
                        string year = args[i + 1];
                        string month = args[i + 2];
                        string day = args[i + 3];
                        date = new DateTime(Convert.ToInt32(year), Convert.ToInt32(month), Convert.ToInt32(day), 12, 00, 00);
                        Log("Work: Daily for " + GetYYYYMMDD(date));
                        datePrefix = GetYYYYMMDD(date) + "_";
                        i += 3;
                        break;
                    case "--download":
                        dataSource = DataSource.Download;
                        Log("Source: Download");
                        break;
                    case "--json":
                        Log("Source: JSON");
                        dataSource = DataSource.Json;
                        break;
                    case "--out":
                        if (args.Length < i + 2) continue;
                        i++;
                        path = args[i];
                        Log("Data folder: " + path);
                        break;
                    case "--rebuild":
                        Log("Work: Rebuild");
                        date = new DateTime(FIRSTRUN_YEAR, FIRSTRUN_MONTH, FIRSTRUN_DAY, 12, 00, 00);
                        workType = WorkType.Rebuild;
                        break;
                    case "--silent":
                        silent = true;
                        Log("Running in silent mode.");
                        break;
                    case "--update":
                        workType = WorkType.UpdateCurrent;
                        Log("Work: Update");
                        break;
                    case "--yesterday":
                        workType = WorkType.GetDaily;
                        date = DateTime.UtcNow.Subtract(new TimeSpan(1, 0, 0, 0));
                        Log("Work: Daily for " + GetYYYYMMDD(date) + " (yesterday)");
                        datePrefix = GetYYYYMMDD(date) + "_";
                        break;
                    default:
                        Log("Unrecognized parameter: " + args[i]);
                        break;
                }
            }

            //Verify
            if (workType == WorkType.Unknown) throw new Exception("No valid work type specified!");

            switch(workType)
            {
                case WorkType.Rebuild:
                    workType = WorkType.GetDaily;
                    DateTime now = DateTime.UtcNow;
                    while (!(date.Year == now.Year && date.Month == now.Month && date.Day == now.Day))
                    {
                        Log("Regenerating daily for " + GetYYYYMMDD(date));
                        LoadAndGenerate(path, date, GetYYYYMMDD(date) + "_");
                        date = date.AddDays(1);
                    }
                    break;
                default:
                    LoadAndGenerate(path, date, datePrefix);
                    break;
            }
            

            Log("Done!");
#if DEBUG
            Console.ReadLine();
#endif
        }

        static void LoadAndGenerate(string path, DateTime date, string datePrefix)
        {
            //Load entries
            LoadEntries(path, date.Year, date.Month, date.Day);
            //Depth sort
            SortByDepth(allEntries, "all entries");
            if (workType == WorkType.UpdateCurrent && dataSource != DataSource.Json) WriteJson(path + FILE_CURRENT);
            if (workType == WorkType.GetDaily && dataSource != DataSource.Json) WriteJson(path + GetYYYYMMDD(date) + ".json");
            WriteFile(path + datePrefix + "depth.html", GeneratePage(datePrefix + "depth.html", allEntries, date, PageType.Depth));
            //Score sort
            SortByScore(allEntries, "all entries");
            WriteFile(path + datePrefix + "score.html", GeneratePage(datePrefix + "score.html", allEntries, date, PageType.Score));
            //Normal, Hard and CO sort
            SortByTime(allEntries, " all entries ");
            WriteFile(path + datePrefix + "time.html", GeneratePage(datePrefix + "time.html", allEntries, date, PageType.Time));
            //Stats
            WriteFile(path + datePrefix + "stats.html", GenerateStats(datePrefix + "stats.html", allEntries, date));
        }

        static void LoadEntries(string path, int year, int month, int day)
        {
            //Load data
            byte[] compressedBuffer = null;
            switch (dataSource)
            {
                case DataSource.Download:
                    string url = "https://cdn.spelunky2.net/static/" + year + Convert.ToString(month).PadLeft(2, '0') + Convert.ToString(day).PadLeft(2, '0');
                    Log("Downloading data from '" + url + "'...");
                    WebClient wc = new WebClient();
                    wc.Headers[HttpRequestHeader.UserAgent] = "Spelunky2LeaderBoardGenerator/0.4";
                    compressedBuffer = wc.DownloadData(url);
                    Log("Data downloaded.");
                    break;
                case DataSource.LocalFile:
                    if (File.Exists("local"))
                    {
                        Log("Loading local file.");
                        FileStream fs = new FileStream("local", FileMode.Open, FileAccess.Read);
                        compressedBuffer = new byte[fs.Length];
                        fs.Read(compressedBuffer, 0, (int)fs.Length);
                        fs.Close();
                        Log("Local file loaded.");
                    }
                    else
                    {
                        throw new FileNotFoundException("Could not get 'local' file for non-download run!");
                    }
                    break;
                case DataSource.Json:
                    switch (workType)
                    {
                        case WorkType.GetDaily:
                            LoadJson(path + GetYYYYMMDD(year, month, day) + ".json");
                            break;
                        case WorkType.UpdateCurrent:
                            LoadJson(path + FILE_CURRENT);
                            break;
                    }
                    break;
            }

            //Decompress
            if (compressedBuffer != null) //If not, it's assumed the entries have already been loaded
            {
                Log("Decompressing...");
                ms = new MemoryStream(compressedBuffer);
                ZstandardStream zs = new ZstandardStream(ms, System.IO.Compression.CompressionMode.Decompress);
                int read = zs.Read(data, 0, LENGTH_DATA);
                if (zs.ReadByte() != -1) throw new Exception("Decompressed data continues beyond LENGTH_DATA!");
                zs.Close();
                ms.Close();
                Log("Finished decompression.");

                //Read num entries
                ms = new MemoryStream(data);
                br = new BinaryReader(ms);
                int numEntries = br.ReadInt32() - 2;
                Log("Number of entries: " + numEntries);

                //Prepare entries
                allEntries = new PlayerEntry[numEntries];
                for (int i = 0; i < numEntries; i++)
                {
                    allEntries[i] = new PlayerEntry();
                }

                //Read data
                Log("Reading IDs...");
                ReadBlock(Blocks.IDs);
                Log("Reading names...");
                ReadBlock(Blocks.Names);
                Log("Reading metadata...");
                ReadBlock(Blocks.Meta);
                Log("Reading run data...");
                ReadBlock(Blocks.RunData);
                Log("Reading scores...");
                ReadBlock(Blocks.Scores);
                Log("Reading unknown data #1...");
                ReadBlock(Blocks.Block7);
                Log("Reading unknown data #2...");
                ReadBlock(Blocks.Block8);
                br.Close();
                ms.Close();

            }
            else Log("No need to decompress.");

            Log("Finished reading data.");

            //Filter pirates
            FilterPirates();
        }

        static void ReadBlock(Blocks block)
        {
            int count = allEntries.Length;
            int start = -1;
            switch(block)
            {
                case Blocks.IDs: start = START_IDS + 2 * 8; break;
                case Blocks.Names: start = START_NAMES + 2 * LENGTH_NAME; break;
                case Blocks.Meta: start = START_META + 2 * 2; break;
                case Blocks.RunData: start = START_RUNDATA + 2 * 8; break;
                case Blocks.Scores: start = START_SCORES + 2 * 8; break;
                case Blocks.Block7: start = START_BLOCK7 + 2 * 4; break;
                case Blocks.Block8: start = START_BLOCK8 + 2 * 4; break;
            }
            ms.Seek(start, SeekOrigin.Begin);
            for (int i = 0; i < count; i++)
            {
                switch(block)
                {
                    case Blocks.IDs: allEntries[i].id = br.ReadUInt64(); break;
                    case Blocks.Names:
                        {
                            byte[] nameBuffer = new byte[LENGTH_NAME];
                            ms.Read(nameBuffer, 0, LENGTH_NAME);
                            allEntries[i].name = Encoding.UTF8.GetString(nameBuffer).TrimEnd(new char[] { '\0' });
                            break;
                        }
                    case Blocks.Meta:
                        {
                            allEntries[i].platform = (Platform)br.ReadByte();
                            allEntries[i].character = (Character)br.ReadByte();
                            break;
                        }
                    case Blocks.RunData:
                        {
                            allEntries[i].runframes = br.ReadInt32();
                            allEntries[i].runend = (RunEndCause)br.ReadInt32();
                        }
                        break;
                    case Blocks.Scores:
                        {
                            allEntries[i].score = br.ReadInt32();
                            allEntries[i].level = br.ReadInt32();
                            break;
                        }
                    case Blocks.Block7: allEntries[i].unknown1 = br.ReadUInt32(); break;
                    case Blocks.Block8: allEntries[i].unknown2 = br.ReadUInt32(); break;
                }
            }
        }

        static void SortByDepth(PlayerEntry[] entries, string desc)
        {
            Log("Sorting " + desc + " by depth...");
            Array.Sort(entries, (x, y) => y.level == x.level ? y.score.CompareTo(x.score) : y.level.CompareTo(x.level));
        }

        static void SortByScore(PlayerEntry[] entries, string desc)
        {
            Log("Sorting " + desc + " by score...");
            Array.Sort(entries, (x, y) => y.score.CompareTo(x.score));
        }

        static void SortByTime(PlayerEntry[] entries, string desc)
        {
            Log("Sorting " + desc + " by time...");
            Array.Sort(entries, (x, y) => x.runframes.CompareTo(y.runframes));
        }

        static void WriteFile(string filename, string content)
        {
            Log("Writing " + filename);
            StreamWriter sw = new StreamWriter(filename);
            sw.Write(content);
            sw.Close();
        }

        static string GeneratePage(string description, PlayerEntry[] entries, DateTime dateTime, PageType pageType)
        {
            Log("Generating " + description);
            StringBuilder sb = new StringBuilder();

            WriteHeader(sb, dateTime, pageType);
            //--BODY--
            sb.AppendLine("<body><center>");
            sb.Append("<font size=\"7\">");
            sb.Append(pageType);
            sb.Append(" ranking for ");
            sb.Append(dateTime.Day);
            sb.Append(" ");
            sb.Append(dateTime.ToString("MMMM", System.Globalization.CultureInfo.CreateSpecificCulture("en")));
            sb.Append(" ");
            sb.Append(dateTime.Year);
            sb.AppendLine("</font><br />");
            if (workType == WorkType.UpdateCurrent)
            {
                sb.AppendLine("(Ongoing)<br />");
            }
            string pagefile = "depth.html";
            switch(pageType)
            {
                case PageType.Score: pagefile = "score.html"; break;
                case PageType.Time: pagefile = "time.html"; break;
            }
            WriteDateNavigation(sb, dateTime, pagefile);
            sb.Append("<br /><br />");
            if (workType == WorkType.UpdateCurrent)
            {
                sb.Append("<div id=\"count\" >Loading...</div><br /><br />");
            }
            WriteTypeNavigation(sb, dateTime, pageType);
            sb.AppendLine("<br /><br />");
            switch(pageType)
            {
                case PageType.Depth:
                case PageType.Score:
                    sb.AppendLine("<table border=\"1\">");
                    for (int i = 0; i < entries.Length; i++)
                    {
                        WriteEntry(sb, entries[i], i + 1, pageType);
                    }
                    sb.AppendLine("</table>");
                    break;
                case PageType.Time:
                    List<PlayerEntry> coEntries = new List<PlayerEntry>();
                    List<PlayerEntry> hardEntries = new List<PlayerEntry>();
                    List<PlayerEntry> normalEntries = new List<PlayerEntry>();
                    for (int i = 0; i < entries.Length; i++)
                    {
                        switch(entries[i].runend)
                        {
                            case RunEndCause.COClear: coEntries.Add(entries[i]); break;
                            case RunEndCause.HardClear: hardEntries.Add(entries[i]); break;
                            case RunEndCause.NormalClear: normalEntries.Add(entries[i]); break;
                        }
                    }
                    WriteTimeRanking(sb, coEntries, "Special", pageType);
                    sb.AppendLine("<br /><br />");
                    WriteTimeRanking(sb, hardEntries, "Hard", pageType);
                    sb.AppendLine("<br /><br />");
                    WriteTimeRanking(sb, normalEntries, "Normal", pageType);
                    sb.AppendLine("<br /><br />");

                    break;
            }

            WriteFooter(sb, dateTime, pageType, true);

            //Write to file
            return sb.ToString();
        }

        static string GenerateStats(string description, PlayerEntry[] entries, DateTime dateTime)
        {
            Log("Generating " + description);
            StringBuilder sb = new StringBuilder();

            WriteHeader(sb, dateTime, PageType.Stats);
            //--BODY--
            sb.AppendLine("<body><center>");
            sb.Append("<font size=\"7\">Stats for ");
            sb.Append(dateTime.Day);
            sb.Append(" ");
            sb.Append(dateTime.ToString("MMMM", System.Globalization.CultureInfo.CreateSpecificCulture("en")));
            sb.Append(" ");
            sb.Append(dateTime.Year);
            sb.AppendLine("</font><br />");
            if (workType == WorkType.UpdateCurrent)
            {
                sb.AppendLine("(Ongoing)<br />");
            }
            WriteDateNavigation(sb, dateTime, "stats.html");
            sb.Append("<br /><br />");
            if (workType == WorkType.UpdateCurrent)
            {
                sb.Append("<div id=\"count\" >Loading...</div><br /><br />");
            }
            WriteTypeNavigation(sb, dateTime, PageType.Stats);
            sb.AppendLine("<br /><br />");

            //Collect stats
            Dictionary<int, int> deathsPerLevel = new Dictionary<int, int>();
            Dictionary<int, int> survivorsPerLevel = new Dictionary<int, int>();
            Dictionary<Character, List<PlayerEntry>> characterEntries = new Dictionary<Character, List<PlayerEntry>>();
            for (int i = 0; i < entries.Length; i++)
            {
                PlayerEntry entry = entries[i];
                //Level
                for (int iLevel = entry.level; iLevel > 0; iLevel--)
                {
                    bool cleared = (entry.runend == RunEndCause.NormalClear || entry.runend == RunEndCause.HardClear || entry.runend == RunEndCause.COClear);
                    if (!deathsPerLevel.ContainsKey(iLevel)) deathsPerLevel.Add(iLevel, 0);
                    if (!survivorsPerLevel.ContainsKey(iLevel)) survivorsPerLevel.Add(iLevel, 0);
                    if (iLevel == entry.level && !cleared)
                    {
                        deathsPerLevel[iLevel]++;
                        continue;
                    }
                    survivorsPerLevel[iLevel]++;
                }
                //Characters
                if (!characterEntries.ContainsKey(entry.character)) characterEntries.Add(entry.character, new List<PlayerEntry>());
                characterEntries[entry.character].Add(entry);
            }

            //Death stats
            sb.AppendLine("<font size=\"6\">Level deadliness</font><br />");
            if (workType != WorkType.UpdateCurrent)
            {
                sb.Append("(You can also check out <a href=\"https://jhay.net/spelunky2daily/?day=");
                sb.Append(GetYYYYMMDD(dateTime));
                sb.AppendLine("\">JeremyHay's site</a> for more survival stats.)<br />");
            }
            sb.AppendLine("<br />");
            sb.AppendLine("<table border=\"1\">");
            sb.AppendLine("<tr class=\"top\"><td></td><td>Level</td><td>Survivors</td><td>Deaths</td><td>Death rate</td></tr>");
            for (int i = 1; i < MAX_LEVEL; i++)
            {
                int survivors = 0;
                int deaths = 0;
                if (survivorsPerLevel.ContainsKey(i)) survivors = survivorsPerLevel[i];
                if (deathsPerLevel.ContainsKey(i)) deaths = deathsPerLevel[i];

                if (survivors + deaths > 0 || i < COSMIC_OCEAN)
                {
                    float deathRate = 0;
                    if (survivors + deaths > 0) deathRate = (float)deaths / (float)(survivors + deaths);
                    
                    sb.Append("<tr");
                    if (deathRate > 0.5f) sb.Append(" class=\"deadly\"");
                    else if (deathRate > 0.3f) sb.Append(" class=\"danger\"");
                    sb.Append("><td class=\"w");
                    string levelName = PlayerEntry.GetLevel(i);
                    sb.Append(levelName.Substring(0, 1));
                    if (i >= COSMIC_OCEAN) sb.Append("5");
                    sb.Append("\" /><td>");
                    sb.Append(levelName);
                    sb.Append("</td><td>");
                    sb.Append(survivors);
                    sb.Append("</td><td>");
                    sb.Append(deaths);
                    sb.Append("</td><td>");
                    sb.Append(Convert.ToString(Math.Round(deathRate * 100f, 1)) + "%");
                    sb.AppendLine("</td></tr>");
                }
            }
            sb.AppendLine("</table><br/><br/>");

            //Characters
            List<KeyValuePair<Character, List<PlayerEntry>>> characterList = characterEntries.ToList(); //Sorry :(
            Dictionary<Character, string> characterPlayerNames = new Dictionary<Character, string>();

            //Character usage
            sb.AppendLine("<font size=\"6\">Character usage</font><br /><br />");
            sb.AppendLine("<table border=\"1\">");
            characterList.Sort((x, y) => y.Value.Count.CompareTo(x.Value.Count));
            for (int i = 0; i < characterList.Count; i++)
            {
                sb.Append("<tr><td>");
                sb.Append(i + 1);
                sb.Append("</td><td class=\"c");
                sb.Append(Convert.ToString((int)characterList[i].Key, 16).PadLeft(2, '0'));
                sb.Append("\" /><td>");
                sb.Append(characterList[i].Value.Count);
                sb.Append("</td><td>");
                sb.Append(Math.Round(((float)characterList[i].Value.Count / (float)entries.Length) * 100f, 1));
                sb.AppendLine("%</td></tr>");
            }
            sb.AppendLine("</table><br/><br/>");

            //Character best depth
            List<KeyValuePair<Character, int>> characterDepths = new List<KeyValuePair<Character, int>>();
            characterPlayerNames.Clear();
            for (int i = 0; i < characterList.Count; i++)
            {
                int bestDepth = 1;
                string bestPlayer = "Unknown";
                for (int j = 0; j < characterList[i].Value.Count; j++)
                {
                    PlayerEntry entry = characterList[i].Value[j];
                    if (entry.level > bestDepth)
                    {
                        bestDepth = entry.level;
                        bestPlayer = entry.name;
                    }
                }
                characterDepths.Add(new KeyValuePair<Character, int>(characterList[i].Key, bestDepth));
                characterPlayerNames.Add(characterList[i].Key, bestPlayer);
            }
            characterDepths.Sort((x, y) => y.Value.CompareTo(x.Value));
            sb.AppendLine("<font size=\"6\">Character best depth</font><br /><br />");
            sb.AppendLine("<table border=\"1\">");
            for (int i = 0; i < characterList.Count; i++)
            {
                sb.Append("<tr><td>");
                sb.Append(i + 1);
                sb.Append("</td><td class=\"c");
                sb.Append(Convert.ToString((int)characterDepths[i].Key, 16).PadLeft(2, '0'));
                sb.Append("\" /><td>");
                sb.Append(characterPlayerNames[characterDepths[i].Key]);
                sb.Append("</td><td>");
                sb.Append(PlayerEntry.GetLevel(characterDepths[i].Value));
                sb.AppendLine("</td></tr>");
            }
            sb.AppendLine("</table><br/><br/>");

            //Character best score
            List<KeyValuePair<Character, int>> characterScores = new List<KeyValuePair<Character, int>>();
            characterPlayerNames.Clear();
            for (int i = 0; i < characterList.Count; i++)
            {
                int bestScore = 0;
                string bestPlayer = "Unknown";
                for (int j = 0; j < characterList[i].Value.Count; j++)
                {
                    PlayerEntry entry = characterList[i].Value[j];
                    if (entry.score > bestScore)
                    {
                        bestScore = entry.score;
                        bestPlayer = entry.name;
                    }
                }
                characterScores.Add(new KeyValuePair<Character, int>(characterList[i].Key, bestScore));
                characterPlayerNames.Add(characterList[i].Key, bestPlayer);
            }
            characterScores.Sort((x, y) => y.Value.CompareTo(x.Value));
            sb.AppendLine("<font size=\"6\">Character best score</font><br /><br />");
            sb.AppendLine("<table border=\"1\">");
            for (int i = 0; i < characterList.Count; i++)
            {
                sb.Append("<tr><td>");
                sb.Append(i + 1);
                sb.Append("</td><td class=\"c");
                sb.Append(Convert.ToString((int)characterScores[i].Key, 16).PadLeft(2, '0'));
                sb.Append("\" /><td>");
                sb.Append(characterPlayerNames[characterScores[i].Key]);
                sb.Append("</td><td>");
                sb.Append(PlayerEntry.GetScore(characterScores[i].Value));
                sb.AppendLine("</td></tr>");
            }
            sb.AppendLine("</table><br/><br/>");

            //Character best time
            List<KeyValuePair<Character, int>> characterTimes = new List<KeyValuePair<Character, int>>();
            characterPlayerNames.Clear();
            int maxTime = 60 /*frames*/ * (((9 /*hours*/ * 60) + 59 /*minutes*/) * 60 + 59 /*seconds*/);
            for (int i = 0; i < characterList.Count; i++)
            {
                int bestTime = maxTime;
                string bestPlayer = "Unknown";
                for (int j = 0; j < characterList[i].Value.Count; j++)
                {
                    PlayerEntry entry = characterList[i].Value[j];
                    switch(entry.runend)
                    {
                        case RunEndCause.NormalClear:
                        case RunEndCause.HardClear:
                        case RunEndCause.COClear:
                            break;
                        default:
                            continue;
                    }
                    if (entry.runframes < bestTime)
                    {
                        bestTime = entry.runframes;
                        bestPlayer = entry.name;
                    }
                }
                characterTimes.Add(new KeyValuePair<Character, int>(characterList[i].Key, bestTime));
                characterPlayerNames.Add(characterList[i].Key, bestPlayer);
            }
            characterTimes.Sort((x, y) => x.Value.CompareTo(y.Value));
            sb.AppendLine("<font size=\"6\">Character best time</font><br /><br />");
            sb.AppendLine("<table border=\"1\">");
            for (int i = 0; i < characterList.Count; i++)
            {
                bool finished = false;
                if (characterTimes[i].Value != maxTime) finished = true;
                sb.Append("<tr><td>");
                sb.Append(i + 1);
                sb.Append("</td><td class=\"c");
                sb.Append(Convert.ToString((int)characterTimes[i].Key, 16).PadLeft(2, '0'));
                sb.Append("\" /><td>");
                if (finished) sb.Append(characterPlayerNames[characterTimes[i].Key]);
                else sb.Append("<i>N/A</i>");
                sb.Append("</td><td>");
                if (finished) sb.Append(PlayerEntry.GetTime(characterTimes[i].Value));
                else sb.Append("<i>did not finish</i>");
                sb.AppendLine("</td></tr>");
            }
            sb.AppendLine("</table><br/><br/>");

            //Finish
            WriteFooter(sb, dateTime, PageType.Stats, true);

            //Write to file
            return sb.ToString();
        }

        static void WriteHeader(StringBuilder sb, DateTime dateTime, PageType pageType)
        {
            sb.AppendLine("<html>");
            //--HEAD--
            sb.AppendLine("<head>");
            sb.AppendLine("<meta charset=\"utf-8\" />");
            sb.Append("<title>");
            sb.Append(pageType);
            sb.Append(" ");
            sb.Append(dateTime.Year);
            sb.Append("-");
            sb.Append(dateTime.Month);
            sb.Append("-");
            sb.Append(dateTime.Day);
            sb.Append(" - Spelunky 2 leaderboards (unofficial)");
            sb.AppendLine("</title>");
            sb.AppendLine("<style>");
            sb.AppendLine(styleCss);
            //Character CSS
            for (int i = 0; i < Convert.ToInt32(Character.ClassicGuy) + 1; i++)
            {
                sb.Append(".c");
                sb.AppendLine(Convert.ToString(i, 16).PadLeft(2, '0'));
                sb.AppendLine("{");
                sb.Append("    background-image: url(\"");
                sb.Append(Convert.ToString((Character)i));
                sb.AppendLine(".png\");");
                sb.AppendLine("    width: 64px;");
                sb.AppendLine("    height: 32px;");
                sb.AppendLine("}"); ;
            }
            sb.AppendLine("</style></head>");
        }

        static void WriteDateNavigation(StringBuilder sb, DateTime dateTime, string pagefile)
        {
            if (!(dateTime.Year == FIRSTRUN_YEAR && dateTime.Month == FIRSTRUN_MONTH && dateTime.Day == FIRSTRUN_DAY))
            {
                sb.Append("<a href=\"");
                sb.Append(GetYYYYMMDD(dateTime.Subtract(new TimeSpan(1, 0, 0, 0))));
                sb.Append("_");
                sb.Append(pagefile);
                sb.Append("\">");
                sb.Append(GetYYYYMMDD(dateTime.Subtract(new TimeSpan(1, 0, 0, 0))));
                sb.Append("</a>");
                sb.Append("&nbsp;&nbsp;&nbsp;&nbsp;<<&nbsp;&nbsp;&nbsp;&nbsp;");
            }
            sb.Append(GetYYYYMMDD(dateTime));
            sb.Append("&nbsp;&nbsp;&nbsp;&nbsp;>>&nbsp;&nbsp;&nbsp;&nbsp;");
            if (workType != WorkType.UpdateCurrent)
            {
                sb.Append("<a href=\"");
                DateTime tomorrow = dateTime.AddDays(1);
                if (!(tomorrow.Year == DateTime.UtcNow.Year && tomorrow.Month == DateTime.UtcNow.Month && tomorrow.Day == DateTime.UtcNow.Day))
                {
                    sb.Append(GetYYYYMMDD(tomorrow));
                    sb.Append("_");
                }
                sb.Append(pagefile);
                sb.Append("\">");
            }
            sb.Append(GetYYYYMMDD(dateTime.AddDays(1)));
            if (workType == WorkType.GetDaily) sb.Append("</a>");
        }

        static void WriteTypeNavigation(StringBuilder sb, DateTime dateTime, PageType pageType)
        {
            string datePrefix = string.Empty;
            if (workType == WorkType.GetDaily) datePrefix = GetYYYYMMDD(dateTime) + "_";
            PageType[] activeTypes =
            {
                PageType.Depth,
                PageType.Score,
                PageType.Time,
                PageType.Stats
            };

            for (int i = 0; i < activeTypes.Length; i++)
            {
                if (i != 0) sb.Append(" - ");
                if (pageType != activeTypes[i])
                {
                    sb.Append("<a href=\"");
                    sb.Append(datePrefix);
                    sb.Append(Convert.ToString(activeTypes[i]).ToLower());
                    sb.Append(".html\">");
                }
                else sb.Append("<b>");
                sb.Append("[");
                sb.Append(Convert.ToString(activeTypes[i]));
                sb.Append("]");
                if (pageType != activeTypes[i]) sb.Append("</a>");
                else sb.Append("</b>");
            }
        }

        static void WriteFooter(StringBuilder sb, DateTime dateTime, PageType pageType, bool jsonDownloadable)
        {
            if (workType == WorkType.UpdateCurrent)
            {
                sb.AppendLine("Updates every 30 minutes.<br />");
                sb.Append("Last update: ");
            }
            else
            {
                sb.Append("Page generated on: ");
            }
            sb.Append(dateTime.ToString());
            sb.AppendLine(" UTC<br/>");
            
            if (jsonDownloadable)
            {
                sb.Append("<br/><a href=\"");
                switch (workType)
                {
                    case WorkType.UpdateCurrent: sb.Append(FILE_CURRENT); break;
                    case WorkType.GetDaily: sb.Append(GetYYYYMMDD(dateTime)); sb.Append(".json"); break;
                }
                sb.AppendLine("\">(Download data in JSON format)</a>");
            }
            if (workType == WorkType.UpdateCurrent)
            {
                sb.AppendLine(countdownJS.Replace("{TODAY}", GetYYYYMMDD(dateTime)).Replace("{TOMORROW}", GetYYYYMMDD(dateTime.AddDays(1))).Replace("{TYPE}", Convert.ToString(pageType).ToLower()));
            }
            sb.AppendLine("</center></body>");
            sb.AppendLine("</html>");
        }

        static void WriteEntry(StringBuilder sb, PlayerEntry entry, int rank, PageType sortedBy)
        {
            if (entry.platform == Platform.PC || entry.platform == Platform.PS4) return;
            //Rank
            sb.Append("<tr><td>");
            sb.Append(Convert.ToString(rank));
            //Platform
            sb.Append("</td><td class=\"");
            switch (entry.platform)
            {
                case Platform.PC: sb.Append("pc"); break;
                case Platform.PS4: sb.Append("ps4"); break;
                default: sb.Append("unknown"); break;
            }
            sb.Append("\" />");
#if DEBUG
            if (true)
            {
                //DEBUG
                /*sb.Append("<td>");
                sb.Append(Convert.ToString((int)entry.runend, 16));
                sb.Append("</td><td>");
                sb.Append(entry.id);
                sb.Append("</td><td>");
                sb.Append(Convert.ToString((long)entry.id, 16));
                sb.Append("</td><td>");
                sb.Append(entry.unknown1);
                sb.Append("</td><td>");
                sb.Append(Convert.ToString(entry.unknown1, 16));
                sb.Append("</td><td>");
                sb.Append(entry.unknown2);
                sb.Append("</td><td>");
                sb.Append(Convert.ToString(entry.unknown2, 16));
                sb.Append("</td><td>");
                sb.Append("</td>");*/
                sb.Append("<td>");
                sb.Append(Convert.ToString((long)entry.id, 16).PadLeft(16, '0'));
                sb.Append("</td>");
            }
#endif
            //Character
            sb.Append("<td class=\"c");
            sb.Append(Convert.ToString(Convert.ToInt16(entry.character), 16).PadLeft(2, '0'));
            sb.Append("\"/>");
            //Name
            sb.Append("<td>");
            sb.Append(entry.name.Replace("<", "").Replace(">", ""));
            switch(sortedBy)
            {
                case PageType.Depth:
                case PageType.Score:
                    //Depth
                    sb.Append("</td><td");
                    switch(entry.runend)
                    {
                        case RunEndCause.NormalClear:
                        case RunEndCause.HardClear:
                        case RunEndCause.COClear:
                            sb.Append(" class=\"clear\">");
                            break;
                        default:
                            sb.Append(">");
                            break;
                    }
                    sb.Append(entry.GetLevel());
                    //Score
                    sb.Append("</td><td class=\"score\">");
                    sb.Append(entry.GetScore());
                    break;
                case PageType.Time:
                    //Time
                    sb.Append("</td><td>");
                    sb.Append(entry.GetTime());
                    break;
            }

            //Finish row
            sb.AppendLine("</td></tr>");
        }

        static void WriteTimeRanking(StringBuilder sb, List<PlayerEntry> entries, string description, PageType sortedBy)
        {
            if (entries.Count == 0)
            {
                sb.Append("<font size=\"6\">No ");
                sb.Append(description);
                switch(workType)
                {
                    case WorkType.UpdateCurrent: sb.Append(" clears have happened yet."); break;
                    case WorkType.GetDaily: sb.Append(" clears happened this day."); break;
                }
                sb.AppendLine("</font>");
            }
            else
            {
                sb.Append("<font size=\"6\">");
                sb.Append(description);
                sb.AppendLine("</font>");
                sb.AppendLine("<br /><br />");
                sb.AppendLine("<table border=\"1\">");
                for (int i = 0; i < entries.Count; i++)
                {
                    WriteEntry(sb, entries[i], i + 1, sortedBy);
                }
                sb.AppendLine("</table>");
            }
        }

        static void FilterPirates()
        {
#if DEBUG
            Log("Skipping pirate filter due to debug mode");
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
            Log("Filtering obvious pirates...");
            List<PlayerEntry> newEntries = new List<PlayerEntry>(allEntries);
            for (int i = 0; i < newEntries.Count; i++)
            {
                switch(newEntries[i].name)
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
            Log("Finished filtering obvious pirates.");
        }

        static void LoadJson(string filename)
        {
            Log("Loading JSON from file: " + filename);
            StreamReader sr = new StreamReader(filename);
            string json = sr.ReadToEnd();
            sr.Close();
            JavaScriptSerializer jss = new JavaScriptSerializer();
            jss.MaxJsonLength = 100 * 1024 * 1024;
            Log("Deserializing...");
            allEntries = jss.Deserialize<PlayerEntry[]>(json);
            if (allEntries == null) throw new Exception("Could not deserialize '" + filename + "'!");
            Log("Deserialization successful.");
        }

        static void WriteJson(string filename)
        {
            Log("Writing JSON to " + filename + "...");
            JavaScriptSerializer jss = new JavaScriptSerializer();
            jss.MaxJsonLength = 100 * 1024 * 1024;
            string json = jss.Serialize(allEntries);
            StreamWriter jsonWriter = new StreamWriter(filename);
            jsonWriter.Write(json);
            jsonWriter.Close();
            Log("Finished writing JSON.");
        }

        static string GetYYYYMMDD(DateTime dt)
        {
            return GetYYYYMMDD(dt.Year, dt.Month, dt.Day);
        }

        static string GetYYYYMMDD(int year, int month, int day)
        {
            return Convert.ToString(year) + "-" + Convert.ToString(month).PadLeft(2, '0') + "-" + Convert.ToString(day).PadLeft(2, '0');
        }

        static void Log(string msg)
        {
            msg = "[" + DateTime.UtcNow.ToString() + "] " + msg;
            if (!silent) Console.WriteLine(msg);
        }

        const string styleCss = @"
table td
{
	border: 1px;
	border-style: solid;
	border-radius: 2px;
	min-width: 3.5em;
	height: 1.25em;
	text-align: center;
}

.pc
{
    background-image: url(""pc.png"");
    width: 64px;
    height: 32px;
}

.ps4
{
    background-image: url(""ps4.png"");
    width: 64px;
    height: 32px;
}

.unknown
{
    background-image: url(""unknown.png"");
    width: 64px;
    height: 32px;
}

.clear
{
    background-color: #009933;
    color: white;
}

.top
{
    background-color: black;
    color: white;
}

.danger
{
    background-color: #ff9966;
}

.deadly
{
    background-color: #cc0000;
    color: white;
}

.w1
{
    background-color: #ff6666;
}

.w2
{
    background-color: #339933;
}

.w3
{
    background-color: #d9d9d9;
}

.w4
{
    background-color: #00b3b3;
}

.w5
{
    background-color: #99c2ff;
}

.w6
{
    background-color: #3333cc;
}

.w7
{
    background-color: #8cd9b3;
}

.w75
{
    background-color: #262626;
}

#count
{
    border: 1px;
    border-style: solid;
    font-size: 150%;
    text-margin: 20px;
}
";

        const string countdownJS = @"
<script>
var tomorrow = new Date(""{TOMORROW}"").getTime();
setInterval(function()
{
	var left = tomorrow - new Date().getTime();
	if (left > 0) document.getElementById(""count"").innerHTML = ""Challenge ends in "" + getLeft(left);
	else if (left > -600000)
	{
		left += 600000;
		document.getElementById(""count"").innerHTML = ""Challenge finished! Final results in "" + getLeft(left);
	}
	else document.getElementById(""count"").innerHTML = ""Challenge finished. <a href=\""{TODAY}_{TYPE}.html\"">Click here to view the final results.</a>"";
}, 1000);
function getLeft(left)
{
	var s = Math.floor(left / 1000);
	var m = Math.floor(s / 60);
	var h = Math.floor(m / 60);
	s -= m * 60;
	m -= h * 60;
	return (""0"" + h).slice(-2) + "":"" + (""0"" + m).slice(-2) + "":"" + (""0"" + s).slice(-2);
}
</script>
";
    }
}
