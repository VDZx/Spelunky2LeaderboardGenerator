using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spelunky2LeaderboardGenerator
{
    public class PageWriter
    {
        const int MAX_LEVEL = 117;
        const int NUM_CHARACTERS = 0x14;
        const int COSMIC_OCEAN = 23;

        PlayerEntry[] entries = { };
        DateTime dateTime = DateTime.UtcNow;
        bool ongoing = false;
        StringBuilder stringBuilder = new StringBuilder();

        public PageWriter(PlayerEntry[] entries, int year, int month, int day, bool ongoing)
        {
            this.entries = entries;
            this.dateTime = new DateTime(year, month, day, 12, 00, 00);
            this.ongoing = ongoing;
        }

        public string GeneratePage(string description, PageType pageType, string pageFile)
        {
            Program.Log("Generating " + description);
            stringBuilder = new StringBuilder();

            WriteHeader(pageType);
            //--BODY--
            stringBuilder.AppendLine("<body><center>");
            stringBuilder.Append("<font size=\"7\">");
            stringBuilder.Append(pageType);
            stringBuilder.Append(" ranking for ");
            stringBuilder.Append(dateTime.Day);
            stringBuilder.Append(" ");
            stringBuilder.Append(dateTime.ToString("MMMM", System.Globalization.CultureInfo.CreateSpecificCulture("en")));
            stringBuilder.Append(" ");
            stringBuilder.Append(dateTime.Year);
            stringBuilder.AppendLine("</font><br />");
            if (ongoing)
            {
                stringBuilder.AppendLine("(Ongoing)<br />");
            }
            WriteDateNavigation(pageFile);
            stringBuilder.Append("<br /><br />");
            if (ongoing)
            {
                stringBuilder.Append("<div id=\"count\" >Loading...</div><br /><br />");
            }
            WriteTypeNavigation(pageType);
            stringBuilder.AppendLine("<br /><br />");
            switch (pageType)
            {
                case PageType.Depth:
                case PageType.Score:
                    stringBuilder.AppendLine("<table border=\"1\">");
                    for (int i = 0; i < entries.Length; i++)
                    {
                        WriteEntry(entries[i], i + 1, pageType);
                    }
                    stringBuilder.AppendLine("</table>");
                    break;
                case PageType.Time:
                    List<PlayerEntry> coEntries = new List<PlayerEntry>();
                    List<PlayerEntry> hardEntries = new List<PlayerEntry>();
                    List<PlayerEntry> normalEntries = new List<PlayerEntry>();
                    for (int i = 0; i < entries.Length; i++)
                    {
                        switch (entries[i].runend)
                        {
                            case RunEndCause.COClear: coEntries.Add(entries[i]); break;
                            case RunEndCause.HardClear: hardEntries.Add(entries[i]); break;
                            case RunEndCause.NormalClear: normalEntries.Add(entries[i]); break;
                        }
                    }
                    WriteTimeRanking(stringBuilder, coEntries, "Special", pageType);
                    stringBuilder.AppendLine("<br /><br />");
                    WriteTimeRanking(stringBuilder, hardEntries, "Hard", pageType);
                    stringBuilder.AppendLine("<br /><br />");
                    WriteTimeRanking(stringBuilder, normalEntries, "Normal", pageType);
                    stringBuilder.AppendLine("<br /><br />");

                    break;
            }

            WriteFooter(pageType, true);

            //Write to file
            return stringBuilder.ToString();
        }

        public string GenerateStats(string description)
        {
            Program.Log("Generating " + description);
            stringBuilder = new StringBuilder();

            WriteHeader(PageType.Stats);
            //--BODY--
            stringBuilder.AppendLine("<body><center>");
            stringBuilder.Append("<font size=\"7\">Stats for ");
            stringBuilder.Append(dateTime.Day);
            stringBuilder.Append(" ");
            stringBuilder.Append(dateTime.ToString("MMMM", System.Globalization.CultureInfo.CreateSpecificCulture("en")));
            stringBuilder.Append(" ");
            stringBuilder.Append(dateTime.Year);
            stringBuilder.AppendLine("</font><br />");
            if (ongoing)
            {
                stringBuilder.AppendLine("(Ongoing)<br />");
            }
            WriteDateNavigation("stats.html");
            stringBuilder.Append("<br /><br />");
            if (ongoing)
            {
                stringBuilder.Append("<div id=\"count\" >Loading...</div><br /><br />");
            }
            WriteTypeNavigation(PageType.Stats);
            stringBuilder.AppendLine("<br /><br />");

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
            stringBuilder.AppendLine("<font size=\"6\">Level deadliness</font><br />");
            if (!ongoing)
            {
                stringBuilder.Append("(You can also check out <a href=\"https://jhay.net/spelunky2daily/?day=");
                stringBuilder.Append(Program.GetYYYYMMDD(dateTime));
                stringBuilder.AppendLine("\">JeremyHay's site</a> for more survival stats.)<br />");
            }
            stringBuilder.AppendLine("<br />");
            stringBuilder.AppendLine("<table border=\"1\">");
            stringBuilder.AppendLine("<tr class=\"top\"><td></td><td>Level</td><td>Survivors</td><td>Deaths</td><td>Death rate</td></tr>");
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

                    stringBuilder.Append("<tr");
                    if (deathRate > 0.5f) stringBuilder.Append(" class=\"deadly\"");
                    else if (deathRate > 0.3f) stringBuilder.Append(" class=\"danger\"");
                    stringBuilder.Append("><td class=\"w");
                    string levelName = PlayerEntry.GetLevel(i);
                    stringBuilder.Append(levelName.Substring(0, 1));
                    if (i >= COSMIC_OCEAN) stringBuilder.Append("5");
                    stringBuilder.Append("\" /><td>");
                    stringBuilder.Append(levelName);
                    stringBuilder.Append("</td><td>");
                    stringBuilder.Append(survivors);
                    stringBuilder.Append("</td><td>");
                    stringBuilder.Append(deaths);
                    stringBuilder.Append("</td><td>");
                    stringBuilder.Append(Convert.ToString(Math.Round(deathRate * 100f, 1)) + "%");
                    stringBuilder.AppendLine("</td></tr>");
                }
            }
            stringBuilder.AppendLine("</table><br/><br/>");

            //Characters
            List<KeyValuePair<Character, List<PlayerEntry>>> characterList = characterEntries.ToList(); //Sorry :(
            Dictionary<Character, string> characterPlayerNames = new Dictionary<Character, string>();

            //Character usage
            stringBuilder.AppendLine("<font size=\"6\">Character usage</font><br /><br />");
            stringBuilder.AppendLine("<table border=\"1\">");
            characterList.Sort((x, y) => y.Value.Count.CompareTo(x.Value.Count));
            for (int i = 0; i < characterList.Count; i++)
            {
                stringBuilder.Append("<tr><td>");
                stringBuilder.Append(i + 1);
                stringBuilder.Append("</td><td class=\"c");
                stringBuilder.Append(Convert.ToString((int)characterList[i].Key, 16).PadLeft(2, '0'));
                stringBuilder.Append("\" /><td>");
                stringBuilder.Append(characterList[i].Value.Count);
                stringBuilder.Append("</td><td>");
                stringBuilder.Append(Math.Round(((float)characterList[i].Value.Count / (float)entries.Length) * 100f, 1));
                stringBuilder.AppendLine("%</td></tr>");
            }
            stringBuilder.AppendLine("</table><br/><br/>");

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
            stringBuilder.AppendLine("<font size=\"6\">Character best depth</font><br /><br />");
            stringBuilder.AppendLine("<table border=\"1\">");
            for (int i = 0; i < characterList.Count; i++)
            {
                stringBuilder.Append("<tr><td>");
                stringBuilder.Append(i + 1);
                stringBuilder.Append("</td><td class=\"c");
                stringBuilder.Append(Convert.ToString((int)characterDepths[i].Key, 16).PadLeft(2, '0'));
                stringBuilder.Append("\" /><td>");
                stringBuilder.Append(characterPlayerNames[characterDepths[i].Key]);
                stringBuilder.Append("</td><td>");
                stringBuilder.Append(PlayerEntry.GetLevel(characterDepths[i].Value));
                stringBuilder.AppendLine("</td></tr>");
            }
            stringBuilder.AppendLine("</table><br/><br/>");

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
            stringBuilder.AppendLine("<font size=\"6\">Character best score</font><br /><br />");
            stringBuilder.AppendLine("<table border=\"1\">");
            for (int i = 0; i < characterList.Count; i++)
            {
                stringBuilder.Append("<tr><td>");
                stringBuilder.Append(i + 1);
                stringBuilder.Append("</td><td class=\"c");
                stringBuilder.Append(Convert.ToString((int)characterScores[i].Key, 16).PadLeft(2, '0'));
                stringBuilder.Append("\" /><td>");
                stringBuilder.Append(characterPlayerNames[characterScores[i].Key]);
                stringBuilder.Append("</td><td>");
                stringBuilder.Append(PlayerEntry.GetScore(characterScores[i].Value));
                stringBuilder.AppendLine("</td></tr>");
            }
            stringBuilder.AppendLine("</table><br/><br/>");

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
                    switch (entry.runend)
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
            stringBuilder.AppendLine("<font size=\"6\">Character best time</font><br /><br />");
            stringBuilder.AppendLine("<table border=\"1\">");
            for (int i = 0; i < characterList.Count; i++)
            {
                bool finished = false;
                if (characterTimes[i].Value != maxTime) finished = true;
                stringBuilder.Append("<tr><td>");
                stringBuilder.Append(i + 1);
                stringBuilder.Append("</td><td class=\"c");
                stringBuilder.Append(Convert.ToString((int)characterTimes[i].Key, 16).PadLeft(2, '0'));
                stringBuilder.Append("\" /><td>");
                if (finished) stringBuilder.Append(characterPlayerNames[characterTimes[i].Key]);
                else stringBuilder.Append("<i>N/A</i>");
                stringBuilder.Append("</td><td>");
                if (finished) stringBuilder.Append(PlayerEntry.GetTime(characterTimes[i].Value));
                else stringBuilder.Append("<i>did not finish</i>");
                stringBuilder.AppendLine("</td></tr>");
            }
            stringBuilder.AppendLine("</table><br/><br/>");

            //Finish
            WriteFooter(PageType.Stats, true);

            //Write to file
            return stringBuilder.ToString();
        }

        public void WriteHeader(PageType pageType)
        {
            stringBuilder.AppendLine("<html>");
            //--HEAD--
            stringBuilder.AppendLine("<head>");
            stringBuilder.AppendLine("<meta charset=\"utf-8\" />");
            stringBuilder.Append("<title>");
            stringBuilder.Append(pageType);
            stringBuilder.Append(" ");
            stringBuilder.Append(dateTime.Year);
            stringBuilder.Append("-");
            stringBuilder.Append(dateTime.Month);
            stringBuilder.Append("-");
            stringBuilder.Append(dateTime.Day);
            stringBuilder.Append(" - Spelunky 2 leaderboards (unofficial)");
            stringBuilder.AppendLine("</title>");
            stringBuilder.AppendLine("<style>");
            stringBuilder.AppendLine(styleCss);
            //Character CSS
            for (int i = 0; i < Convert.ToInt32(Character.ClassicGuy) + 1; i++)
            {
                stringBuilder.Append(".c");
                stringBuilder.AppendLine(Convert.ToString(i, 16).PadLeft(2, '0'));
                stringBuilder.AppendLine("{");
                stringBuilder.Append("    background-image: url(\"");
                stringBuilder.Append(Convert.ToString((Character)i));
                stringBuilder.AppendLine(".png\");");
                stringBuilder.AppendLine("    width: 64px;");
                stringBuilder.AppendLine("    height: 32px;");
                stringBuilder.AppendLine("}"); ;
            }
            stringBuilder.AppendLine("</style></head>");
        }

        public void WriteDateNavigation(string pagefile)
        {
            if (!(dateTime.Year == Program.FIRSTRUN_YEAR && dateTime.Month == Program.FIRSTRUN_MONTH && dateTime.Day == Program.FIRSTRUN_DAY))
            {
                stringBuilder.Append("<a href=\"");
                stringBuilder.Append(Program.GetYYYYMMDD(dateTime.Subtract(new TimeSpan(1, 0, 0, 0))));
                stringBuilder.Append("_");
                stringBuilder.Append(pagefile);
                stringBuilder.Append("\">");
                stringBuilder.Append(Program.GetYYYYMMDD(dateTime.Subtract(new TimeSpan(1, 0, 0, 0))));
                stringBuilder.Append("</a>");
                stringBuilder.Append("&nbsp;&nbsp;&nbsp;&nbsp;<<&nbsp;&nbsp;&nbsp;&nbsp;");
            }
            stringBuilder.Append(Program.GetYYYYMMDD(dateTime));
            stringBuilder.Append("&nbsp;&nbsp;&nbsp;&nbsp;>>&nbsp;&nbsp;&nbsp;&nbsp;");
            if (!ongoing)
            {
                stringBuilder.Append("<a href=\"");
                DateTime tomorrow = dateTime.AddDays(1);
                if (!(tomorrow.Year == DateTime.UtcNow.Year && tomorrow.Month == DateTime.UtcNow.Month && tomorrow.Day == DateTime.UtcNow.Day))
                {
                    stringBuilder.Append(Program.GetYYYYMMDD(tomorrow));
                    stringBuilder.Append("_");
                }
                stringBuilder.Append(pagefile);
                stringBuilder.Append("\">");
            }
            stringBuilder.Append(Program.GetYYYYMMDD(dateTime.AddDays(1)));
            if (!ongoing) stringBuilder.Append("</a>");
        }

        public void WriteTypeNavigation(PageType pageType)
        {
            string datePrefix = string.Empty;
            if (!ongoing) datePrefix = Program.GetYYYYMMDD(dateTime) + "_";
            PageType[] activeTypes =
            {
                PageType.Depth,
                PageType.Score,
                PageType.Time,
                PageType.Stats
            };

            for (int i = 0; i < activeTypes.Length; i++)
            {
                if (i != 0) stringBuilder.Append(" - ");
                if (pageType != activeTypes[i])
                {
                    stringBuilder.Append("<a href=\"");
                    stringBuilder.Append(datePrefix);
                    stringBuilder.Append(Convert.ToString(activeTypes[i]).ToLower());
                    stringBuilder.Append(".html\">");
                }
                else stringBuilder.Append("<b>");
                stringBuilder.Append("[");
                stringBuilder.Append(Convert.ToString(activeTypes[i]));
                stringBuilder.Append("]");
                if (pageType != activeTypes[i]) stringBuilder.Append("</a>");
                else stringBuilder.Append("</b>");
            }
        }

        public void WriteFooter(PageType pageType, bool jsonDownloadable)
        {
            if (ongoing)
            {
                stringBuilder.AppendLine("Updates every 5 minutes.<br />");
                stringBuilder.Append("Last update: ");
            }
            else
            {
                stringBuilder.Append("Page generated on: ");
            }
            stringBuilder.Append(DateTime.UtcNow.ToString());
            stringBuilder.AppendLine(" UTC<br/>");

            if (jsonDownloadable)
            {
                stringBuilder.Append("<br/><a href=\"");
                if (ongoing) { stringBuilder.Append(Program.FILE_CURRENT); }
                else { stringBuilder.Append(Program.GetYYYYMMDD(dateTime)); stringBuilder.Append(".json"); }
                stringBuilder.AppendLine("\">(Download data in JSON format)</a>");
            }
            if (ongoing)
            {
                stringBuilder.AppendLine(countdownJS.Replace("{TODAY}", Program.GetYYYYMMDD(dateTime)).Replace("{TOMORROW}", Program.GetYYYYMMDD(dateTime.AddDays(1))).Replace("{TYPE}", Convert.ToString(pageType).ToLower()));
            }
            stringBuilder.AppendLine("</center></body>");
            stringBuilder.AppendLine("</html>");
        }

        public void WriteEntry(PlayerEntry entry, int rank, PageType sortedBy)
        {
            //Rank
            stringBuilder.Append("<tr><td>");
            stringBuilder.Append(Convert.ToString(rank));
            //Platform
            stringBuilder.Append("</td><td class=\"");
            switch (entry.platform)
            {
                case Platform.PC: stringBuilder.Append("pc"); break;
                case Platform.PS4: stringBuilder.Append("ps4"); break;
                default: stringBuilder.Append("unknown"); break;
            }
            stringBuilder.Append("\" />");
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
                stringBuilder.Append("<td>");
                stringBuilder.Append(Convert.ToString((long)entry.id, 16).PadLeft(16, '0'));
                stringBuilder.Append("</td>");
            }
