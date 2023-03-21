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
using System.Web.Script.Serialization; //Add reference to System.Web.Extensions for this

namespace Spelunky2LeaderboardGenerator
{
    class Program
    {
        public const string FILE_ADDITIONS = "additions.json";
        public const string FILE_BLACKLIST = "blacklist.json";
        public const string FILE_CURRENT = "current.json";
        public const string FILE_PLAYERS = "players.json";
        //September 14th, 2020 was the first daily
        public const int FIRSTRUN_YEAR = 2020;
        public const int FIRSTRUN_MONTH = 9;
        public const int FIRSTRUN_DAY = 14;

        public static Dictionary<ulong, PlayerAddition> additions = new Dictionary<ulong, PlayerAddition>();
        public static List<ulong> blacklist = new List<ulong>();
        public static Config config = new Config();
        public static Dictionary<ulong, PlayerInfo> players = null;

        static WorkType workType = WorkType.Unknown;
        static DataSource dataSource = DataSource.LocalFile;
        static bool silent = false;
        static JavaScriptSerializer jss = null;

        static void Main(string[] args)
        {
            //Init
            int year = DateTime.UtcNow.Year;
            int month = DateTime.UtcNow.Month;
            int day = DateTime.UtcNow.Day;
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
                        year = Convert.ToInt32(args[i + 1]);
                        month = Convert.ToInt32(args[i + 2]);
                        day = Convert.ToInt32(args[i + 3]);
                        Log("Work: Daily for " + GetYYYYMMDD(year, month, day));
                        datePrefix = GetYYYYMMDD(year, month, day) + "_";
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
                        year = FIRSTRUN_YEAR;
                        month = FIRSTRUN_MONTH;
                        day = FIRSTRUN_DAY;
                        workType = WorkType.Rebuild;
                        break;
                    case "--silent":
                        silent = true;
                        Log("Running in silent mode.");
                        break;
                    case "--update":
                        workType = WorkType.UpdateCurrent;
                        datePrefix = GetYYYYMMDD(DateTime.UtcNow) + "_";
                        Log("Work: Update");
                        break;
                    case "--yesterday":
                        workType = WorkType.GetDaily;
                        DateTime date = DateTime.UtcNow.Subtract(new TimeSpan(1, 0, 0, 0));
                        year = date.Year;
                        month = date.Month;
                        day = date.Day;
                        Log("Work: Daily for " + GetYYYYMMDD(date) + " (yesterday)");
                        datePrefix = GetYYYYMMDD(date) + "_";
                        break;
                    case "--test":
                        Log(AppDomain.CurrentDomain.BaseDirectory + FILE_LOG_BLACKLIST);
                        Environment.Exit(0);
                        break;
                    default:
                        Log("Unrecognized parameter: " + args[i]);
                        break;
                }
            }

            //Verify
            if (workType == WorkType.Unknown) throw new Exception("No valid work type specified!");

            //Load player data
            if (workType != WorkType.UpdateCurrent) LoadPlayerData();
            //Load blacklist
            LoadBlacklist();

            //Work
            LeaderboardData data = null;
            switch (workType)
            {
                case WorkType.Rebuild:
                    DateTime now = DateTime.UtcNow;
                    while (!(year == now.Year && month == now.Month && day == now.Day))
                    {
                        Log("Regenerating daily for " + GetYYYYMMDD(year, month, day));
                        data = LoadAndGenerate(path, year, month, day, GetYYYYMMDD(year, month, day) + "_");
                        DateTime next = new DateTime(year, month, day, 12, 00, 00).AddDays(1);
                        year = next.Year;
                        month = next.Month;
                        day = next.Day;
                    }
                    break;
                default:
                    data = LoadAndGenerate(path, year, month, day, datePrefix);
                    break;
            }

            ExportBlacklist(); //Update local file with auto-blacklisted players

