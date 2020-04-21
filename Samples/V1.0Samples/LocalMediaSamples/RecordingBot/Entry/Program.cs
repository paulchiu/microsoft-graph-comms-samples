// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Program.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Entry
{
    using System;
    using System.Net;
    using Microsoft.Graph.Communications.Common.Telemetry;
    using Sample.PolicyRecordingBot.FrontEnd;

    /// <summary>
    /// RecordingBot entry.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// The graph logger.
        /// </summary>
        private readonly IGraphLogger logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="Program"/> class.
        /// </summary>
        public Program()
        {
            this.logger = new GraphLogger(typeof(Program).Assembly.GetName().Name, redirectToTrace: true);
        }

        /// <summary>
        /// Create a new bot and fire it up.
        /// </summary>
        /// <param name="args">
        /// Configurable input arguments.
        /// </param>
        public static void Main(string[] args)
        {
            var p = new Program();
            p.StartBot();
        }

        /// <summary>
        /// Configures the bot and starts it.
        /// </summary>
        private void StartBot()
        {
            try
            {
                // Set the maximum number of concurrent connections
                ServicePointManager.DefaultConnectionLimit = 12;

                Console.WriteLine("Hit");

                // Create and start the environment-independent service.
                Service.Instance.Initialize(new AzureConfiguration(this.logger), this.logger);
                Service.Instance.Start();

                Console.WriteLine("Running");

                this.logger.Info("WorkerRole has been started");
            }
            catch (Exception e)
            {
                this.logger.Error(e, "Exception on startup");
                throw;
            }
        }
    }
}
