using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spelunky2LeaderboardGenerator
{
    public class Config
    {
        public string dataurl = "https://cdn.spelunky2.net/static/{YEAR}{MONTH}{DAY}";
        public string useragent = "Spelunky2LeaderBoardGenerator/0.6";
    }

    public class PlayerEntry
    {
        public ulong id = 0;
        public string name = "Unknown player";
        public int score = 0;
        public int level = 1;
        public Character character = Character.Unknown;
        public Platform platform = Platform.Unknown;
        public int runframes = 9 * 60 * 60 * 60 + 59 * 60 * 60 + 59 * 60; //9:59:99
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
            return GetTime((long)frames);
        }

        public static string GetTime(long frames)
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

    public class ExtendedPlayerEntry : PlayerEntry
    {
        public int timestamp = -1;
        public int levelRank = -1;
        public int scoreRank = -1;
        public int normalTimeRank = -1;
        public int hardTimeRank = -1;
        public int specialTimeRank = -1;

        public ExtendedPlayerEntry() { }

        public ExtendedPlayerEntry(PlayerEntry pe, int year, int month, int day)
        {
            this.timestamp = Program.TimestampToInt(year, month, day);

            this.id = pe.id;
            this.name = pe.name;
            this.score = pe.score;
            this.level = pe.level;
            this.character = pe.character;
            this.platform = pe.platform;
            this.runframes = pe.runframes;
            this.runend = pe.runend;
            this.unknown1 = pe.unknown1;
            this.unknown2 = pe.unknown2;
        }
    }

    public class PlayerInfo
    {
        public ulong id = 0;
        public string name = "Unknown player";
        public List<string> previousNames = new List<string>();
        public List<ExtendedPlayerEntry> entries = new List<ExtendedPlayerEntry>();
        private Dictionary<int, ExtendedPlayerEntry> indexedEntries = null;

        public int bestLevel = -1;
        public List<int> bestLevelDates = new List<int>();
        public int bestScore = -1;
        public int bestScoreDate = -1;
        public int bestNormalTime = -1;
        public int bestNormalTimeDate = -1;
        public int bestHardTime = -1;
        public int bestHardTimeDate = -1;
        public int bestSpecialTime = -1;
        public int bestSpecialTimeDate = -1;

        public int bestLevelRank = -1;
        public List<int> bestLevelRankDates = new List<int>();
        public int bestScoreRank = -1;
        public List<int> bestScoreRankDates = new List<int>();
        public int bestNormalTimeRank = -1;
        public List<int> bestNormalTimeRankDates = new List<int>();
        public int bestHardTimeRank = -1;
        public List<int> bestHardTimeRankDates = new List<int>();
        public int bestSpecialTimeRank = -1;
        public List<int> bestSpecialTimeRankDates = new List<int>();

        public List<int> topLevels = new List<int>();
        public float topLevelsAverage = 1;
        public List<int> topScores = new List<int>();
        public int topScoresAverage = 0; //Just round it
        public List<int> topTimes = new List<int>();
        public int topTimesAverage = 9 * 60 * 60 * 60 + 59 * 60 * 60 + 59 * 60; //9:59:99, also just round it

        public void AddEntry(ExtendedPlayerEntry epe)
        {
            //Add entry
            ExtendedPlayerEntry prevEntry = GetEntry(epe.timestamp);
            if (prevEntry != null)
            {
                entries.Remove(prevEntry);
                indexedEntries.Remove(prevEntry.timestamp);
            }
            entries.Add(epe);
            indexedEntries.Add(epe.timestamp, epe);

            //Set ID in case it hasn't been set yet
            this.id = epe.id;

            //Adjust name in case it's changed
            if (this.name != epe.name)
            {
                if (this.name != "Unknown player") this.previousNames.Add(this.name);
                this.name = epe.name;
            }

            //Adjust best level
            if (epe.level > this.bestLevel)
            {
                this.bestLevel = epe.level;
                this.bestLevelDates.Clear();
                this.bestLevelDates.Add(epe.timestamp);
            }
            else if (epe.level == this.bestLevel)
            {
                bool alreadyHas = false;
                for (int i = 0; i < this.bestLevelDates.Count; i++) if (this.bestLevelDates[i] == epe.timestamp) alreadyHas = true;
                if (!alreadyHas) this.bestLevelDates.Add(epe.timestamp);
            }
            //Adjust best score
            if (epe.score > this.bestScore)
            {
                this.bestScore = epe.score;
                this.bestScoreDate = epe.timestamp;
            }
            //Adjust best times
            switch(epe.runend)
            {
                case RunEndCause.NormalClear:
                    if (epe.runframes < this.bestNormalTime || this.bestNormalTime == -1)
                    {
                        this.bestNormalTime = epe.runframes;
                        this.bestNormalTimeDate = epe.timestamp;
                    }
                    break;
                case RunEndCause.HardClear:
                    if (epe.runframes < this.bestHardTime || this.bestHardTime == -1)
                    {
                        this.bestHardTime = epe.runframes;
                        this.bestHardTimeDate = epe.timestamp;
                    }
                    break;
                case RunEndCause.SpecialClear:
                    if (epe.runframes < this.bestSpecialTime || this.bestSpecialTime == -1)
                    {
                        this.bestSpecialTime = epe.runframes;
                        this.bestSpecialTimeDate = epe.timestamp;
                    }
                    break;
            }

            //Adjust best level rank
            if (epe.levelRank <= this.bestLevelRank || this.bestLevelRank == -1)
            {
                if (epe.levelRank < this.bestLevelRank) this.bestLevelRankDates.Clear();
                this.bestLevelRank = epe.levelRank;
                bool alreadyHas = false;
                for (int i = 0; i < this.bestLevelRankDates.Count; i++) if (this.bestLevelRankDates[i] == epe.timestamp) alreadyHas = true;
                if (!alreadyHas) this.bestLevelRankDates.Add(epe.timestamp);
            }
            //Adjust best score rank
            if (epe.scoreRank <= this.bestScoreRank || this.bestScoreRank == -1)
            {
                if (epe.scoreRank < this.bestScoreRank) this.bestScoreRankDates.Clear();
                this.bestScoreRank = epe.scoreRank;
                bool alreadyHas = false;
                for (int i = 0; i < this.bestScoreRankDates.Count; i++) if (this.bestScoreRankDates[i] == epe.timestamp) alreadyHas = true;
                if (!alreadyHas) this.bestScoreRankDates.Add(epe.timestamp);
            }
            //Adjust best time ranks
            if (epe.normalTimeRank != -1 && (epe.normalTimeRank <= this.bestNormalTimeRank || this.bestNormalTimeRank == -1))
            {
                if (epe.normalTimeRank < this.bestNormalTimeRank) this.bestNormalTimeRankDates.Clear();
                this.bestNormalTimeRank = epe.normalTimeRank;
                bool alreadyHas = false;
                for (int i = 0; i < this.bestNormalTimeRankDates.Count; i++) if (this.bestNormalTimeRankDates[i] == epe.timestamp) alreadyHas = true;
                if (!alreadyHas) this.bestNormalTimeRankDates.Add(epe.timestamp);
            }
            if (epe.hardTimeRank != -1 && (epe.hardTimeRank <= this.bestHardTimeRank || this.bestHardTimeRank == -1))
            {
                if (epe.hardTimeRank < this.bestHardTimeRank) this.bestHardTimeRankDates.Clear();
                this.bestHardTimeRank = epe.hardTimeRank;
                bool alreadyHas = false;
                for (int i = 0; i < this.bestHardTimeRankDates.Count; i++) if (this.bestHardTimeRankDates[i] == epe.timestamp) alreadyHas = true;
                if (!alreadyHas) this.bestHardTimeRankDates.Add(epe.timestamp);
            }
            if (epe.specialTimeRank != -1 && (epe.specialTimeRank <= this.bestSpecialTimeRank || this.bestSpecialTimeRank == -1))
            {
                if (epe.specialTimeRank < this.bestSpecialTimeRank) this.bestSpecialTimeRankDates.Clear();
                this.bestSpecialTimeRank = epe.specialTimeRank;
                bool alreadyHas = false;
                for (int i = 0; i < this.bestSpecialTimeRankDates.Count; i++) if (this.bestSpecialTimeRankDates[i] == epe.timestamp) alreadyHas = true;
                if (!alreadyHas) this.bestSpecialTimeRankDates.Add(epe.timestamp);
            }

            //Get top entries
            List<ExtendedPlayerEntry> adjustedEntries = null;
            if (entries.Count > 9) adjustedEntries = entries;
            else adjustedEntries = new List<ExtendedPlayerEntry>(entries);
            int num = 1;
            while (adjustedEntries.Count < 10)
            {
                adjustedEntries.Add(new ExtendedPlayerEntry(new PlayerEntry(), 0, 0, num)); //Avoid duplicate timestamps
                num++;
            }
            //Top 10 depths
            adjustedEntries.Sort((x, y) => (y.level.CompareTo(x.level)));
            topLevels.Clear();
            topLevelsAverage = 0;
            for (int i = 0; i < 10; i++)
            {
                topLevels.Add(adjustedEntries[i].timestamp);
                topLevelsAverage += adjustedEntries[i].level;
            }
            topLevelsAverage /= 10f;
            //Top 10 scores
            adjustedEntries.Sort((x, y) => (y.score.CompareTo(x.score)));
            topScores.Clear();
            topScoresAverage = 0;
            for (int i = 0; i < 10; i++)
            {
                topScores.Add(adjustedEntries[i].timestamp);
                topScoresAverage += adjustedEntries[i].score;
            }
            topScoresAverage = Convert.ToInt32(Math.Round((float)topScoresAverage / 10f));
            //Top 5 times
            List<ExtendedPlayerEntry> normalClears = new List<ExtendedPlayerEntry>();
            List<ExtendedPlayerEntry> hardClears = new List<ExtendedPlayerEntry>();
            List<ExtendedPlayerEntry> specialClears = new List<ExtendedPlayerEntry>();
            List<ExtendedPlayerEntry> allClears = new List<ExtendedPlayerEntry>();
            for (int i = 0; i < adjustedEntries.Count; i++)
            {
                switch (adjustedEntries[i].runend)
                {
                    case RunEndCause.NormalClear:
                        normalClears.Add(adjustedEntries[i]);
                        allClears.Add(adjustedEntries[i]);
                        break;
                    case RunEndCause.HardClear:
                        hardClears.Add(adjustedEntries[i]);
                        allClears.Add(adjustedEntries[i]);
                        break;
                    case RunEndCause.SpecialClear:
                        specialClears.Add(adjustedEntries[i]);
                        allClears.Add(adjustedEntries[i]);
                        break;
                }
            }
            while (allClears.Count < 5) allClears.Add(new ExtendedPlayerEntry(new PlayerEntry(), 0, 0, 0));
            allClears.Sort((x, y) => (x.runframes.CompareTo(y.runframes)));
            topTimes.Clear();
            topTimesAverage = 0;
            for (int i = 0; i < 5; i++)
            {
                topTimes.Add(allClears[i].timestamp);
                topTimesAverage += allClears[i].runframes;
            }
            topTimesAverage = Convert.ToInt32(Math.Round((float)topTimesAverage / 5f));
        }

        public ExtendedPlayerEntry GetEntry(int timestamp)
        {
            if (indexedEntries == null)
            {
                indexedEntries = new Dictionary<int, ExtendedPlayerEntry>();
                for (int i = 0; i < entries.Count; i++)
                {
                    indexedEntries.Add(entries[i].timestamp, entries[i]);
                }
            }
            if (!indexedEntries.ContainsKey(timestamp)) return null;
            return indexedEntries[timestamp];
        }
    }

    public class PlayerAddition
    {
        public ulong id = 0;
        public string html = string.Empty;
    }

    public class Accomplishment
    {
        public string name;
        public string description;
        public string icon;

        public Accomplishment(string name, string description, string icon)
        {
            this.name = name;
            this.description = description;
            this.icon = icon;
        }
    }
}
