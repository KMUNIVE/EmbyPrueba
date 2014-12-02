﻿using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using System.Linq;

namespace MediaBrowser.Server.Implementations.Library.Resolvers.TV
{
    /// <summary>
    /// Class EpisodeResolver
    /// </summary>
    public class EpisodeResolver : BaseVideoResolver<Episode>
    {
        public EpisodeResolver(ILibraryManager libraryManager) : base(libraryManager)
        {
        }

        /// <summary>
        /// Resolves the specified args.
        /// </summary>
        /// <param name="args">The args.</param>
        /// <returns>Episode.</returns>
        protected override Episode Resolve(ItemResolveArgs args)
        {
            var parent = args.Parent;

            if (parent == null)
            {
                return null;
            }

            var season = parent as Season;

            // Just in case the user decided to nest episodes. 
            // Not officially supported but in some cases we can handle it.
            if (season == null)
            {
                season = parent.Parents.OfType<Season>().FirstOrDefault();
            }

            // If the parent is a Season or Series, then this is an Episode if the VideoResolver returns something
            if (season != null || parent is Series || parent.Parents.OfType<Series>().Any())
            {
                Episode episode = null;

                if (args.IsDirectory)
                {
                    if (args.ContainsFileSystemEntryByName("video_ts"))
                    {
                        episode = new Episode
                        {
                            Path = args.Path,
                            VideoType = VideoType.Dvd
                        };
                    }
                    if (args.ContainsFileSystemEntryByName("bdmv"))
                    {
                        episode = new Episode
                        {
                            Path = args.Path,
                            VideoType = VideoType.BluRay
                        };
                    }
                }

                if (episode == null)
                {
                    episode = base.Resolve(args);
                }

                if (episode != null)
                {
                    // The base video resolver is going to fill these in, so null them out
                    episode.ProductionYear = null;
                    episode.Name = null;

                    if (season != null)
                    {
                        episode.ParentIndexNumber = season.IndexNumber;
                    }

                    if (episode.ParentIndexNumber == null)
                    {
                        episode.ParentIndexNumber = SeriesResolver.GetSeasonNumberFromEpisodeFile(args.Path);
                    }
                }

                return episode;
            }

            return null;
        }
    }
}
