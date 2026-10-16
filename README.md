# YamahaAvrNet

YamahaAvrNet is an independent C# library for controlling Yamaha AV receivers. Its intended protocol coverage includes **YNCA** (TCP, optionally serial), **Yamaha Extended Control** (HTTP/JSON), and **legacy Yamaha HTTP/XML**. The design is independent of the Python `ynca` implementation; `ynca` is used only as a technical protocol reference.

**Target framework:** .NET Framework 4.8 (`net48`)  
**Language baseline:** C# 7.3  
**License:** MIT (see [LICENSE](LICENSE))

> **Current implementation status:** Read-only Yamaha Extended Control HTTP/JSON communication is implemented for `getDeviceInfo`, `getFeatures`, and `getFuncStatus`. `ConnectAsync`, `InitializeAsync`, and `RefreshAsync` use this provider when Extended Control is enabled. The standalone WPF application can discover and inspect a receiver manually. **YNCA, legacy Yamaha XML, zone control, and speaker setup commands are not implemented yet.** Receiver compatibility still depends on the real device; historical RX-A3060 observations are not an assertion of current hardware testing with this version of the library.

## Solution and projects

Open **`YamahaAvrNet.slnx`**. The repository does not use a `.sln` file.

| Project | Purpose | Framework |
| --- | --- | --- |
| `YamahaAvrNet.csproj` + core source folders in the repository root | Standalone Yamaha library and Yamaha-specific API contracts | `net48` |
| `YamahaAvrNet.Sample/` | Optional read-only console discovery example | `net48` |
| `YamahaAvrNet.TesterGui/` | Standalone WPF application with manual read-only device discovery | .NET Framework 4.8 |

All Yamaha library source files are located directly under the repository root (for example, `PublicApi/`, `Features/` and `Models/`). The root `YamahaAvrNet.csproj` explicitly includes only Yamaha source files. There are no `src/`, `samples/` or `tools/` wrapper directories.

### Standalone library and ownership of types

YamahaAvrNet has **no dependency on `AvrNet.Abstractions`, `AvrDispatcher`, or DenonMarantzAvrNet**. Receiver lifecycle, capabilities, zone descriptions, connection state, and speaker roles use dedicated Yamaha types from `YamahaAvrNet.Models` and `YamahaAvrNet.PublicApi`. No dispatcher or manufacturer-neutral interface is required to use this library.

Cross-manufacturer adapters and common contracts can be designed separately in the future AvrDispatcher project. Such adapters will consume YamahaAvrNet's public API; YamahaAvrNet will not reference AvrDispatcher.

## Requirements and build

- Windows with Visual Studio 2022 **17.13 or later** and the **.NET desktop development** workload.
- **.NET Framework 4.8 Developer Pack / Targeting Pack** (the runtime alone is not sufficient for compilation).
- A compatible MSBuild version. If using the `dotnet` CLI to open a `.slnx` solution, use .NET SDK **9.0.200 or later**. The SDK version does not change the projects' `net48` target.

From a Developer PowerShell for Visual Studio in the repository root:

```powershell
msbuild .\YamahaAvrNet.slnx /restore /p:Configuration=Debug
```

To launch the GUI, open `YamahaAvrNet.slnx` in Visual Studio and select **`YamahaAvrNet.TesterGui`** as the startup project. Enter the receiver's IP address/hostname and HTTP port (normally `80`), then choose **Connect and discover**. No requests are sent on application startup.

The console sample asks for the receiver IP address or hostname and optional HTTP port **interactively** after starting. No command-line arguments are required:

```powershell
.\YamahaAvrNet.Sample\bin\Debug\net48\YamahaAvrNet.Sample.exe
```

Enter the receiver IP address or hostname when prompted. Press **Enter** to accept the default HTTP port `80`. The sample performs read-only discovery and then displays the detected receiver information. Press **Enter** at the final prompt to close the console window.

The three GETs return device identity (model/firmware/API), system and per-zone functions, native zone/input/sound IDs, scene counts, native numeric ranges, speaker-pattern count and active pattern when the receiver reports them. **Actual device behavior is not automatically verified by merely compiling the library.**

The WPF application is a **manual** control and diagnostic interface, not an automated test suite. The core library contains no WPF dependency. The `net48` target does not imply supported Linux or Raspberry Pi execution.

## Read-only Extended Control example

```csharp
using (var receiver = new YamahaAvrClient(new YamahaAvrOptions
{
    Host = "192.168.1.50",
    EnableYnca = false,
    EnableLegacyXml = false,
    EnableExtendedControl = true
}))
{
    await receiver.InitializeAsync();
    var device = receiver.DeviceInfo;        // model, firmware and API version
    var features = receiver.DeviceFeatures;  // native zones, ranges and counts
    var status = receiver.FunctionStatus;    // active speaker pattern if reported
    await receiver.RefreshAsync();           // refreshes system function status
}
```

The methods are cancellable. They check HTTP status and Yamaha's JSON `response_code` separately, reject oversized/invalid JSON, and do not send write requests. An isolated `receiver.ExtendedControl.GetFeaturesAsync()` call returns freshly parsed data but does not update the facade's cached state; `InitializeAsync` and `RefreshAsync` do.

The library uses `Newtonsoft.Json` 13.0.4, which provides a .NET Framework-compatible runtime implementation. The host address is never logged by the library.

## API and design principles

- `YamahaAvrClient` is the application-facing entry point. Its lifecycle methods currently use read-only Extended Control when enabled, with protocol parsing delegated to a dedicated provider.
- `YamahaAvrOptions` defines host, HTTP and YNCA ports, enabled protocols, and a request timeout. Constructing the client does not open a connection. The only implemented provider is currently Extended Control, so disable it only if you do not intend to connect yet.
- System features, zones, inputs, sound programs, scene counts, speaker-pattern counts, and numeric ranges are read from actual Extended Control responses. Native speaker channel IDs will be discovered in the legacy-XML phase; no universal fixed mapping is assumed.
- Capability status and evidence are tracked separately. An unknown capability is **not** treated as unsupported, and successful reads do not imply write permission.
- Extended Control volume ranges retain the receiver-reported **dB** values independently of its numerical display scale. Speaker levels and distances will use `decimal` for measured values, with native unit and exponent retained when those features are implemented.
- Addressable speaker channels, configured speakers, and channels currently carrying a signal are distinct concepts.
- YNCA command handling and status monitoring will share one receiver connection; the WPF application will not contain protocol-specific networking logic.
- Discovery must remain read-only. Write operations require verified capabilities, explicit user action, validation, and readback rather than blind retries.

## Documentation

- [Compatibility and evidence](docs/COMPATIBILITY.md): receiver-specific observations and verified/unknown features.
- [Protocol reference](docs/PROTOCOLS.md): YNCA, Extended Control, legacy XML, and observed RX-A3060 behavior.
- [Speaker setup](docs/SPEAKER_SETUP.md): native channels, speaker patterns, configuration semantics, levels, and distances.

The XML files in [`docs/protocol-examples/rx-a3060/`](docs/protocol-examples/rx-a3060/) are **documented request examples only**, not fabricated response captures. In particular, the PUT examples must not be sent automatically.

## License and external references

The project's own source code is licensed under the MIT license in [LICENSE](LICENSE). Technical protocol references include [`mvdwetering/ynca`](https://github.com/mvdwetering/ynca) and Yamaha's device interfaces; **no Python source code or proprietary Yamaha documentation is included** in the present codebase. If third-party source or protected assets are included in the future, their applicable notices and licenses must be preserved.

Do not commit private receiver backups, credentials, serial numbers, or unredacted network identifiers.
