﻿using MediaBrowser.Model.LiveTv;
using MediaBrowser.Model.Querying;
using System.Collections.Generic;

namespace MediaBrowser.Controller.LiveTv
{
    /// <summary>
    /// Manages all live tv services installed on the server
    /// </summary>
    public interface ILiveTvManager
    {
        /// <summary>
        /// Gets the services.
        /// </summary>
        /// <value>The services.</value>
        IReadOnlyList<ILiveTvService> Services { get; }

        /// <summary>
        /// Adds the parts.
        /// </summary>
        /// <param name="services">The services.</param>
        void AddParts(IEnumerable<ILiveTvService> services);

        /// <summary>
        /// Gets the channels.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>IEnumerable{Channel}.</returns>
        QueryResult<ChannelInfoDto> GetChannels(ChannelQuery query);

        /// <summary>
        /// Gets the channel information dto.
        /// </summary>
        /// <param name="info">The information.</param>
        /// <returns>ChannelInfoDto.</returns>
        ChannelInfoDto GetChannelInfoDto(Channel info);

        /// <summary>
        /// Gets the channel.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <returns>Channel.</returns>
        Channel GetChannel(string id);

        /// <summary>
        /// Gets the programs.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>IEnumerable{ProgramInfo}.</returns>
        QueryResult<ProgramInfoDto> GetPrograms(ProgramQuery query);
    }
}