            if (workType != WorkType.UpdateCurrent && data != null)
            {
                //Save player data
                ExportPlayers(year, month, day, path);
                //Generate player pages
                if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + FILE_ADDITIONS))
                {
                    List<PlayerAddition> adds = DeserializeFromFile<List<PlayerAddition>>(AppDomain.CurrentDomain.BaseDirectory + FILE_ADDITIONS, "Player Profile Additions");
                    if (adds != null)
                    {
                        for (int i = 0; i < adds.Count; i++) additions.Add(adds[i].id, adds[i]);
                    }
                }
                PageWriter pageWriter = new PageWriter();
                Log("Writing player pages.");
                foreach(KeyValuePair<ulong, PlayerInfo> kp in players)
                {
                    //When just doing a daily update, only rewrite the pages for players who participated in that daily
                    if (workType == WorkType.GetDaily)
                    {
                        bool hasParticipated = false;
                        for (int i = 0; i < data.allEntries.Length; i++)
                        {
                            if (data.allEntries[i].id == kp.Key)
                            {
                                hasParticipated = true;
                                break;
                            }
                        }
                        if (!hasParticipated) continue;
                    }
                    //Generate and write
                    WriteFile(path + Convert.ToString((long)kp.Key, 16).PadLeft(16, '0') + ".html", pageWriter.GeneratePlayerPage(kp.Value), true);
                }
                Log("Finished writing player pages.");
            }

            Log("Done!");
#if DEBUG
            Console.ReadLine();
