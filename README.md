# YamahaAvrNet

YamahaAvrNet is an independent C# library for controlling Yamaha AV receivers. Its intended protocol coverage includes **YNCA** (TCP, optionally serial), **Yamaha Extended Control** (HTTP/JSON), and **legacy Yamaha HTTP/XML**. The design is independent of the Python `ynca` implementation; `ynca` is used only as a technical protocol reference.

**Target framework:** .NET Framework 4.8 (`net48`)  
**Language baseline:** C# 7.3  
**License:** MIT (see [LICENSE](LICENSE))

**Current implementation status:** Yamaha Extended Control discovery (`getDeviceInfo`, `getFeatures`, `getFuncStatus`) and legacy Yamaha HTTP/XML speaker reads are implemented. The RX-A3060's confirmed aggregate GET paths are used for active speaker pattern, level and distance queries. Both speaker patterns can be read by their explicit number; a bulk pattern enumeration uses the count reported by Extended Control. **Phase 3.2 adds a strictly limited, opt-in speaker PUT path** for the historically verified RX-A3060 firmware 2.86 / active Pattern 1 / `Front_L` level and distance values. YNCA TCP GETs were successfully observed on the RX-A3060. Phase 4.2 adds typed zone status and optional YNCA power, mute, dB volume and input writes, all guarded by explicit opt-in and readback. These new YNCA writes have not yet been hardware-verified. Active speaker configuration, PEQ, writing other speaker channels and Pattern 2 remain unimplemented. The C# legacy XML PUT implementation also requires hardware verification. Historical RX-A3060 observations are documented separately from software implementation.

## Solution and projects

Open **`YamahaAvrNet.slnx`**. The repository does not use a `.sln` file.

| Project | Purpose | Framework |
| --- | --- | --- |
| `YamahaAvrNet.csproj` + core source folders in the repository root | Standalone Yamaha library and Yamaha-specific API contracts | `net48` |
| `YamahaAvrNet.Sample/` | Interactive receiver discovery and optional speaker reads | `net48` |
| `YamahaAvrNet.TesterGui/` | Standalone WPF command browser; read-only by default, optional confirmed verified-scope writes | .NET Framework 4.8 |

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

To launch the GUI, open `YamahaAvrNet.slnx` in Visual Studio and select **`YamahaAvrNet.TesterGui`** as the startup project. Enter the receiver's IP address/hostname and HTTP port (normally `80`), then choose **Connect**. By default, all operations are read-only; to permit the two historically verified RX-A3060 writes, select **Enable limited RX-A3060 writes** *before* connecting and confirm each write separately. No writing occurs automatically. The left-hand grouped command list and right-hand result/HTTP/YNCA areas intentionally follow the DenonMarantz tester layout. No requests are sent on application startup.

The console sample asks for the receiver IP address or hostname and optional HTTP port **interactively** after starting. No command-line arguments are required:

```powershell
.\YamahaAvrNet.Sample\bin\Debug\net48\YamahaAvrNet.Sample.exe
```

Enter the receiver IP address or hostname when prompted. Press **Enter** to accept the default HTTP port `80`. The sample performs read-only discovery and displays the receiver information. It then asks whether to read legacy XML speaker settings. If selected, enter a reported pattern number to read levels and distances; press Enter to finish. After that, choose whether to connect to YNCA port `50000`, read `SYS:MODELNAME` and Main Zone status, and optionally request additional `SUBUNIT:FUNCTION` values. No command-line parameters are required. Other YNCA tools must disconnect first because many receivers permit only one TCP client.

The three GETs return device identity (model/firmware/API), system and per-zone functions, native zone/input/sound IDs, scene counts, native numeric ranges, speaker-pattern count and active pattern when the receiver reports them. These reads have been observed on an RX-A3060, firmware 2.86; other receiver models are not assumed compatible.

The WPF application is a **manual** command and diagnostic interface, not an automated test suite. It refuses writes unless the user opted in before connecting and explicitly confirms the selected target and current setting. Its HTTP output displays operation summaries, **not captured raw network traffic**. The GUI has YNCA connect/disconnect controls, read-only commands, and separately opted-in typed zone write commands with confirmation; unsolicited TCP messages are shown in the YNCA pane. Bilingual language switching and compatibility-report export remain future GUI work. The core library contains no WPF dependency. The `net48` target does not imply supported Linux or Raspberry Pi execution.