#endif
            //Character
            stringBuilder.Append("<td class=\"c");
            stringBuilder.Append(Convert.ToString(Convert.ToInt16(entry.character), 16).PadLeft(2, '0'));
            stringBuilder.Append("\"/>");
            //Name
            stringBuilder.Append("<td>");
            stringBuilder.Append(entry.name.Replace("<", "").Replace(">", ""));
            switch (sortedBy)
            {
                case PageType.Depth:
                case PageType.Score:
                    //Depth
                    stringBuilder.Append("</td><td");
                    switch (entry.runend)
                    {
                        case RunEndCause.NormalClear:
                        case RunEndCause.HardClear:
                        case RunEndCause.COClear:
                            stringBuilder.Append(" class=\"clear\">");
                            break;
                        default:
                            stringBuilder.Append(">");
                            break;
                    }
                    stringBuilder.Append(entry.GetLevel());
                    //Score
                    stringBuilder.Append("</td><td class=\"score\">");
                    stringBuilder.Append(entry.GetScore());
                    break;
                case PageType.Time:
                    //Time
                    stringBuilder.Append("</td><td>");
                    stringBuilder.Append(entry.GetTime());
                    break;
            }

            //Finish row
            stringBuilder.AppendLine("</td></tr>");
        }

        public void WriteTimeRanking(StringBuilder stringBuilder, List<PlayerEntry> timeEntries, string description, PageType sortedBy)
        {
            if (timeEntries.Count == 0)
            {
                stringBuilder.Append("<font size=\"6\">No ");
                stringBuilder.Append(description);

                if (ongoing) stringBuilder.Append(" clears have happened yet.");
                else stringBuilder.Append(" clears happened this day.");
                stringBuilder.AppendLine("</font>");
            }
            else
            {
                stringBuilder.Append("<font size=\"6\">");
                stringBuilder.Append(description);
                stringBuilder.AppendLine("</font>");
                stringBuilder.AppendLine("<br /><br />");
                stringBuilder.AppendLine("<table border=\"1\">");
                for (int i = 0; i < timeEntries.Count; i++)
                {
                    WriteEntry(timeEntries[i], i + 1, sortedBy);
                }
                stringBuilder.AppendLine("</table>");
            }
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
