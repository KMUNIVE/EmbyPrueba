﻿using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Implementations.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using System;
using System.IO;

namespace MediaBrowser.Server.Implementations.Configuration
{
    /// <summary>
    /// Class ServerConfigurationManager
    /// </summary>
    public class ServerConfigurationManager : BaseConfigurationManager, IServerConfigurationManager
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ServerConfigurationManager" /> class.
        /// </summary>
        /// <param name="applicationPaths">The application paths.</param>
        /// <param name="logManager">The log manager.</param>
        /// <param name="xmlSerializer">The XML serializer.</param>
        public ServerConfigurationManager(IApplicationPaths applicationPaths, ILogManager logManager, IXmlSerializer xmlSerializer)
            : base(applicationPaths, logManager, xmlSerializer)
        {
            UpdateItemsByNamePath();
        }

        /// <summary>
        /// Gets the type of the configuration.
        /// </summary>
        /// <value>The type of the configuration.</value>
        protected override Type ConfigurationType
        {
            get { return typeof(ServerConfiguration); }
        }

        /// <summary>
        /// Gets the application paths.
        /// </summary>
        /// <value>The application paths.</value>
        public IServerApplicationPaths ApplicationPaths
        {
            get { return (IServerApplicationPaths)CommonApplicationPaths; }
        }

        /// <summary>
        /// Gets the configuration.
        /// </summary>
        /// <value>The configuration.</value>
        public ServerConfiguration Configuration
        {
            get { return (ServerConfiguration)CommonConfiguration; }
        }

        /// <summary>
        /// Called when [configuration updated].
        /// </summary>
        protected override void OnConfigurationUpdated()
        {
            UpdateItemsByNamePath();

            base.OnConfigurationUpdated();
        }

        /// <summary>
        /// Updates the items by name path.
        /// </summary>
        private void UpdateItemsByNamePath()
        {
            if (!string.IsNullOrEmpty(Configuration.ItemsByNamePath))
            {
                ApplicationPaths.ItemsByNamePath = Configuration.ItemsByNamePath;
            }
        }

        /// <summary>
        /// Replaces the configuration.
        /// </summary>
        /// <param name="newConfiguration">The new configuration.</param>
        /// <exception cref="System.IO.DirectoryNotFoundException"></exception>
        public override void ReplaceConfiguration(BaseApplicationConfiguration newConfiguration)
        {
            var newConfig = (ServerConfiguration) newConfiguration;

            var newIbnPath = newConfig.ItemsByNamePath;

            if (!string.IsNullOrWhiteSpace(newIbnPath)
                && !string.Equals(Configuration.ItemsByNamePath ?? string.Empty, newIbnPath))
            {
                // Validate
                if (!Directory.Exists(newIbnPath))
                {
                    throw new DirectoryNotFoundException(string.Format("{0} does not exist.", newConfig.ItemsByNamePath));
                }
            }

            base.ReplaceConfiguration(newConfiguration);
        }
    }
}
