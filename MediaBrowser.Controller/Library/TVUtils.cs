﻿using System.Globalization;
using MediaBrowser.Controller.Resolvers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace MediaBrowser.Controller.Library
{
    /// <summary>
    /// Class TVUtils
    /// </summary>
    public static class TVUtils
    {
        /// <summary>
        /// The TVDB API key
        /// </summary>
        public static readonly string TvdbApiKey = "B89CE93890E9419B";
        /// <summary>
        /// The banner URL
        /// </summary>
        public static readonly string BannerUrl = "http://www.thetvdb.com/banners/";

        /// <summary>
        /// A season folder must contain one of these somewhere in the name
        /// </summary>
        private static readonly string[] SeasonFolderNames = new[]
                                                                 {
                                                                     "season",
                                                                     "sæson",
                                                                     "temporada",
                                                                     "saison",
                                                                     "staffel"
                                                                 };

        /// <summary>
        /// Used to detect paths that represent episodes, need to make sure they don't also
        /// match movie titles like "2001 A Space..."
        /// Currently we limit the numbers here to 2 digits to try and avoid this
        /// </summary>
        private static readonly Regex[] EpisodeExpressions = new[]
                                                                 {
                                                                     new Regex(
                                                                         @".*\\[s|S]?(?<seasonnumber>\d{1,2})[x|X](?<epnumber>\d{1,3})[^\\]*$",
                                                                         RegexOptions.Compiled),
                                                                     // 01x02 blah.avi S01x01 balh.avi
                                                                     new Regex(
                                                                         @".*\\[s|S](?<seasonnumber>\d{1,2})[x,X]?[e|E](?<epnumber>\d{1,3})[^\\]*$",
                                                                         RegexOptions.Compiled),
                                                                     // S01E02 blah.avi, S01xE01 blah.avi
                                                                     new Regex(
                                                                         @".*\\(?<seriesname>[^\\]*)[s|S]?(?<seasonnumber>\d{1,2})[x|X](?<epnumber>\d{1,3})[^\\]*$",
                                                                         RegexOptions.Compiled),
                                                                     // 01x02 blah.avi S01x01 balh.avi
                                                                     new Regex(
                                                                         @".*\\(?<seriesname>[^\\]*)[s|S](?<seasonnumber>\d{1,2})[x|X|\.]?[e|E](?<epnumber>\d{1,3})[^\\]*$",
                                                                         RegexOptions.Compiled)
                                                                     // S01E02 blah.avi, S01xE01 blah.avi
                                                                 };

        private static readonly Regex[] DoubleEpisodeExpressions = new[]
                                                                 {
                                                                     new Regex(
                                                                         @".*\\[s|S]?(?<seasonnumber>\d{1,2})[x|X]\d{1,3}[x|X](?<epnumber>\d{1,3})[^\\]*$",
                                                                         RegexOptions.Compiled),
                                                                     // 01x02x03 blah.avi S01x01x03 balh.avi
                                                                     new Regex(
                                                                         @".*\\[s|S](?<seasonnumber>\d{1,2})[x,X]?[e|E]\d{1,3}[x,X]?[e|E](?<epnumber>\d{1,3})[^\\]*$",
                                                                         RegexOptions.Compiled),
                                                                     // S01E02E03 blah.avi, S01xE01xE02 blah.avi
                                                                     new Regex(
                                                                         @".*\\(?<seriesname>[^\\]*)[s|S]?(?<seasonnumber>\d{1,2})[x|X]\d{1,3}[x|X](?<epnumber>\d{1,3})[^\\]*$",
                                                                         RegexOptions.Compiled),
                                                                     // 01x02x03 blah.avi S01x01X03 balh.avi
                                                                     new Regex(
                                                                         @".*\\(?<seriesname>[^\\]*)[s|S]?(?<seasonnumber>\d{1,2})[x|X]\d{1,3}.{1,3}[s|S]?\d{1,2}[x|X](?<epnumber>\d{1,3})[^\\]*$",
                                                                         RegexOptions.Compiled),
                                                                     // 01x02 01x03 blah.avi 01x02 - 01x03 blah.avi S01x02 S01x03 blah.avi S01x02 - S01x03 blah.avi
                                                                     new Regex(
                                                                         @".*\\(?<seriesname>[^\\]*)[s|S](?<seasonnumber>\d{1,2})[x|X|\.]?[e|E]\d{1,3}[x|X|\.]?[e|E](?<epnumber>\d{1,3})[^\\]*$",
                                                                         RegexOptions.Compiled)
                                                                     // S01E02-E03 blah.avi, S01xE01xE02 blah.avi
                                                                 };

        /// <summary>
        /// To avoid the following matching movies they are only valid when contained in a folder which has been matched as a being season
        /// </summary>
        private static readonly Regex[] EpisodeExpressionsInASeasonFolder = new[]
                                                                                {
                                                                                    new Regex(
                                                                                        @".*\\(?<epnumber>\d{1,2})\s?-\s?[^\\]*$",
                                                                                        RegexOptions.Compiled),
                                                                                    // 01 - blah.avi, 01-blah.avi
                                                                                    new Regex(
                                                                                        @".*\\(?<epnumber>\d{1,2})[^\d\\]*[^\\]*$",
                                                                                        RegexOptions.Compiled),
                                                                                    // 01.avi, 01.blah.avi "01 - 22 blah.avi" 
                                                                                    new Regex(
                                                                                        @".*\\(?<seasonnumber>\d)(?<epnumber>\d{1,2})[^\d\\]+[^\\]*$",
                                                                                        RegexOptions.Compiled),
                                                                                    // 01.avi, 01.blah.avi
                                                                                    new Regex(
                                                                                        @".*\\\D*\d+(?<epnumber>\d{2})",
                                                                                        RegexOptions.Compiled)
                                                                                    // hell0 - 101 -  hello.avi

                                                                                };

        /// <summary>
        /// Gets the season number from path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>System.Nullable{System.Int32}.</returns>
        public static int? GetSeasonNumberFromPath(string path)
        {
            // Look for one of the season folder names
            foreach (var name in SeasonFolderNames)
            {
                var index = path.IndexOf(name, StringComparison.OrdinalIgnoreCase);

                if (index != -1)
                {
                    return GetSeasonNumberFromPathSubstring(path.Substring(index + name.Length));
                }
            }

            return null;
        }

        /// <summary>
        /// Extracts the season number from the second half of the Season folder name (everything after "Season", or "Staffel")
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>System.Nullable{System.Int32}.</returns>
        private static int? GetSeasonNumberFromPathSubstring(string path)
        {
            int numericStart = -1;
            int length = 0;

            // Find out where the numbers start, and then keep going until they end
            for (var i = 0; i < path.Length; i++)
            {
                if (char.IsNumber(path, i))
                {
                    if (numericStart == -1)
                    {
                        numericStart = i;
                    }
                    length++;
                }
                else if (numericStart != -1)
                {
                    break;
                }
            }

            if (numericStart == -1)
            {
                return null;
            }

            return int.Parse(path.Substring(numericStart, length));
        }

        /// <summary>
        /// Determines whether [is season folder] [the specified path].
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns><c>true</c> if [is season folder] [the specified path]; otherwise, <c>false</c>.</returns>
        private static bool IsSeasonFolder(string path)
        {
            return GetSeasonNumberFromPath(path) != null && !new DirectoryInfo(path).EnumerateFiles().Any(i => EntityResolutionHelper.IsAudioFile(i.FullName));
        }

        /// <summary>
        /// Determines whether [is series folder] [the specified path].
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="fileSystemChildren">The file system children.</param>
        /// <returns><c>true</c> if [is series folder] [the specified path]; otherwise, <c>false</c>.</returns>
        public static bool IsSeriesFolder(string path, IEnumerable<FileSystemInfo> fileSystemChildren)
        {
            // A folder with more than 3 non-season folders in will not becounted as a series
            var nonSeriesFolders = 0;

            foreach (var child in fileSystemChildren)
            {
                var attributes = child.Attributes;

                if (attributes.HasFlag(FileAttributes.Hidden) || attributes.HasFlag(FileAttributes.System))
                {
                    continue;
                }

                if (attributes.HasFlag(FileAttributes.Directory))
                {
                    if (IsSeasonFolder(child.FullName))
                    {
                        return true;
                    }

                    nonSeriesFolders++;

                    if (nonSeriesFolders >= 3)
                    {
                        return false;
                    }
                }
                else
                {
                    if (EntityResolutionHelper.IsVideoFile(child.FullName) && GetEpisodeNumberFromFile(child.FullName, false).HasValue)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Episodes the number from file.
        /// </summary>
        /// <param name="fullPath">The full path.</param>
        /// <param name="isInSeason">if set to <c>true</c> [is in season].</param>
        /// <returns>System.String.</returns>
        public static int? GetEpisodeNumberFromFile(string fullPath, bool isInSeason)
        {
            string fl = fullPath.ToLower();
            foreach (var r in EpisodeExpressions)
            {
                Match m = r.Match(fl);
                if (m.Success)
                    return ParseEpisodeNumber(m.Groups["epnumber"].Value);
            }
            if (isInSeason)
            {
                var match = EpisodeExpressionsInASeasonFolder.Select(r => r.Match(fl))
                    .FirstOrDefault(m => m.Success);

                if (match != null)
                {
                    return ParseEpisodeNumber(match.Value);
                }
            }

            return null;
        }

        public static int? GetDoubleEpisodeNumberFromFile(string fullPath)
        {
            var fl = fullPath.ToLower();
            foreach (var r in DoubleEpisodeExpressions)
            {
                var m = r.Match(fl);
                if (m.Success)
                    return ParseEpisodeNumber(m.Groups["epnumber"].Value);
            }
            return null;
        }

        private static readonly CultureInfo UsCulture = new CultureInfo("en-US");
        
        private static int? ParseEpisodeNumber(string val)
        {
            int num;

            if (!string.IsNullOrEmpty(val) && int.TryParse(val, NumberStyles.Integer, UsCulture, out num))
            {
                return num;
            }

            return null;
        }

        /// <summary>
        /// Seasons the number from episode file.
        /// </summary>
        /// <param name="fullPath">The full path.</param>
        /// <returns>System.String.</returns>
        public static string SeasonNumberFromEpisodeFile(string fullPath) {
            return SeasonNumberFromFile(fullPath, EpisodeExpressions);
        }

        /// <summary>
        /// Seasons the number from episode file.
        /// </summary>
        /// <param name="fullPath">The full path.</param>
        /// <returns>System.String.</returns>
        public static string SeasonNumberFromDoubleEpisodeFile(string fullPath)
        {
            return SeasonNumberFromFile(fullPath, DoubleEpisodeExpressions);
        }

        static string SeasonNumberFromFile(string fullPath, IEnumerable<Regex> expressions) {
            var fl = fullPath.ToLower();
            foreach (var r in expressions)
            {
                var m = r.Match(fl);
                if (m.Success)
                {
                    var g = m.Groups["seasonnumber"];
                    return g != null ? g.Value : null;
                }
            }
            return null;
        }

        /// <summary>
        /// Gets the air days.
        /// </summary>
        /// <param name="day">The day.</param>
        /// <returns>List{DayOfWeek}.</returns>
        public static List<DayOfWeek> GetAirDays(string day)
        {
            if (!string.IsNullOrWhiteSpace(day))
            {
                if (day.Equals("Daily", StringComparison.OrdinalIgnoreCase))
                {
                    return new List<DayOfWeek>
                               {
                                   DayOfWeek.Sunday,
                                   DayOfWeek.Monday,
                                   DayOfWeek.Tuesday,
                                   DayOfWeek.Wednesday,
                                   DayOfWeek.Thursday,
                                   DayOfWeek.Friday,
                                   DayOfWeek.Saturday
                               };
                }

                DayOfWeek value;

                if (Enum.TryParse(day, true, out value))
                {
                    return new List<DayOfWeek>
                               {
                                   value
                               };
                }

                return new List<DayOfWeek>();
            }
            return null;
        }
    }
}