## Read-only YNCA TCP example

YNCA uses a single persistent TCP connection per receiver. The API below does not change AVR settings and works independently of the dispatcher or WPF GUI. The `InitializeAsync()` method remains Extended-Control-specific; connect YNCA explicitly through `Ynca.ConnectAsync()`.

```csharp
using (var receiver = new YamahaAvrClient(new YamahaAvrOptions
{
    Host = "192.168.1.50",
    EnableYnca = true,
    EnableExtendedControl = false,
    EnableLegacyXml = false
}))
{
    await receiver.Ynca.ConnectAsync();
    receiver.Ynca.MessageReceived += (sender, e) => Console.WriteLine(e.Message.Raw);
    var volume = await receiver.Ynca.GetAsync("MAIN", "VOL");
    Console.WriteLine("Observed volume: " + volume.Value);
    receiver.Ynca.Disconnect();
}
```

The transport bounds request time, serializes GETs, handles split and combined TCP frames, interprets `@RESTRICTED`/`@UNDEFINED`, and resets the session after an ambiguous timeout. Push notifications can be indistinguishable from solicited replies: a matching observation is **not** a cryptographically or transactionally correlated acknowledgment. Read-only operations never infer write capabilities. A dropped connection is reported and a subsequent explicit GET reconnects; no background reconnect/keepalive is implemented yet. The YNCA GET functionality was confirmed against the RX-A3060 in an interactive console run; optional zone writes remain unverified.

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


## Verified-scope speaker writes (manual opt-in)

These methods are disabled by default. The example below is **illustrative only**; do not run it without intending to change the receiver's speaker setting.

```csharp
using (var receiver = new YamahaAvrClient(new YamahaAvrOptions
{
    Host = "192.168.1.50",
    EnableYnca = false,
    EnableExtendedControl = true,
    EnableLegacyXml = true,
    EnableVerifiedSpeakerWrites = true
}))
{
    await receiver.InitializeAsync();
    await receiver.Speakers.SetLevelAsync(1, "Front_L", 0.5m);
    // Readback verification is performed internally; this does not auto-restore.
}
```

Only `RX-A3060` firmware `2.86`, Pattern `1`, native channel `Front_L` is eligible. The allowed pairs are **level 0.0/0.5 dB** and **distance 2.95/3.00 m**, based on previously completed receiver writes and readback. The setting must already contain one of the verified pair values, with its native exponent and unit. The library reads the active pattern and the original value, sends one PUT, checks the Yamaha RC and reads the aggregate again to verify the result. A timeout or failed verification does **not** trigger a retry or rollback. Other models, firmware versions, channels and Pattern 2 stay read-only until independently verified.

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

The methods are cancellable. Extended Control checks HTTP status and Yamaha's JSON `response_code`; legacy XML checks HTTP status and XML `RC`. Both reject oversized or malformed responses. HTTP/XML sends a PUT only through the restricted opt-in write methods; discovery remains read-only. An isolated `receiver.ExtendedControl.GetFeaturesAsync()` call returns freshly parsed data but does not update the facade's cached state; `InitializeAsync` and `RefreshAsync` do.

The library uses `Newtonsoft.Json` 13.0.4, which provides a .NET Framework-compatible runtime implementation. The host address is never logged by the library.

## API and design principles

