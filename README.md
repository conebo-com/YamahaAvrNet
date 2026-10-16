# YamahaAvrNet

YamahaAvrNet is an independent C# library for controlling Yamaha AV receivers. Its intended protocol coverage includes **YNCA** (TCP, optionally serial), **Yamaha Extended Control** (HTTP/JSON), and **legacy Yamaha HTTP/XML**. The design is independent of the Python `ynca` implementation; `ynca` is used only as a technical protocol reference.

**Target framework:** .NET Framework 4.8 (`net48`)  
**Language baseline:** C# 7.3  
**License:** MIT (see [LICENSE](LICENSE))

**Current implementation status:** Yamaha Extended Control discovery (`getDeviceInfo`, `getFeatures`, `getFuncStatus`) and **read-only legacy Yamaha HTTP/XML speaker settings** are implemented. The RX-A3060's confirmed aggregate GET paths are used for active speaker pattern, level and distance queries. Both speaker patterns can be read by their explicit number; a bulk pattern enumeration uses the count reported by Extended Control. **Speaker writes, YNCA, active speaker configuration and PEQ are not implemented.** Hardware behavior with this C# build has not yet been validated. Historical RX-A3060 observations are documented separately from software implementation.

## Solution and projects

Open **`YamahaAvrNet.slnx`**. The repository does not use a `.sln` file.

| Project | Purpose | Framework |
| --- | --- | --- |
| `YamahaAvrNet.csproj` + core source folders in the repository root | Standalone Yamaha library and Yamaha-specific API contracts | `net48` |
| `YamahaAvrNet.Sample/` | Interactive receiver discovery and optional speaker reads | `net48` |
| `YamahaAvrNet.TesterGui/` | Standalone WPF command browser with manual read-only operations | .NET Framework 4.8 |

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

To launch the GUI, open `YamahaAvrNet.slnx` in Visual Studio and select **`YamahaAvrNet.TesterGui`** as the startup project. Enter the receiver's IP address/hostname and HTTP port (normally `80`), then choose **Connect**. The left-hand grouped command list and right-hand result/HTTP/YNCA areas intentionally follow the DenonMarantz tester layout. No requests are sent on application startup.

The console sample asks for the receiver IP address or hostname and optional HTTP port **interactively** after starting. No command-line arguments are required:

```powershell
.\YamahaAvrNet.Sample\bin\Debug\net48\YamahaAvrNet.Sample.exe
```

Enter the receiver IP address or hostname when prompted. Press **Enter** to accept the default HTTP port `80`. The sample performs read-only discovery and displays the receiver information. It then asks whether to read legacy XML speaker settings. If selected, enter a reported pattern number to read levels and distances; press Enter to finish. No command-line parameters are required.

The three GETs return device identity (model/firmware/API), system and per-zone functions, native zone/input/sound IDs, scene counts, native numeric ranges, speaker-pattern count and active pattern when the receiver reports them. **Actual device behavior is not automatically verified by merely compiling the library.**

The WPF application is a **manual** command and diagnostic interface, not an automated test suite. Its HTTP output displays operation summaries, **not captured raw network traffic**. YNCA output, bilingual language switching, and compatibility-report export remain future GUI work. The core library contains no WPF dependency. The `net48` target does not imply supported Linux or Raspberry Pi execution.

## Read-only speaker settings example

```csharp
using (var receiver = new YamahaAvrClient(new YamahaAvrOptions
{
    Host = "192.168.1.50",
    EnableYnca = false,
    EnableExtendedControl = true,
    EnableLegacyXml = true
}))
{
    await receiver.InitializeAsync();

    int? active = await receiver.Speakers.GetActivePatternAsync();
    var levels = await receiver.Speakers.GetLevelsAsync(1);
    var distances = await receiver.Speakers.GetDistancesAsync(1);
    foreach (var value in levels)
        Console.WriteLine(value.Key + ": " + value.Value.Value + " " + value.Value.Unit);
}
```

Values retain their original `Val`, `Exp`, and `Unit` representation. Native channel IDs are read from the device, not a hardcoded channel list. Aggregate reads do **not** establish whether the listed speakers are configured. Pattern 2 reads were historically successful on the RX-A3060; writes to Pattern 2 have not been verified.

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

The methods are cancellable. Extended Control checks HTTP status and Yamaha's JSON `response_code`; legacy XML checks HTTP status and XML `RC`. Both reject oversized or malformed responses and send no write requests. An isolated `receiver.ExtendedControl.GetFeaturesAsync()` call returns freshly parsed data but does not update the facade's cached state; `InitializeAsync` and `RefreshAsync` do.

The library uses `Newtonsoft.Json` 13.0.4, which provides a .NET Framework-compatible runtime implementation. The host address is never logged by the library.

## API and design principles

- `YamahaAvrClient` is the application-facing entry point. Its lifecycle methods currently use read-only Extended Control when enabled. Speaker reads use a separate legacy XML provider; neither provider depends on the WPF GUI.
- `YamahaAvrOptions` defines host, HTTP and YNCA ports, enabled protocols, and a request timeout. Constructing the client does not open a connection. `InitializeAsync` currently requires Extended Control; explicit speaker reads can use legacy XML independently of initialization if enabled.
- System features, zones, inputs, sound programs, scene counts, speaker-pattern counts, and numeric ranges are read from actual Extended Control responses. Native speaker channel IDs are discovered from aggregate legacy XML speaker reads; no universal fixed mapping is assumed.
- Capability status and evidence are tracked separately. An unknown capability is **not** treated as unsupported, and successful reads do not imply write permission.
- Extended Control volume ranges retain the receiver-reported **dB** values independently of its numerical display scale. Speaker levels and distances use `decimal` and preserve native unit and exponent.
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
