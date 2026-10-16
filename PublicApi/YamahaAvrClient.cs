using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AvrNet.Abstractions.Interfaces;
using AvrNet.Abstractions.Models;
using YamahaAvrNet.Features.SpeakerSetup;

namespace YamahaAvrNet.PublicApi
{
    /// <summary>
    /// Independent Yamaha entry point. In phase 1 this exposes type-safe contracts only.
    /// Transport providers and real initialization will be implemented in later phases.
    /// </summary>
    public sealed class YamahaAvrClient : IAvrReceiver
    {
        private static readonly IReadOnlyList<AvrZoneInfo> EmptyZones =
            Array.AsReadOnly(new AvrZoneInfo[0]);

        private readonly YamahaAvrOptions _options;
        private readonly YamahaDeviceCapabilities _capabilities;
        private bool _disposed;

        public YamahaAvrClient(YamahaAvrOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            _options = options.CopyValidated();
            _capabilities = new YamahaDeviceCapabilities(new AvrCapability[0]);
            Speakers = new YamahaSpeakerSetupController(EnsureNotDisposed);
        }

        public string Host { get { return _options.Host; } }
        public string Manufacturer { get { return "Yamaha"; } }
        /// <summary>Null until a device discovery result has been received.</summary>
        public string Model { get { return null; } }
        public AvrConnectionState ConnectionState
        {
            get { return _disposed ? AvrConnectionState.Disposed : AvrConnectionState.Disconnected; }
        }

        public IReadOnlyList<AvrZoneInfo> Zones
        {
            get { EnsureNotDisposed(); return EmptyZones; }
        }

        public IReadOnlyList<AvrCapability> Capabilities
        {
            get { EnsureNotDisposed(); return _capabilities.All; }
        }

        public YamahaDeviceCapabilities YamahaCapabilities
        {
            get { EnsureNotDisposed(); return _capabilities; }
        }

        public IYamahaSpeakerSetup Speakers { get; private set; }

        public AvrCapability GetCapability(string featureId)
        {
            EnsureNotDisposed();
            return _capabilities.Get(featureId);
        }

        public YamahaReceiverState GetState()
        {
            EnsureNotDisposed();
            return new YamahaReceiverState(AvrConnectionState.Disconnected,
                null, true, null);
        }

        public Task ConnectAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            EnsureNotDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            throw NoTransport();
        }

        public Task InitializeAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            EnsureNotDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            throw NoTransport();
        }

        public Task RefreshAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            EnsureNotDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            throw NoTransport();
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            EnsureNotDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            // No transport exists in phase 1: disconnected is already the truthful state.
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _disposed = true;
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(YamahaAvrClient));
        }

        private static NotSupportedException NoTransport()
        {
            return new NotSupportedException(
                "Receiver communication is not implemented in phase 1. No connection was opened.");
        }
    }
}
