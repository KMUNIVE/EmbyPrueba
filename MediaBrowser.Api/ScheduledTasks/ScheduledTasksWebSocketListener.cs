﻿using MediaBrowser.Common.Net;
using MediaBrowser.Common.ScheduledTasks;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MediaBrowser.Api.ScheduledTasks
{
    /// <summary>
    /// Class ScheduledTasksWebSocketListener
    /// </summary>
    public class ScheduledTasksWebSocketListener : BasePeriodicWebSocketListener<IEnumerable<TaskInfo>, object>
    {
        /// <summary>
        /// Gets or sets the task manager.
        /// </summary>
        /// <value>The task manager.</value>
        private ITaskManager TaskManager { get; set; }

        /// <summary>
        /// Gets the name.
        /// </summary>
        /// <value>The name.</value>
        protected override string Name
        {
            get { return "ScheduledTasksInfo"; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ScheduledTasksWebSocketListener" /> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="taskManager">The task manager.</param>
        public ScheduledTasksWebSocketListener(ILogger logger, ITaskManager taskManager)
            : base(logger)
        {
            TaskManager = taskManager;
        }

        private bool _lastResponseHadTasksRunning = true;

        /// <summary>
        /// Gets the data to send.
        /// </summary>
        /// <param name="state">The state.</param>
        /// <returns>Task{IEnumerable{TaskInfo}}.</returns>
        protected override Task<IEnumerable<TaskInfo>> GetDataToSend(object state)
        {
            var tasks = TaskManager.ScheduledTasks.ToList();

            var anyRunning = tasks.Any(i => i.State != TaskState.Idle);

            if (anyRunning)
            {
                _lastResponseHadTasksRunning = true;
            }
            else
            {
                if (!_lastResponseHadTasksRunning)
                {
                    return Task.FromResult<IEnumerable<TaskInfo>>(null);
                }

                _lastResponseHadTasksRunning = false;
            }

            return Task.FromResult(tasks
                .OrderBy(i => i.Name)
                .Select(ScheduledTaskHelpers.GetTaskInfo)
                .Where(i => !i.IsHidden));
        }
    }
}
