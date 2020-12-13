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

namespace Spelunky2LeaderboardGenerator
{
    class Program
    {
        public const string FILE_CURRENT = "current.json";
        //September 14th, 2020 was the first daily
        public const int FIRSTRUN_YEAR = 2020;
        public const int FIRSTRUN_MONTH = 9;
        public const int FIRSTRUN_DAY = 14;

        public static Config config = new Config();

        static WorkType workType = WorkType.Unknown;
        static DataSource dataSource = DataSource.LocalFile;
        static bool silent = false;

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
                    while (!(year == now.Year && month == now.Month && day == now.Day))
                    {
                        Log("Regenerating daily for " + GetYYYYMMDD(year, month, day));
                        LoadAndGenerate(path, year, month, day, GetYYYYMMDD(year, month, day) + "_");
                        DateTime next = new DateTime(year, month, day, 12, 00, 00).AddDays(1);
                        year = next.Year;
                        month = next.Month;
                        day = next.Day;
                    }
                    break;
                default:
                    LoadAndGenerate(path, year, month, day, datePrefix);
                    break;
            }

            Log("Done!");
#if DEBUG
            Console.ReadLine();
#endif
        }

        static void LoadAndGenerate(string path, int year, int month, int day, string datePrefix)
        {
            //Load entries
            LeaderboardData data = LoadEntries(path, year, month, day);
            Log("Finished reading data.");
            //Filter pirates
            data.FilterPirates();
            //Prepare page writer
            bool ongoing = (workType == WorkType.UpdateCurrent);
            PageWriter pw = new PageWriter(data.allEntries, year, month, day, ongoing);
            //Depth sort
            data.SortByDepth("all entries");
            if (dataSource != DataSource.Json)
            {
                if (ongoing) data.ExportJson(path + FILE_CURRENT);
                else data.ExportJson(path + GetYYYYMMDD(year, month, day) + ".json");
            }
            WriteFile(path + datePrefix + "depth.html", pw.GeneratePage(datePrefix + "depth.html", PageType.Depth, "depth.html"));
            //Score sort
            data.SortByScore("all entries");
            WriteFile(path + datePrefix + "score.html", pw.GeneratePage(datePrefix + "score.html", PageType.Score, "score.html"));
            //Normal, Hard and CO sort
            data.SortByTime(" all entries ");
            WriteFile(path + datePrefix + "time.html", pw.GeneratePage(datePrefix + "time.html", PageType.Time, "time.html"));
            //Stats
            WriteFile(path + datePrefix + "stats.html", pw.GenerateStats(datePrefix + "stats.html"));
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

        public static void WriteFile(string filename, string content)
        {
            Log("Writing " + filename);
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

        public static void Log(string msg)
        {
            msg = "[" + DateTime.UtcNow.ToString() + "] " + msg;
            if (!silent) Console.WriteLine(msg);
        }
    }
}
