using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

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

        public PageWriter()
        {
            //Empty constructor
        }

        public PageWriter(PlayerEntry[] entries, int year, int month, int day, bool ongoing)
        {
            this.entries = entries;
            this.dateTime = new DateTime(year, month, day, 12, 00, 00);
            this.ongoing = ongoing;
        }

        public string GeneratePage(string description, PageType pageType, string pageFile, string path)
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
            stringBuilder.AppendLine("<br /><br /><div class=\"hidelink\">");
            switch (pageType)
            {
                case PageType.Depth:
                case PageType.Score:
                    stringBuilder.AppendLine("<table border=\"1\">");
                    for (int i = 0; i < entries.Length; i++)
                    {
                        WriteEntry(entries[i], i + 1, pageType, path);
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
                            case RunEndCause.SpecialClear: coEntries.Add(entries[i]); break;
                            case RunEndCause.HardClear: hardEntries.Add(entries[i]); break;
                            case RunEndCause.NormalClear: normalEntries.Add(entries[i]); break;
                        }
                    }
                    WriteTimeRanking(stringBuilder, coEntries, "Special", pageType, path);
                    stringBuilder.AppendLine("<br /><br />");
                    WriteTimeRanking(stringBuilder, hardEntries, "Hard", pageType, path);
                    stringBuilder.AppendLine("<br /><br />");
                    WriteTimeRanking(stringBuilder, normalEntries, "Normal", pageType, path);
                    stringBuilder.AppendLine("<br /><br />");

                    break;
            }

            stringBuilder.AppendLine("</div>"); //end hidelink
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
                    bool cleared = (entry.runend == RunEndCause.NormalClear || entry.runend == RunEndCause.HardClear || entry.runend == RunEndCause.SpecialClear);
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
                    if (deathRate >= 0.5f) stringBuilder.Append(" class=\"deadly\"");
                    else if (deathRate >= 0.3f) stringBuilder.Append(" class=\"danger\"");
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
                        case RunEndCause.SpecialClear:
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

        public string GeneratePlayerPage(PlayerInfo pi)
        {
            //Program.Log("Generating player page for " + pi.name + " (" + pi.id + ")"); //Enable this for a lot of spam
            stringBuilder = new StringBuilder();

            //Sort entries chronologically (oldest scores first)
            pi.entries.Sort((x, y) => (x.timestamp.CompareTo(y.timestamp)));

            //Get info
            ExtendedPlayerEntry latestEntry = pi.entries[pi.entries.Count - 1];
            long totalFrames = 0;
            int normalClears = 0;
            int hardClears = 0;
            int specialClears = 0;
            int scVisits = 0;
            int coVisits = 0;
            bool doubleSpecial = false;
            List<Character> playedChars = new List<Character>();
            for (int i = 0; i < pi.entries.Count; i++)
            {
                ExtendedPlayerEntry e = pi.entries[i];
                totalFrames += e.runframes;
                switch(e.runend)
                {
                    case RunEndCause.NormalClear: normalClears++; break;
                    case RunEndCause.HardClear: hardClears++; break;
                    case RunEndCause.SpecialClear: specialClears++; break;
                }
                if (e.level >= 19) scVisits++;
                if (e.level >= 23) coVisits++;

                //Accomplishment tracking
                if (i > 0 && e.runend == RunEndCause.SpecialClear)
                {
                    if (pi.entries[i - 1].runend == RunEndCause.SpecialClear) doubleSpecial = true;
                }

                bool hasPlayedCharacter = false;
                for (int j = 0; j < playedChars.Count; j++) if (playedChars[j] == pi.entries[i].character) hasPlayedCharacter = true;
                if (!hasPlayedCharacter) playedChars.Add(pi.entries[i].character);
            }

            //Check for accomplishments
            List<Accomplishment> accomplishments = new List<Accomplishment>();
            if (specialClears >= 10) accomplishments.Add(new Accomplishment("Legend", "This player has reached 7-99 ten times!", "legend.png"));
            if (pi.bestLevelRank == 1) accomplishments.Add(new Accomplishment("#1 Depth", "This player has achieved rank #1 in Depth on a daily!", "1depth.png"));
            if (pi.bestScoreRank == 1) accomplishments.Add(new Accomplishment("#1 Score", "This player has achieved rank #1 in Score on a daily!", "1score.png"));
            if (pi.bestHardTimeRank == 1) accomplishments.Add(new Accomplishment("#1 Hard", "This player got first place in a Hard speedrun on a daily!", "1hard.png"));
            if (pi.bestNormalTimeRank == 1) accomplishments.Add(new Accomplishment("#1 Normal", "This player got first place in a Normal speedrun on a daily!", "1normal.png"));
            if (pi.bestSpecialTimeRank == 1) accomplishments.Add(new Accomplishment("#1 Special", "This player was the fastest to get a Special clear on a daily!", "1special.png"));
            if (doubleSpecial) accomplishments.Add(new Accomplishment("Consistent", "This player has reached 7-99 twice in a row!", "consistent.png"));
            if (pi.bestSpecialTime != -1 && pi.bestSpecialTime < 116 * 60 * 60) accomplishments.Add(new Accomplishment("Celeritas", "This player reached 7-99 within 1 hour and 56 minutes!", "celeritas.png"));
            if (specialClears > 0) accomplishments.Add(new Accomplishment("One with the Cosmos", "This player has reached 7-99!", "799.png"));
            if (pi.bestScore > 2000000) accomplishments.Add(new Accomplishment("Treasure Hunter", "This player has achieved a top score of over $2,000,000!", "2million.png"));
            if (pi.bestHardTime != -1 && pi.bestHardTime < 20 * 60 * 60) accomplishments.Add(new Accomplishment("brb killing primordial chaos", "This player has completed a Hard run in under 20 minutes!", "fasthard.png"));
            if ((pi.bestNormalTime != -1 && pi.bestNormalTime < 8 * 60 * 60) || (pi.bestHardTime != -1 && pi.bestHardTime < 8 * 60 * 60))
                accomplishments.Add(new Accomplishment("Really Fast", "This player has completed a run in under 8 minutes!", "fastnormal.png"));
            if (pi.topScoresAverage > 1000000) accomplishments.Add(new Accomplishment("More like 'Palace of Treasure' am I right", "This player has a top 10 average score of over $1,000,000!", "million.png"));
            if (coVisits >= 10) accomplishments.Add(new Accomplishment("Astronaut", "This player has reached the Cosmic Ocean ten times!", "astronaut.png"));
            if (pi.entries.Count >= 365) accomplishments.Add(new Accomplishment("Yearly Challenge", "This player has participated 365 times!", "year.png"));
            if (playedChars.Count == 20) accomplishments.Add(new Accomplishment("I Main Random", "This player has played with every character!", "allchars.png"));

            //Write header
            WriteHeader(PageType.Player, pi.name);

            //Return link
            stringBuilder.AppendLine("<center><a href=\"depth.html\">[Click here to return to leaderboard listings]</a><br /><br />");

            //Name(s)
            stringBuilder.Append("<font size=\"7\">");
            stringBuilder.Append(pi.name);
            stringBuilder.Append("</font>");
            BR();
            //Filter duplicate alternate names
            for (int i = 0; i < pi.previousNames.Count; i++)
            {
                if (pi.previousNames[i] == pi.name)
                {
                    pi.previousNames.RemoveAt(i);
                    i--;
                    continue;
                }
                for (int j = 0; j < pi.previousNames.Count; j++)
                {
                    if (i == j) continue;
                    if (pi.previousNames[i] == pi.previousNames[j])
                    {
                        pi.previousNames.RemoveAt(j);
                        j--;
                    }
                }
            }
            //List alternate names
            for (int i = 0; i < pi.previousNames.Count; i++)
            {
                stringBuilder.Append("aka ");
                stringBuilder.Append(pi.previousNames[i]);
                BR();
            }

            //Last used character
            stringBuilder.Append("<table><tr><td class=\"c");
            stringBuilder.Append(Convert.ToString((int)latestEntry.character, 16).PadLeft(2, '0'));
            stringBuilder.Append("\"/></tr></table>");
            BR();

            //Info
            PlayerAddition addition = null;
            if (Program.additions.ContainsKey(pi.id)) addition = Program.additions[pi.id];
            if (addition != null) { stringBuilder.Append(addition.html); BR(); }
            if (latestEntry.platform == Platform.Steam)
            {
                stringBuilder.Append("<a href=\"https://steamcommunity.com/profiles/");
                stringBuilder.Append(pi.id);
                stringBuilder.AppendLine("\">Visit Steam profile</a><br />");
            }
            BR();

            //Accomplishments
            if (accomplishments.Count > 0)
            {
                stringBuilder.AppendLine("<table>");
                for (int i = 0; i < accomplishments.Count; i++)
                {
                    WriteKeyValue("<img src=\"" + accomplishments[i].icon + "\">", "<b>" + accomplishments[i].name + "</b>: " + accomplishments[i].description);
                }
                stringBuilder.AppendLine("</table><br />");
            }

            //From here on, do not show links until hovered
            stringBuilder.AppendLine("<div class=\"hidelink\">");

            //General stats
            stringBuilder.AppendLine("<font size=\"5\">General statistics</font><br />");
            stringBuilder.AppendLine("<table>");
            WriteKeyValue("Runs", pi.entries.Count);
            WriteKeyValue("Total playtime", PlayerEntry.GetTime(totalFrames));
            WriteKeyValue("Normal clears", normalClears);
            WriteKeyValue("Sunken City visits", scVisits);
            WriteKeyValue("Hard clears", hardClears);
            WriteKeyValue("CO visits", coVisits);
            WriteKeyValue("Special clears", specialClears);
            stringBuilder.AppendLine("</table><br /><br />");

            //Top average scores
            stringBuilder.AppendLine("<font size=\"5\">Top averages</font><br />");
            stringBuilder.AppendLine("<table>");
            int wholePart = (int)Math.Floor(pi.topLevelsAverage);
            float fraction = pi.topLevelsAverage - wholePart;
            stringBuilder.Append("<tr><td><b>Top 10 average depth</b></td><td>");
            stringBuilder.Append(PlayerEntry.GetLevel(wholePart));
            stringBuilder.Append("<font size=\"1\">.");
            string fractionalString = Convert.ToString(Math.Round(fraction, 3));
            if (fractionalString.Length < 2) fractionalString = "000";
            else
            {
                fractionalString = fractionalString.Substring(2).PadRight(3, '0');
            }
            stringBuilder.Append(fractionalString);
            stringBuilder.Append("</font>");
            stringBuilder.AppendLine("</td></tr>");
            WriteKeyValue("Top 10 average score", PlayerEntry.GetScore(pi.topScoresAverage));
            WriteKeyValue("Top 5 average time", PlayerEntry.GetTime(pi.topTimesAverage));
            stringBuilder.AppendLine("</table><br /><br />");

            //Best rankings
            stringBuilder.AppendLine("<font size=\"5\">Best rankings</font><br />");
            stringBuilder.AppendLine("<table>");
            WriteTimestampedValue("Best depth ranking", pi.bestLevelRank, pi.bestLevelRankDates);
            WriteTimestampedValue("Best score ranking", pi.bestScoreRank, pi.bestScoreRankDates);
            if (pi.bestNormalTimeRankDates.Count == 0) WriteKeyValue("Best Normal ranking", "<i>N/A</i>");
            else WriteTimestampedValue("Best Normal ranking", pi.bestNormalTimeRank, pi.bestNormalTimeRankDates);
            if (pi.bestHardTimeRankDates.Count == 0) WriteKeyValue("Best Hard ranking", "<i>N/A</i>");
            else WriteTimestampedValue("Best Hard ranking", pi.bestHardTimeRank, pi.bestHardTimeRankDates);
            if (pi.bestSpecialTimeRankDates.Count == 0) WriteKeyValue("Best Special ranking", "<i>N/A</i>");
            else WriteTimestampedValue("Best Special ranking", pi.bestSpecialTimeRank, pi.bestSpecialTimeRankDates);
            stringBuilder.AppendLine("</table><br /><br />");

            //Top results
            stringBuilder.AppendLine("<font size=\"5\">Top results</font><br />");
            stringBuilder.AppendLine("<table>");
            WriteTimestampedValue("Best depth", PlayerEntry.GetLevel(pi.bestLevel), pi.bestLevelDates);
            WriteTimestampedValue("Best score", PlayerEntry.GetScore(pi.bestScore), pi.bestScoreDate);
            if (pi.bestNormalTime == -1) WriteKeyValue("Fastest Normal clear", "<i>N/A</i>");
            else WriteTimestampedValue("Fastest Normal clear", PlayerEntry.GetTime(pi.bestNormalTime), pi.bestNormalTimeDate);
            if (pi.bestHardTime == -1) WriteKeyValue("Fastest Hard clear", "<i>N/A</i>");
            else WriteTimestampedValue("Fastest Hard clear", PlayerEntry.GetTime(pi.bestHardTime), pi.bestHardTimeDate);
            if (pi.bestSpecialTime == -1) WriteKeyValue("Fastest Special clear", "<i>N/A</i>");
            else WriteTimestampedValue("Fastest Special clear", PlayerEntry.GetTime(pi.bestSpecialTime), pi.bestSpecialTimeDate);
            stringBuilder.AppendLine("</table><br /><br />");

            //All results
            pi.entries.Sort((x, y) => (y.timestamp.CompareTo(x.timestamp))); //Sort in reverse chronological order for display
            stringBuilder.AppendLine("<font size=\"5\">All results</font><br />");
            stringBuilder.AppendLine("<table style=\"min-width: 0.5em;\">");
            
            for (int i = 0; i < pi.entries.Count; i++)
            {
                ExtendedPlayerEntry epe = pi.entries[i];
                bool clear = (epe.runend == RunEndCause.NormalClear || epe.runend == RunEndCause.HardClear || epe.runend == RunEndCause.SpecialClear);
                stringBuilder.Append("<tr><td>");
                string tsString = Program.IntTimestampToString(epe.timestamp);
                stringBuilder.Append("&nbsp;<a href=\"");
                stringBuilder.Append(tsString);
                stringBuilder.Append("_depth.html\">");
                stringBuilder.Append(tsString);
                stringBuilder.Append("</a>&nbsp;");
                stringBuilder.Append("</td><td /><td class=\"c");
                stringBuilder.Append(Convert.ToString((int)epe.character, 16).PadLeft(2, '0'));
                stringBuilder.Append("\" /><td /><td>#");
                stringBuilder.Append(epe.levelRank);
                stringBuilder.Append("</td><td");
                if (clear) stringBuilder.Append(" class=\"clear\">");
                else stringBuilder.Append(">");
                stringBuilder.Append(epe.GetLevel());
                stringBuilder.Append("</td><td /><td>#");
                stringBuilder.Append(epe.scoreRank);
                stringBuilder.Append("</td><td>$");
                stringBuilder.Append(epe.GetScore());
                if (clear)
                {
                    stringBuilder.Append("</td><td /><td>#");
                    switch(epe.runend)
                    {
                        case RunEndCause.NormalClear: stringBuilder.Append(epe.normalTimeRank); break;
                        case RunEndCause.HardClear: stringBuilder.Append(epe.hardTimeRank); break;
                        case RunEndCause.SpecialClear: stringBuilder.Append(epe.specialTimeRank); break;
                    }
                    stringBuilder.Append("</td><td>");
                    stringBuilder.Append(epe.GetTime());
                }
                stringBuilder.AppendLine("</td></tr>");
            }
            stringBuilder.AppendLine("</table><br />");

            //Finish
            stringBuilder.AppendLine("</div>"); //hidelink end
            WriteFooter(PageType.Player, false);

            //Write to file
            return stringBuilder.ToString();
        }

        public void BR()
        {
            stringBuilder.AppendLine("<br />");
        }

        public void WriteHeader(PageType pageType, string customTitle = null)
        {
            stringBuilder.AppendLine("<html>");
            //--HEAD--
            stringBuilder.AppendLine("<head>");
            stringBuilder.AppendLine("<meta charset=\"utf-8\" />");
            stringBuilder.Append("<title>");
            if (customTitle != null) stringBuilder.Append(customTitle);
            else
            {
                stringBuilder.Append(pageType);
                stringBuilder.Append(" ");
                stringBuilder.Append(dateTime.Year);
                stringBuilder.Append("-");
                stringBuilder.Append(Convert.ToString(dateTime.Month).PadLeft(2, '0'));
                stringBuilder.Append("-");
                stringBuilder.Append(Convert.ToString(dateTime.Day).PadLeft(2, '0'));
            }
            stringBuilder.Append(" - Spelunky 2 leaderboards (unofficial)");
            stringBuilder.AppendLine("</title>");
            stringBuilder.AppendLine("<link rel=\"stylesheet\" href=\"style.css\" /></head>");
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
                stringBuilder.Append(Program.GetYYYYMMDD(tomorrow));
                stringBuilder.Append("_");
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

        public void WriteEntry(PlayerEntry entry, int rank, PageType sortedBy, string path)
        {
            //Rank
            stringBuilder.Append("<tr><td>");
            stringBuilder.Append(Convert.ToString(rank));
            //Platform
            stringBuilder.Append("</td><td class=\"");
            bool knownPlatform = true;
            switch (entry.platform)
            {
                case Platform.Steam: stringBuilder.Append("pc"); break;
                case Platform.PS4: stringBuilder.Append("ps4"); break;
                default: stringBuilder.Append("unknown"); knownPlatform = false; break;
            }
            if (knownPlatform) stringBuilder.Append("\" />");
            else
            {
#if DEBUG
                stringBuilder.Append("\" >");
                stringBuilder.Append(Convert.ToString((int)entry.platform, 16));
                stringBuilder.Append("</td>");
#else
                stringBuilder.Append("\" />"); //Don't expose weirdness to end-users
#endif
            }
#if DEBUG
            if (true)
            {
                //DEBUG
                stringBuilder.Append("<td>");
                stringBuilder.Append(entry.unknown1);
                stringBuilder.Append("</td><td>");
                stringBuilder.Append(Convert.ToString(entry.unknown1, 16));
                stringBuilder.Append("</td><td>");
                stringBuilder.Append(entry.unknown2);
                stringBuilder.Append("</td><td>");
                stringBuilder.Append(Convert.ToString(entry.unknown2, 16));
                stringBuilder.Append("</td><td>");
                stringBuilder.Append("</td>");
            }
#endif
            //Character
            stringBuilder.Append("<td class=\"c");
            stringBuilder.Append(Convert.ToString(Convert.ToInt16(entry.character), 16).PadLeft(2, '0'));
            stringBuilder.Append("\"/>");
            //Name
            string playerFile = Convert.ToString((long)entry.id, 16).PadLeft(16, '0') + ".html";
            if (ongoing && !File.Exists(path + playerFile)) playerFile = null;
            stringBuilder.Append("<td>");
            if (playerFile != null)
            {
                stringBuilder.Append("<a href=\"");
                stringBuilder.Append(playerFile);
                stringBuilder.Append("\">");
            }
            stringBuilder.Append(entry.name.Replace("<", "").Replace(">", ""));
            if (playerFile != null) stringBuilder.Append("</a>");
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
                        case RunEndCause.SpecialClear:
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

        public void WriteTimeRanking(StringBuilder stringBuilder, List<PlayerEntry> timeEntries, string description, PageType sortedBy, string path)
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
                    WriteEntry(timeEntries[i], i + 1, sortedBy, path);
                }
                stringBuilder.AppendLine("</table>");
            }
        }

        public void WriteKeyValue(string key, int value)
        {
            WriteKeyValue(key, Convert.ToString(value));
        }

        public void WriteKeyValue(string key, string value)
        {
            stringBuilder.Append("<tr><td><b>");
            stringBuilder.Append(key);
            stringBuilder.Append("</b></td><td>");
            stringBuilder.Append(value);
            stringBuilder.AppendLine("</td></tr>");
        }

        public void WriteTimestampedValue(string key, int value, int timestamp)
        {
            WriteTimestampedValue(key, Convert.ToString(value), timestamp);
        }

        public void WriteTimestampedValue(string key, string value, int timestamp)
        {
            List<int> timestamps = new List<int>();
            timestamps.Add(timestamp);
            WriteTimestampedValue(key, value, timestamps);
        }

        public void WriteTimestampedValue(string key, int value, List<int> timestamps)
        {
            WriteTimestampedValue(key, Convert.ToString(value), timestamps);
        }

        public void WriteTimestampedValue(string key, string value, List<int> timestamps)
        {
            stringBuilder.Append("<tr><td><b>");
            stringBuilder.Append(key);
            stringBuilder.Append("</b></td><td>");
            stringBuilder.Append(value);
            stringBuilder.Append("</td><td>");
            for (int i = 0; i < timestamps.Count; i++)
            {
                if (i > 0) stringBuilder.Append("<br />");
                string tsString = Program.IntTimestampToString(timestamps[i]);
                stringBuilder.Append("<a href=\"");
                stringBuilder.Append(tsString);
                stringBuilder.Append("_depth.html\">");
                stringBuilder.Append(tsString);
                stringBuilder.Append("</a>");
            }
            stringBuilder.AppendLine("</td></tr>");
        }

        const string countdownJS = @"
<script>
var tomorrow = new Date(""{TOMORROW}"").getTime();
setInterval(function()
{
	var left = tomorrow - new Date().getTime();
	if (left > 0) document.getElementById(""count"").innerHTML = ""Challenge ends in "" + getLeft(left);
	else if (left > -1800000)
	{
		left += 1800000;
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
