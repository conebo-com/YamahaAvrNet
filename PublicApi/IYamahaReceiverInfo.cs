using System;
using System.Collections.Generic;
using YamahaAvrNet.Models;

namespace YamahaAvrNet.PublicApi
{
    /// <summary>
    /// Yamaha-specific, read-only integration surface. Consumers such as a future
    /// external dispatcher may use it without depending on Yamaha transports or
    /// receiving any permission to change receiver settings.
    /// This contract deliberately belongs to YamahaAvrNet, not a shared assembly.
    /// </summary>
    public interface IYamahaReceiverInfo
    {
        event EventHandler<YamahaReceiverStateChangedEventArgs> ReceiverStateChanged;

        string Host { get; }
        string Manufacturer { get; }
        string Model { get; }
        YamahaConnectionState ConnectionState { get; }
        IReadOnlyList<YamahaZoneInfo> Zones { get; }
        IReadOnlyDictionary<string, YamahaProtocolProbe> Protocols { get; }
        IReadOnlyList<YamahaCapability> Capabilities { get; }
        IReadOnlyList<YamahaFunctionRoute> FunctionRoutes { get; }

        YamahaReceiverState GetState();
        YamahaReceiverOverview GetOverview();
        YamahaCapability GetCapability(string featureId);
        YamahaFunctionRoute GetPreferredVerifiedRoute(string functionId,
            YamahaFunctionOperation operation);
    }
}
