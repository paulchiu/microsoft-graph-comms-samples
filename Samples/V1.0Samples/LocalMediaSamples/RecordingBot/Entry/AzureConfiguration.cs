// --------------------------------------------------------------------------------------------------------------------
// <copyright file="AzureConfiguration.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// </copyright>
// <summary>
//   The configuration for azure.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Entry
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Diagnostics;
    using System.Net;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using Microsoft.Graph.Communications.Common.Telemetry;
    using Microsoft.Skype.Bots.Media;
    using Sample.PolicyRecordingBot.FrontEnd;
    using Sample.PolicyRecordingBot.FrontEnd.Http;

    /// <summary>
    /// Reads the Configuration from service Configuration.
    /// </summary>
    internal class AzureConfiguration : IConfiguration
    {
        /// <summary>
        /// The default endpoint key.
        /// </summary>
        private const string DefaultEndpointKey = "DefaultEndpoint";

        /// <summary>
        /// The Microsoft app id key.
        /// </summary>
        private const string AadAppIdKey = "AadAppId";

        /// <summary>
        /// The Microsoft app password key.
        /// </summary>
        private const string AadAppSecretKey = "AadAppSecret";

        /// <summary>
        /// The default certificate key.
        /// </summary>
        private const string DefaultCertificateKey = "CertificateThumbprint";

        /// <summary>
        /// The default internal port key.
        /// </summary>
        private const string InstanceInternalPortKey = "InstanceInternalPort";

        /// <summary>
        /// The default public port key.
        /// </summary>
        private const string InstancePublicPortKey = "InstancePublicPort";

        /// <summary>
        /// The place call endpoint URL key.
        /// </summary>
        private const string PlaceCallEndpointUrlKey = "PlaceCallEndpointUrl";

        /// <summary>
        /// The instance media control endpoint key.
        /// </summary>
        private const string InstanceMediaControlEndpointKey = "InstanceMediaControlEndpoint";

        /// <summary>
        /// The service dns name key.
        /// </summary>
        private const string ServiceDnsNameKey = "ServiceDNSName";

        /// <summary>
        /// The service cname key.
        /// </summary>
        private const string ServiceCNameKey = "ServiceCNAME";

        /// <summary>
        /// The default Microsoft app id value.
        /// </summary>
        private const string DefaultAadAppIdValue = "%AadAppId%";

        /// <summary>
        /// The default Microsoft app password value.
        /// </summary>
        private const string DefaultAadAppSecretValue = "%AadAppSecret%";

        /// <summary>
        /// The default public port value.
        /// </summary>
        private const string DefaultInstancePublicPortValue = "%PublicTCPPort%";

        /// <summary>
        /// The default public port value.
        /// </summary>
        private const string DefaultServiceCNameValue = "%CName%";

        /// <summary>
        /// The default public port value.
        /// </summary>
        private const string DefaultServiceDnsNameValue = "%ServiceDns%";

        /// <summary>
        /// localPort specified in <InputEndpoint name="DefaultCallControlEndpoint" protocol="tcp" port="443" localPort="9441" />
        /// in .csdef. This is needed for running in emulator. Currently only messaging can be debugged in the emulator.
        /// Media debugging in emulator will be supported in future releases.
        /// </summary>
        private const int DefaultPort = 9441;

        /// <summary>
        /// Graph logger.
        /// </summary>
        private IGraphLogger graphLogger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureConfiguration"/> class.
        /// </summary>
        /// <param name="logger">Logger instance.</param>
        public AzureConfiguration(IGraphLogger logger)
        {
            this.graphLogger = logger;
            this.Initialize();
        }

        /// <inheritdoc/>
        public string ServiceDnsName { get; private set; }

        /// <summary>
        /// Gets the service cname.
        /// </summary>
        public string ServiceCname { get; private set; }

        /// <inheritdoc/>
        public IEnumerable<Uri> CallControlListeningUrls { get; private set; }

        /// <inheritdoc/>
        public Uri CallControlBaseUrl { get; private set; }

        /// <inheritdoc/>
        public Uri PlaceCallEndpointUrl { get; private set; }

        /// <inheritdoc/>
        public MediaPlatformSettings MediaPlatformSettings { get; private set; }

        /// <inheritdoc/>
        public string AadAppId { get; private set; }

        /// <inheritdoc/>
        public string AadAppSecret { get; private set; }

        /// <summary>
        /// Initialize from serviceConfig.
        /// </summary>
        public void Initialize()
        {
            // Collect config values from Azure config.
            this.TraceEndpointInfo();
            this.ServiceDnsName = this.GetString(ServiceDnsNameKey, DefaultServiceDnsNameValue);
            this.ServiceCname = this.GetString(ServiceCNameKey, DefaultServiceCNameValue, true);
            if (string.IsNullOrEmpty(this.ServiceCname))
            {
                this.ServiceCname = this.ServiceDnsName;
            }

            var placeCallEndpointUrlStr = this.GetString(PlaceCallEndpointUrlKey, null, true);
            if (!string.IsNullOrEmpty(placeCallEndpointUrlStr))
            {
                this.PlaceCallEndpointUrl = new Uri(placeCallEndpointUrlStr);
            }

            X509Certificate2 defaultCertificate = this.GetCertificateFromStore(DefaultCertificateKey);

            this.AadAppId = this.GetString(AadAppIdKey, DefaultAadAppIdValue);
            this.AadAppSecret = this.GetString(AadAppSecretKey, DefaultAadAppSecretValue);

            List<Uri> controlListenUris = new List<Uri>();

            var baseDomain = Debugger.IsAttached ? "localhost" : this.ServiceCname;

            // Create structured config objects for service.
            this.CallControlBaseUrl = new Uri(string.Format(
                "https://{0}/{1}/{2}",
                this.ServiceCname,
                HttpRouteConstants.CallSignalingRoutePrefix,
                HttpRouteConstants.OnNotificationRequestRoute));

            controlListenUris.Add(new Uri("https://" + baseDomain + ":" + DefaultPort + "/"));
            controlListenUris.Add(new Uri("http://" + baseDomain + ":" + (DefaultPort + 1) + "/"));

            this.TraceConfigValue("CallControlCallbackUri", this.CallControlBaseUrl);
            this.CallControlListeningUrls = controlListenUris;

            foreach (Uri uri in this.CallControlListeningUrls)
            {
                this.TraceConfigValue("Call control listening Uri", uri);
            }

            var instanceAddresses = Dns.GetHostEntry(baseDomain).AddressList;
            if (instanceAddresses.Length == 0)
            {
                throw new InvalidOperationException("Could not resolve the PIP hostname. Please make sure that PIP is properly configured for the service");
            }

            int mediaPort = int.Parse(this.GetString(InstanceInternalPortKey, null));
            int tcpPort = int.Parse(this.GetString(InstancePublicPortKey, DefaultInstancePublicPortValue));

            this.MediaPlatformSettings = new MediaPlatformSettings()
            {
                MediaPlatformInstanceSettings = new MediaPlatformInstanceSettings()
                {
                    CertificateThumbprint = defaultCertificate.Thumbprint,
                    InstanceInternalPort = mediaPort,
                    InstancePublicIPAddress = instanceAddresses[0],
                    InstancePublicPort = tcpPort,
                    ServiceFqdn = this.ServiceCname,
                },
                ApplicationId = this.AadAppId,
            };
        }

        /// <summary>
        /// Dispose the Configuration.
        /// </summary>
        public void Dispose()
        {
        }

        /// <summary>
        /// Lookup configuration value.
        /// </summary>
        /// <param name="key">Configuration key.</param>
        /// <param name="defaultValue">Checks if default value is overridden or not.</param>
        /// <param name="allowEmpty">If empty configurations are allowed.</param>
        /// <returns>Configuration value, if found.</returns>
        private string GetString(string key, string defaultValue, bool allowEmpty = false)
        {
            string s = ConfigurationManager.AppSettings[key];

            this.TraceConfigValue(key, s);

            if (!allowEmpty && (string.IsNullOrWhiteSpace(s) || string.Equals(s, defaultValue)))
            {
                throw new ConfigurationException(key, "The Configuration value is null or empty or is not set.");
            }

            if (allowEmpty && string.Equals(s, defaultValue))
            {
                return null;
            }

            return s;
        }

        /// <summary>
        /// Helper to search the certificate store by its thumbprint.
        /// </summary>
        /// <param name="key">Configuration key containing the Thumbprint to search.</param>
        /// <returns>Certificate if found.</returns>
        private X509Certificate2 GetCertificateFromStore(string key)
        {
            string thumbprint = this.GetString(key, null);

            X509Store store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);
            try
            {
                X509Certificate2Collection certs = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, validOnly: false);
                if (certs.Count != 1)
                {
                    throw new ArgumentException(key, $"No certificate with thumbprint {thumbprint} was found in the machine store.");
                }

                return certs[0];
            }
            finally
            {
                store.Close();
            }
        }

        /// <summary>
        /// Write endpoint info into the debug logs.
        /// </summary>
        private void TraceEndpointInfo()
        {
            string[] endpoints = Debugger.IsAttached
                ? new string[] { DefaultEndpointKey }
                : new string[] { DefaultEndpointKey, InstanceMediaControlEndpointKey };

            foreach (string endpointName in endpoints)
            {
                StringBuilder info = new StringBuilder();
                info.AppendFormat("Internal=https://{0}, ", this.ServiceCname);
                string publicInfo = "-";
                info.AppendFormat("PublicPort={0}", publicInfo);
                this.TraceConfigValue(endpointName, info);
            }
        }

        /// <summary>
        /// Write debug entries for the configuration.
        /// </summary>
        /// <param name="key">Configuration key.</param>
        /// <param name="value">Configuration value.</param>
        private void TraceConfigValue(string key, object value)
        {
            this.graphLogger.Info($"{key} ->{value}");
        }
    }
}
