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
        public string useragent = "Spelunky2LeaderBoardGenerator/0.5";
    }

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
}