- `YamahaAvrClient` is the application-facing entry point. Its lifecycle methods currently use read-only Extended Control when enabled. Speaker reads use a separate legacy XML provider; neither provider depends on the WPF GUI.
- `YamahaAvrOptions` defines host, HTTP and YNCA ports, enabled protocols, and a request timeout. Constructing the client does not open a connection. `InitializeAsync` currently requires Extended Control. `receiver.Ynca.ConnectAsync()` works without Extended Control; explicit legacy XML speaker reads are also independent of initialization.
- System features, zones, inputs, sound programs, scene counts, speaker-pattern counts, and numeric ranges are read from actual Extended Control responses. Native speaker channel IDs are discovered from aggregate legacy XML speaker reads; no universal fixed mapping is assumed.
- Capability status and evidence are tracked separately. An unknown capability is **not** treated as unsupported, and successful reads do not imply write permission.
- Extended Control volume ranges retain the receiver-reported **dB** values independently of its numerical display scale. Speaker levels and distances use `decimal` and preserve native unit and exponent.
- Addressable speaker channels, configured speakers, and channels currently carrying a signal are distinct concepts.
- YNCA GET requests and unsolicited messages share one TCP connection; the WPF application does not contain protocol-specific networking logic. Disconnected sessions are re-established on an explicit connect or subsequent GET. Continuous background reconnect, write commands, serial transport, and keepalive remain future work.
- Discovery remains read-only. Setting `YamahaAvrOptions.EnableVerifiedSpeakerWrites = true` is required for writes; the current implementation additionally requires `RX-A3060` firmware `2.86`, exactly two advertised patterns, active Pattern `1`, channel `Front_L`, a recognized native encoding and an allowed reference value. Level writes are restricted to `0.0` or `0.5 dB`; distance writes to `2.95` or `3.00 m`. Before/after aggregate reads verify the operation. No blind retries or automatic restoration are attempted; if a write times out, the receiver state is unknown and must be checked before proceeding.

## Documentation

- [Compatibility and evidence](docs/COMPATIBILITY.md): receiver-specific observations and verified/unknown features.
- [Protocol reference](docs/PROTOCOLS.md): YNCA, Extended Control, legacy XML, and observed RX-A3060 behavior.
- [Speaker setup](docs/SPEAKER_SETUP.md): native channels, speaker patterns, configuration semantics, levels, and distances.

The XML files in [`docs/protocol-examples/rx-a3060/`](docs/protocol-examples/rx-a3060/) are **documented request examples only**, not fabricated response captures. In particular, the PUT examples must not be sent automatically.

## License and external references

The project's own source code is licensed under the MIT license in [LICENSE](LICENSE). Technical protocol references include [`mvdwetering/ynca`](https://github.com/mvdwetering/ynca) and Yamaha's device interfaces; **no Python source code or proprietary Yamaha documentation is included** in the present codebase. If third-party source or protected assets are included in the future, their applicable notices and licenses must be preserved.

Do not commit private receiver backups, credentials, serial numbers, or unredacted network identifiers.

### YNCA zone control (Phase 4.2)

The existing shared YNCA TCP connection now provides `receiver.ZoneControl` for typed
zone status reads and explicitly opted-in writes. The supported YNCA subunits are
`MAIN`, `ZONE2`, `ZONE3`, and `ZONE4`, but **only if the receiver reports the
corresponding zone** in Extended Control discovery. For example:

```csharp
await receiver.InitializeAsync(); // Extended Control: device and zone discovery
var main = await receiver.ZoneControl.RefreshAsync("main");
Console.WriteLine(main.VolumeDb); // dB, or null if not observed
```

Use `receiver.ZoneControl.GetStates()` to obtain snapshots, and subscribe to
`ZoneStateChanged` for unsolicited YNCA updates. After disconnect, observed zone
states are retained but explicitly marked `IsStale=true`. The normal sample
program remains **read-only** and asks interactively whether to read the detected
zones after connecting over YNCA.

For optional manual writes, create the client with
`EnableYncaZoneWrites = true` and explicitly invoke `SetPowerAsync`, `SetMuteAsync`,
`SetVolumeDbAsync` or `SetInputAsync`. No write occurs during initialization,
refresh, or normal YNCA status reading. Volume is expressed **only in dB** and
requires device-reported `actual_volume_db` minimum, maximum and step; input
writes require an input ID actually reported for the selected zone. The WPF
tester asks for a separate write opt-in and a per-command confirmation.

After any write, the library issues a GET for that same YNCA field and checks
the observed value; it never retries a PUT automatically. YNCA has no request
IDs, so even a matching observation is **not an absolute guarantee** that a
spontaneous status message was not received instead of a response. This
new write path has **not yet been confirmed on hardware**. Please verify
receiver behavior before using it for routine control.
