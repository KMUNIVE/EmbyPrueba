﻿using System.Text;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Logging;
using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Server.Implementations.Persistence
{
    public class SqliteProviderInfoRepository : IProviderRepository
    {
        private IDbConnection _connection;

        private readonly ILogger _logger;

        private IDbCommand _saveStatusCommand;
        private readonly IApplicationPaths _appPaths;

        public SqliteProviderInfoRepository(IApplicationPaths appPaths, ILogManager logManager)
        {
            _appPaths = appPaths;
            _logger = logManager.GetLogger(GetType().Name);
        }

        private SqliteShrinkMemoryTimer _shrinkMemoryTimer;

        /// <summary>
        /// Gets the name of the repository
        /// </summary>
        /// <value>The name.</value>
        public string Name
        {
            get
            {
                return "SQLite";
            }
        }

        /// <summary>
        /// Opens the connection to the database
        /// </summary>
        /// <returns>Task.</returns>
        public async Task Initialize()
        {
            var dbFile = Path.Combine(_appPaths.DataPath, "refreshinfo.db");

            _connection = await SqliteExtensions.ConnectToDb(dbFile, _logger).ConfigureAwait(false);

            string[] queries = {

                                "create table if not exists MetadataStatus (ItemId GUID PRIMARY KEY, ItemName TEXT, ItemType TEXT, SeriesName TEXT, DateLastMetadataRefresh datetime, DateLastImagesRefresh datetime, LastStatus TEXT, LastErrorMessage TEXT, MetadataProvidersRefreshed TEXT, ImageProvidersRefreshed TEXT, ItemDateModified DateTimeNull)",
                                "create index if not exists idx_MetadataStatus on MetadataStatus(ItemId)",

                                //pragmas
                                "pragma temp_store = memory",

                                "pragma shrink_memory"
                               };

            _connection.RunQueries(queries, _logger);

            AddItemDateModifiedCommand();

            PrepareStatements();

            _shrinkMemoryTimer = new SqliteShrinkMemoryTimer(_connection, _writeLock, _logger);
        }

        private static readonly string[] StatusColumns =
        {
            "ItemId",
            "ItemName",
            "ItemType",
            "SeriesName",
            "DateLastMetadataRefresh",
            "DateLastImagesRefresh",
            "LastStatus",
            "LastErrorMessage",
            "MetadataProvidersRefreshed",
            "ImageProvidersRefreshed",
            "ItemDateModified"
        };

        private void AddItemDateModifiedCommand()
        {
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA table_info(MetadataStatus)";

                using (var reader = cmd.ExecuteReader(CommandBehavior.SequentialAccess | CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        if (!reader.IsDBNull(1))
                        {
                            var name = reader.GetString(1);

                            if (string.Equals(name, "ItemDateModified", StringComparison.OrdinalIgnoreCase))
                            {
                                return;
                            }
                        }
                    }
                }
            }

            var builder = new StringBuilder();

            builder.AppendLine("alter table MetadataStatus");
            builder.AppendLine("add column ItemDateModified DateTime NULL");

            _connection.RunQueries(new[] { builder.ToString() }, _logger);
        }
        
        /// <summary>
        /// The _write lock
        /// </summary>
        private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Prepares the statements.
        /// </summary>
        private void PrepareStatements()
        {
            _saveStatusCommand = _connection.CreateCommand();

            _saveStatusCommand.CommandText = string.Format("replace into MetadataStatus ({0}) values ({1})",
                string.Join(",", StatusColumns),
                string.Join(",", StatusColumns.Select(i => "@" + i).ToArray()));

            foreach (var col in StatusColumns)
            {
                _saveStatusCommand.Parameters.Add(_saveStatusCommand, "@" + col);
            }
        }

        public MetadataStatus GetMetadataStatus(Guid itemId)
        {
            if (itemId == Guid.Empty)
            {
                throw new ArgumentNullException("itemId");
            }

            using (var cmd = _connection.CreateCommand())
            {
                var cmdText = "select " + string.Join(",", StatusColumns) + " from MetadataStatus where";

                cmdText += " ItemId=@ItemId";
                cmd.Parameters.Add(cmd, "@ItemId", DbType.Guid).Value = itemId;

                cmd.CommandText = cmdText;

                using (var reader = cmd.ExecuteReader(CommandBehavior.SequentialAccess | CommandBehavior.SingleResult | CommandBehavior.SingleRow))
                {
                    while (reader.Read())
                    {
                        return GetStatus(reader);
                    }

                    return null;
                }
            }
        }

        private MetadataStatus GetStatus(IDataReader reader)
        {
            var result = new MetadataStatus
            {
                ItemId = reader.GetGuid(0)
            };

            if (!reader.IsDBNull(1))
            {
                result.ItemName = reader.GetString(1);
            }

            if (!reader.IsDBNull(2))
            {
                result.ItemName = reader.GetString(2);
            }

            if (!reader.IsDBNull(3))
            {
                result.SeriesName = reader.GetString(3);
            }

            if (!reader.IsDBNull(4))
            {
                result.DateLastMetadataRefresh = reader.GetDateTime(4).ToUniversalTime();
            }

            if (!reader.IsDBNull(5))
            {
                result.DateLastImagesRefresh = reader.GetDateTime(5).ToUniversalTime();
            }

            if (!reader.IsDBNull(6))
            {
                result.LastStatus = (ProviderRefreshStatus)Enum.Parse(typeof(ProviderRefreshStatus), reader.GetString(6), true);
            }

            if (!reader.IsDBNull(7))
            {
                result.LastErrorMessage = reader.GetString(7);
            }

            // Skip metadata and image providers

            if (!reader.IsDBNull(10))
            {
                result.ItemDateModified = reader.GetDateTime(10).ToUniversalTime();
            }

            return result;
        }

        public async Task SaveMetadataStatus(MetadataStatus status, CancellationToken cancellationToken)
        {
            if (status == null)
            {
                throw new ArgumentNullException("status");
            }

            cancellationToken.ThrowIfCancellationRequested();

            await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);

            IDbTransaction transaction = null;

            try
            {
                transaction = _connection.BeginTransaction();
                
                _saveStatusCommand.GetParameter(0).Value = status.ItemId;
                _saveStatusCommand.GetParameter(1).Value = status.ItemName;
                _saveStatusCommand.GetParameter(2).Value = status.ItemType;
                _saveStatusCommand.GetParameter(3).Value = status.SeriesName;
                _saveStatusCommand.GetParameter(4).Value = status.DateLastMetadataRefresh;
                _saveStatusCommand.GetParameter(5).Value = status.DateLastImagesRefresh;
                _saveStatusCommand.GetParameter(6).Value = status.LastStatus.ToString();
                _saveStatusCommand.GetParameter(7).Value = status.LastErrorMessage;
                _saveStatusCommand.GetParameter(8).Value = string.Empty;
                _saveStatusCommand.GetParameter(9).Value = string.Empty;
                _saveStatusCommand.GetParameter(10).Value = status.ItemDateModified;

                _saveStatusCommand.Transaction = transaction;

                _saveStatusCommand.ExecuteNonQuery();

                transaction.Commit();
            }
            catch (OperationCanceledException)
            {
                if (transaction != null)
                {
                    transaction.Rollback();
                }

                throw;
            }
            catch (Exception e)
            {
                _logger.ErrorException("Failed to save provider info:", e);

                if (transaction != null)
                {
                    transaction.Rollback();
                }

                throw;
            }
            finally
            {
                if (transaction != null)
                {
                    transaction.Dispose();
                }

                _writeLock.Release();
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private readonly object _disposeLock = new object();

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="dispose"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool dispose)
        {
            if (dispose)
            {
                try
                {
                    lock (_disposeLock)
                    {
                        if (_shrinkMemoryTimer != null)
                        {
                            _shrinkMemoryTimer.Dispose();
                            _shrinkMemoryTimer = null;
                        }

                        if (_connection != null)
                        {
                            if (_connection.IsOpen())
                            {
                                _connection.Close();
                            }

                            _connection.Dispose();
                            _connection = null;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.ErrorException("Error disposing database", ex);
                }
            }
        }
    }
}