#endif
        }

        static void AutoBlacklist(LeaderboardData data)
        {
            for (int i = 0; i < data.allEntries.Length; i++)
            {
                PlayerEntry e = data.allEntries[i];
                string reasonToAdd = null;
                if (e.score > 19000000)
                {
                    reasonToAdd = "Score over $19 million (" + e.GetScore() + ").";
                }
                else if (e.runend == RunEndCause.NormalClear && e.runframes < 90 * 60)
                {
                    reasonToAdd = "Normal clear in under one and a half minutes (" + e.GetTime() + ").";
                }
                else if (e.runend == RunEndCause.HardClear && e.runframes < 3 * 60 * 60)
                {
                    reasonToAdd = "Hard clear in under three minutes (" + e.GetTime() + ").";
                }
                else if (e.runend == RunEndCause.SpecialClear && e.runframes < 20 * 60 * 60)
                {
                    reasonToAdd = "Special clear in under 20 minutes (" + e.GetTime() + ").";
                }
                else if (e.level > 4 && e.runframes < e.level * 3 * 60)
                {
                    reasonToAdd = "Less than 3 seconds per level average (" + e.GetLevel() + " in " + e.GetTime() + ").";
                }

                if (reasonToAdd != null)
                {
                    if (AddToBlacklist(e.id)) BlacklistLog("Adding " + e.name + " (" + Convert.ToString((long)e.id, 16) + ") to blacklist: " + reasonToAdd);
                }
            }
        }

        static bool AddToBlacklist(ulong id)
        {
            for (int i = 0; i < blacklist.Count; i++) if (blacklist[i] == id) return false;
            blacklist.Add(id);
            return true;
        }

        static void ExportPlayers(int year, int month, int day, string path)
        {
#if FALSE//DEBUG
            Log("Player Data serialization disabled in debug mode! (It takes ages.)");
            return;
#endif
            //Need to convert to list to serialize, ulong dicts aren't supported :(
            List<PlayerInfo> playerInfos = new List<PlayerInfo>();
            foreach (KeyValuePair<ulong, PlayerInfo> kp in players)
            {
                playerInfos.Add(kp.Value);
            }
            SerializeToFile(AppDomain.CurrentDomain.BaseDirectory + FILE_PLAYERS, playerInfos, "Player Data");
            //Dump individual players
            int now = Program.TimestampToInt(year, month, day);
            for (int i = 0; i < playerInfos.Count; i++)
            {
                PlayerInfo pi = playerInfos[i];
                if (pi.entries.Count == 0) continue;
                bool hasTodayEntry = false;
                for (int j = 0; j < pi.entries.Count; j++) if (pi.entries[j].timestamp == now) hasTodayEntry = true;
                if (!hasTodayEntry) continue; //Was not updated today, don't re-dump
                SerializeToFile(path + Convert.ToString((long)pi.id, 16).PadLeft(16, '0') + ".json", pi, null);
            }
        }

        static void ExportBlacklist()
        {
            SerializeToFile(AppDomain.CurrentDomain.BaseDirectory + FILE_BLACKLIST, blacklist, "Blacklist");
        }

        static LeaderboardData LoadAndGenerate(string path, int year, int month, int day, string datePrefix)
        {
            bool ongoing = (workType == WorkType.UpdateCurrent);

            //Load entries
            LeaderboardData data = LoadEntries(path, year, month, day);
            Log("Finished reading data.");
            AutoBlacklist(data);
            //Filter pirates and blacklisted players
            data.FilterPirates();
            data.FilterBlacklisted();
            //Prepare extended entries
            Dictionary<ulong, ExtendedPlayerEntry> extendedEntries = new Dictionary<ulong, ExtendedPlayerEntry>();
			for (int i = 0; i < data.allEntries.Length; i++)
			{
				PlayerEntry entry = data.allEntries[i];
				if (extendedEntries.ContainsKey(entry.id))
				{
					Log("WARNING: Removing duplicate entry for player " + entry.name + " (" + entry.id + "; " + entry.platform +")!");
					extendedEntries.Remove(entry.id); //Remove the former as the rest of the code effectively works with the latter
				}
				extendedEntries.Add(entry.id, new ExtendedPlayerEntry(entry, year, month, day));
			}
            //Prepare page writer
            PageWriter pw = new PageWriter(data.allEntries, year, month, day, ongoing);
            //Depth sort
            data.SortByDepth("all entries");
            if (dataSource != DataSource.Json)
            {
                if (ongoing) data.ExportJson(path + FILE_CURRENT);
                else data.ExportJson(path + GetYYYYMMDD(year, month, day) + ".json");
            }
            WriteFile(path + datePrefix + "depth.html", pw.GeneratePage(datePrefix + "depth.html", PageType.Depth, "depth.html", path));
            if (workType == WorkType.UpdateCurrent) File.Copy(path + datePrefix + "depth.html", path + "depth.html", true);
            for (int i = 0; i < data.allEntries.Length; i++) extendedEntries[data.allEntries[i].id].levelRank = i + 1;
            //Score sort
            data.SortByScore("all entries");
            WriteFile(path + datePrefix + "score.html", pw.GeneratePage(datePrefix + "score.html", PageType.Score, "score.html", path));
            if (workType == WorkType.UpdateCurrent) File.Copy(path + datePrefix + "score.html", path + "score.html", true);
            for (int i = 0; i < data.allEntries.Length; i++) extendedEntries[data.allEntries[i].id].scoreRank = i + 1;
            //Normal, Hard and CO sort
            data.SortByTime(" all entries ");
            WriteFile(path + datePrefix + "time.html", pw.GeneratePage(datePrefix + "time.html", PageType.Time, "time.html", path));
            if (workType == WorkType.UpdateCurrent) File.Copy(path + datePrefix + "time.html", path + "time.html", true);
            int normalRank = 1;
            int hardRank = 1;
            int specialRank = 1;
            for (int i = 0; i < data.allEntries.Length; i++)
            {
                ulong id = data.allEntries[i].id;
                switch(data.allEntries[i].runend)
                {
                    case RunEndCause.NormalClear:
                        extendedEntries[id].normalTimeRank = normalRank;
                        normalRank++;
                        break;
                    case RunEndCause.HardClear:
                        extendedEntries[id].hardTimeRank = hardRank;
                        hardRank++;
                        break;
                    case RunEndCause.SpecialClear:
                        extendedEntries[id].specialTimeRank = specialRank;
                        specialRank++;
                        break;
                }
            }
            //Stats
            WriteFile(path + datePrefix + "stats.html", pw.GenerateStats(datePrefix + "stats.html"));
            if (workType == WorkType.UpdateCurrent) File.Copy(path + datePrefix + "stats.html", path + "stats.html", true);
            //Add entries to players
            if (workType != WorkType.UpdateCurrent)
            {
                foreach (KeyValuePair<ulong, ExtendedPlayerEntry> kp in extendedEntries)
                {
                    if (!players.ContainsKey(kp.Key)) players.Add(kp.Key, new PlayerInfo());
                    players[kp.Key].AddEntry(kp.Value);
                }
            }

            return data;
        }

        static void LoadPlayerData()
        {
            players = new Dictionary<ulong, PlayerInfo>();
            if (!File.Exists(AppDomain.CurrentDomain.BaseDirectory + FILE_PLAYERS))
            {
                Log("Players file '" + FILE_PLAYERS + "' does not exist! Skipping player data loading.");
                return;
            }
            List<PlayerInfo> playerInfos = DeserializeFromFile<List<PlayerInfo>>(AppDomain.CurrentDomain.BaseDirectory + FILE_PLAYERS, "Player Data");
            for (int i = 0; i < playerInfos.Count; i++) players.Add(playerInfos[i].id, playerInfos[i]);
        }

        static void LoadBlacklist()
        {
            blacklist.Clear();
            if (!File.Exists(AppDomain.CurrentDomain.BaseDirectory + FILE_BLACKLIST))
            {
                Log("Blacklist file '" + FILE_BLACKLIST + "' does not exist! Generating new blacklist!");
                return;
            }
            blacklist = DeserializeFromFile<List<ulong>>(AppDomain.CurrentDomain.BaseDirectory + FILE_BLACKLIST, "Blacklist");
        }

        static LeaderboardData LoadEntries(string path, int year, int month, int day)
        {
            //Load data
            switch (dataSource)
            {
                case DataSource.Download: return LeaderboardData.FromDownload(year, month, day);
                case DataSource.LocalFile:
                    {
                        LeaderboardData toReturn = LeaderboardData.FromCompressedDataFile("local");
                        if (toReturn == null) throw new FileNotFoundException("Could not load 'local' file for non-download run!");
                        return toReturn;
                    }
                case DataSource.Json:
                    {
                        string jsonPath = null;
                        switch (workType)
                        {
                            case WorkType.GetDaily:
                            case WorkType.Rebuild:
                                jsonPath = path + GetYYYYMMDD(year, month, day) + ".json";
                                break;
                            case WorkType.UpdateCurrent:
                                jsonPath = path + FILE_CURRENT;
                                break;
                        }
                        LeaderboardData toReturn = LeaderboardData.FromJSONFile(jsonPath);
                        if (toReturn == null) throw new FileNotFoundException("Could not load JSON file '" + jsonPath + "'!");
                        return toReturn;
                    }
            }

            Program.Log("Could not load data.");
            throw new Exception();
        }

        public static void WriteFile(string filename, string content, bool silent = false)
        {
            if (!silent) Log("Writing " + filename);
            StreamWriter sw = new StreamWriter(filename);
            sw.Write(content);
            sw.Close();
        }

        public static string GetYYYYMMDD(DateTime dt)
        {
            return GetYYYYMMDD(dt.Year, dt.Month, dt.Day);
        }

        public static string GetYYYYMMDD(int year, int month, int day)
        {
            return Convert.ToString(year) + "-" + Convert.ToString(month).PadLeft(2, '0') + "-" + Convert.ToString(day).PadLeft(2, '0');
        }

        public static int TimestampToInt(int year, int month, int day)
        {
            return year * 10000 + month * 100 + day;
        }

        public static string IntTimestampToString(int timestamp)
        {
            string raw = Convert.ToString(timestamp);
            return raw.Substring(0, 4) + "-" + raw.Substring(4, 2) + "-" + raw.Substring(6, 2);
        }

        private static void InitSerializer()
        {
            if (jss != null) return;
            jss = new JavaScriptSerializer();
            jss.MaxJsonLength = 2000 * 1024 * 1024;
        }

        public static string Serialize(object obj, string description)
        {
            if (description != null) Program.Log("Serializing " + description + "...");
            InitSerializer();
            string json = jss.Serialize(obj);
            if (description != null) Program.Log("Serialization of " + description + " successful.");
            return json;
        }

        public static void SerializeToFile(string filename, object obj, string description)
        {
            string json = Serialize(obj, description);
            if (description != null) Program.Log("Writing JSON for " + description + " to " + filename + "...");
            Program.WriteFile(filename, json);
            if (description != null) Program.Log("Finished writing JSON for " + description + ".");
        }

        public static T Deserialize<T>(string json, string description)
        {
            InitSerializer();
            Program.Log("Deserializing " + description + "...");
            T toReturn = jss.Deserialize<T>(json);
            if (toReturn == null) throw new Exception("Could not deserialize JSON for " + description + "!");
            Program.Log("Deserialization of " + description + " successful.");
            return toReturn;
        }

        public static T DeserializeFromFile<T>(string filename, string description)
        {
            Program.Log("Reading " + filename + " for " + description + " deserialization...");
            StreamReader sr = new StreamReader(filename);
            string json = sr.ReadToEnd();
            sr.Close();
            return Deserialize<T>(json, description);
        }

        public static void Log(string msg)
        {
            msg = "[" + DateTime.UtcNow.ToString() + "] " + msg;
            if (!silent) Console.WriteLine(msg);
        }

        const string FILE_LOG_BLACKLIST = "blacklist.log";
        static StreamWriter loggerBlacklist = null;
        static void BlacklistLog(string msg)
        {
            if (loggerBlacklist == null) loggerBlacklist = new StreamWriter(AppDomain.CurrentDomain.BaseDirectory + FILE_LOG_BLACKLIST, true);
            msg = "[" + DateTime.UtcNow.ToString() + "] " + msg;
            if (!silent) Console.WriteLine(msg);
            loggerBlacklist.WriteLine(msg);
            loggerBlacklist.Flush();
        }
    }
}
