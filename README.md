# YamahaAvrNet

YamahaAvrNet is an independent C# library for controlling Yamaha AV receivers. Its intended protocol coverage includes **YNCA** (TCP, optionally serial), **Yamaha Extended Control** (HTTP/JSON), and **legacy Yamaha HTTP/XML**. The design is independent of the Python `ynca` implementation; `ynca` is used only as a technical protocol reference.

**Target framework:** .NET Framework 4.8 (`net48`)  
**Language baseline:** C# 7.3  
**License:** MIT (see [LICENSE](LICENSE))

**Current implementation status:** Multi-protocol read-only discovery now works without mandatory Extended Control. `InitializeAsync()` independently probes enabled Extended Control, legacy XML and YNCA; discovered protocol reachability and read evidence are exposed separately. A YNCA-only receiver can expose its observed model and successfully probed zone IDs, but does not gain unverified volume ranges, input lists or write rights. `desc.xml` / `UnitDesc.xml` are now optionally read as incomplete discovery metadata; broad model profiles are not implemented yet. Phase 5.1 additionally exposes sound/DSP status, guarded native sound program selection and scene recall. The RX-A3060 has now confirmed sound/DSP reads via both Extended Control and YNCA; sound/scene writes remain unverified. Yamaha Extended Control discovery (`getDeviceInfo`, `getFeatures`, `getFuncStatus`) and legacy Yamaha HTTP/XML speaker reads are implemented. The RX-A3060's confirmed aggregate GET paths are used for active speaker pattern, level and distance queries. Both speaker patterns can be read by their explicit number; a bulk pattern enumeration uses the count reported by Extended Control. **Phase 3.2 adds a strictly limited, opt-in speaker PUT path** for the historically verified RX-A3060 firmware 2.86 / active Pattern 1 / `Front_L` level and distance values. YNCA TCP GETs were successfully observed on the RX-A3060. Phase 4.2 adds typed zone status and optional YNCA power, mute, dB volume and input writes, all guarded by explicit opt-in and readback. The user reports successful manual GUI control on RX-A3060; exact per-zone coverage was not recorded. Active speaker configuration, PEQ, writing other speaker channels and Pattern 2 remain unimplemented. The C# legacy XML PUT implementation also requires hardware verification. Historical RX-A3060 observations are documented separately from software implementation.

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

## Phase 6.1: Stable read-only integration surface

The Yamaha-specific `IYamahaReceiverInfo` interface exposes receiver identity,
state changes, discovered zones, protocol probes, capabilities and native routes.
`YamahaAvrClient` implements it; **no new assembly or dependency is introduced**.
The future AvrDispatcher may implement its own manufacturer-neutral adapter
against this interface, but YamahaAvrNet does not reference that dispatcher.

Use `GetOverview()` when a consumer needs the receiver state and discovery
metadata from **one local API capture**, instead of combining several separate
property reads during concurrent YNCA events and HTTP refreshes:

```csharp
IYamahaReceiverInfo info = receiver;
YamahaReceiverOverview snapshot = info.GetOverview();
Console.WriteLine(snapshot.State.ConnectionState);
Console.WriteLine(snapshot.State.IsStale);
Console.WriteLine(snapshot.DiscoveredZones.Count);
Console.WriteLine(snapshot.FunctionRoutes.Count);
```

The returned collections are immutable copies of the current metadata and
contain no executable commands. `CapturedUtc` records the local snapshot time,
**not** the time when every field was observed. Individual zone states retain
`ObservedUtc` and `IsStale`; unknown values remain null. `GetOverview()` is
read-only and does not contact the receiver. Previous APIs remain available.

This step does not change YNCA/HTTP writes, the RX-A3060 speaker safeguards,
or the optional monitoring cadence. A Windows `net48` build and live receiver
validation are still required for this revision.

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

The core Extended Control discovery GETs return device identity (model/firmware/API), system and per-zone functions, native zone/input/sound IDs, scene counts, native numeric ranges, speaker-pattern count and active pattern when the receiver reports them. These reads have been observed on an RX-A3060, firmware 2.86; other receiver models are not assumed compatible.

The WPF application is a **manual** command and diagnostic interface, not an automated test suite. It refuses writes unless the user opted in before connecting and explicitly confirms the selected target and current setting. Its HTTP output displays operation summaries, **not captured raw network traffic**. The GUI has YNCA connect/disconnect controls, read-only commands, and separately opted-in typed zone write commands with confirmation; unsolicited TCP messages are shown in the YNCA pane. Bilingual language switching and compatibility-report export remain future GUI work. The core library contains no WPF dependency. The `net48` target does not imply supported Linux or Raspberry Pi execution.

## Read-only YNCA TCP example

YNCA uses a single TCP connection per receiver. `InitializeAsync()` now tests enabled protocols independently with read-only commands. A discovery-only YNCA connection is released afterwards to avoid monopolizing the port. To receive unsolicited messages continuously, explicitly connect `Ynca.ConnectAsync()` after discovery. This works without Extended Control.

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

The transport bounds request time, serializes GETs, handles split and combined TCP frames, interprets `@RESTRICTED`/`@UNDEFINED`, and resets the session after an ambiguous timeout. Push notifications can be indistinguishable from solicited replies: a matching observation is **not** a cryptographically or transactionally correlated acknowledgment. Read-only operations never infer write capabilities. A dropped connection is reported and a subsequent explicit GET reconnects; no background reconnect/keepalive is implemented yet. The YNCA GET functionality was confirmed against the RX-A3060 in an interactive console run; manual zone writes were subsequently reported working via the GUI, without per-zone evidence details.

## Multi-protocol discovery without Extended Control

```csharp
using (var receiver = new YamahaAvrClient(new YamahaAvrOptions
{
    Host = "192.168.1.50",
    EnableExtendedControl = false,
    EnableLegacyXml = false,
    EnableYnca = true
}))
{
    await receiver.InitializeAsync();
    Console.WriteLine(receiver.Model ?? "Model unknown");
    foreach (var probe in receiver.Protocols.Values)
        Console.WriteLine(probe.Protocol + ": " + probe.Availability);
    foreach (var zone in receiver.Zones)
        Console.WriteLine("Observed YNCA zone: " + zone.NativeId);
}
```

The YNCA-only mode supports read-only typed zone refreshes for **successfully observed**
native zones. It does **not** claim universal zone support or authorize zone writes
without Extended Control device-advertised ranges and IDs. Connection state
`PartiallyConnected` is possible when one enabled protocol works but another fails.
Availability means a particular endpoint/probe worked, not that all its commands work.

## Phase 5.3: Function-route and legacy descriptor discovery

`InitializeAsync()` now optionally retrieves `/YamahaRemoteControl/desc.xml` and falls
back to `/YamahaRemoteControl/UnitDesc.xml` **only when Legacy XML is enabled**.
The descriptor HTTP GET has a 1 MiB size limit, a timeout, no redirects, and
DTD/external entities disabled. A malformed or absent descriptor does not make
an otherwise functioning control endpoint unavailable. Descriptor contents
are **not** converted into executable commands or writable capabilities.
The public `LegacyDescriptor` snapshot exposes its source path, root name,
command-element count and any clearly identified native command IDs. A null
snapshot means missing or unverified metadata, not that Legacy XML is unsupported.

`FunctionRoutes` exposes a read-only list of native IDs associated with system
and per-zone functions advertised in Extended Control `getFeatures`, plus
successful YNCA zone `PWR` observations obtained during protocol discovery.
A route includes a Yamaha-specific logical feature ID, protocol, native
identifier, evidence source and status. These routes are **descriptive**; they
do not expose callable endpoints, authorize writes, or promise that an application
controller already implements the corresponding operation. The capability
snapshot also retains each advertised zone `range_step` and extra `*_list` under
`zone.<id>.range.<name>` and `zone.<id>.options.<name>`.

```csharp
await receiver.InitializeAsync();
foreach (var route in receiver.FunctionRoutes)
    Console.WriteLine(route.FunctionId + " | " + route.Protocol +
        " | " + route.NativeIdentifier + " | " + route.Status);
var descriptor = receiver.LegacyDescriptor;
if (descriptor != null)
    Console.WriteLine(descriptor.ResourcePath + ": " + descriptor.CommandElementCount);
```

The interactive sample offers an optional route dump after connecting.
The WPF tester offers **Overview → Function routes and legacy description**.
Neither operation sends device-setting writes. Future AvrDispatcher adapters
can consume these Yamaha snapshots without adding any Yamaha dependency on
AvrDispatcher, DenonMarantzAvrNet or a fourth shared library.

Model profiles should be introduced only for verified, otherwise undiscoverable
model/firmware differences; no generic device permissions are inferred from a
model number. Validation on models other than the RX-A3060 remains outstanding.

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

- `YamahaAvrClient` is the application-facing entry point. `InitializeAsync` uses safe, independent read-only probes for each enabled protocol. A failed protocol does not prevent another working protocol from being used. Speaker reads use a separate legacy XML provider; neither provider depends on the WPF GUI.
- `YamahaAvrOptions` defines host, HTTP and YNCA ports, enabled protocols, and a request timeout. Constructing the client does not open a connection. `InitializeAsync` no longer requires Extended Control. `receiver.Protocols` exposes `Available`, `Unavailable`, `Disabled` or `NotChecked` per protocol; `Unavailable` is not permanent lack of support. `receiver.Ynca.ConnectAsync()` and explicit legacy XML reads still work independently.
- System features, zones, inputs, sound programs, scene counts, speaker-pattern counts, and numeric ranges are read from actual Extended Control responses when available. Without Extended Control, model identity and recognized zone identifiers can come from successful YNCA queries; missing Extended-specific values stay unknown. Native speaker channel IDs are discovered from aggregate legacy XML speaker reads; no universal fixed mapping is assumed.
- Capability status and evidence are tracked separately. An unknown capability is **not** treated as unsupported, and successful reads do not imply write permission.
- Extended Control volume ranges retain the receiver-reported **dB** values independently of its numerical display scale. Speaker levels and distances use `decimal` and preserve native unit and exponent.
- Addressable speaker channels, configured speakers, and channels currently carrying a signal are distinct concepts.
- YNCA GET requests and unsolicited messages share one TCP connection; the WPF application does not contain protocol-specific networking logic. Disconnected sessions are re-established on an explicit connect or subsequent GET. Continuous background reconnect, write commands, serial transport, and keepalive remain future work.
- Discovery remains read-only. YNCA probing sends `SYS:MODELNAME` and, only when Extended Control did not provide zones, known native zone `PWR` GETs. Legacy XML probing requests `Pattern_Sel` only. Probes never send writes or switch speaker patterns. `desc.xml` cannot be relied upon as a complete list of Yamaha controls and is not parsed by this release. Setting `YamahaAvrOptions.EnableVerifiedSpeakerWrites = true` is required for writes; the current implementation additionally requires `RX-A3060` firmware `2.86`, exactly two advertised patterns, active Pattern `1`, channel `Front_L`, a recognized native encoding and an allowed reference value. Level writes are restricted to `0.0` or `0.5 dB`; distance writes to `2.95` or `3.00 m`. Before/after aggregate reads verify the operation. No blind retries or automatic restoration are attempted; if a write times out, the receiver state is unknown and must be checked before proceeding.

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
zone writes have been **reported working through the GUI on an RX-A3060**.
This is not a model-wide or per-zone compatibility claim.

## Sound programs, DSP and scenes (Phase 5.1)

`receiver.Sound` adds a typed API for receiver-advertised Yamaha sound and scene features.
It is available when Extended Control is enabled and discovery has populated `DeviceFeatures`.

- `GetStatusAsync(zone)` reads `/v1/{zone}/getStatus` and returns the advertised native sound
  program ID and available Direct/Pure Direct/Enhancer/decoder fields (unknown remains `null`).
- `GetAdvertisedSoundPrograms(zone)` and `GetAdvertisedSceneCount(zone)` return only the
  features reported for that zone, without inventing static presets.
- `SetSoundProgramAsync(zone, nativeId)` uses the **Extended Control native ID** obtained
  from `getFeatures`, sends `/setSoundProgram` and checks a subsequent `/getStatus` readback.
  Do not substitute a YNCA `SOUNDPRG` display value for the Extended ID.
- `SetStraightAsync`, `SetPureDirectAsync` and `SetEnhancerAsync` currently target YNCA
  `MAIN` only and use the one shared TCP session plus observed readback. Unsupported or
  temporarily unavailable functions can still be rejected by the receiver.
- `RecallSceneAsync(zone, number)` sends `/recallScene` only after validating the zone's
  advertised `scene` function and scene count. It checks HTTP and Yamaha's
  `response_code`, **not** the final multi-setting scene state. This is an action, not a
  regular readback-verifiable field. The RX-A3060 advertised 12 Main Scenes and 4
  Scenes in each additional zone; this is not a universal constant.

All Phase 5.1 writes require `EnableSoundSceneWrites = true`; the option is false by
 default. The WPF tester has a separate opt-in checkbox and requires confirmation
for each sound/scene change. The interactive console sample remains read-only and
can display the sound status, available sound IDs and scene count. The new Phase 5.1
C# write methods have **not** yet been live-validated on the receiver.

## Phase 5.4: Per-protocol routes and evidence-aware resolution

Phase 5.4 adds a command implementation catalog alongside receiver-advertised
metadata. `FunctionRoutes` now includes implemented YNCA zone commands and Main
DSP switches, observed YNCA GETs, Extended Control sound/scene operations,
and Legacy XML aggregate speaker GETs. An **implemented** command is not proof
that a particular receiver supports it. `ImplementedRead`, `ImplementedWrite`
and `ImplementedExecute` describe library code; `CanRead`, `CanWrite`, and
`CanExecute` are nullable, receiver-instance-specific observations. Unprobed
capabilities stay `null`, not `false`.

The existing controllers publish successful GET/readback evidence. A successfully
verified zone/sound/speaker write is recorded only after its existing guardrails
and readback have passed. A successful `recallScene` response is marked
`ActionAccepted` and `CanExecute=true`: this means the action was **accepted**,
not that all resulting settings were independently verified. Historical write
tests are not silently promoted to live evidence for a newly discovered device.
All write opt-in settings and model-specific speaker restrictions remain in
force, and discovery itself sends no writes.

`GetPreferredVerifiedRoute(featureId, operation)` is a read-only, deterministic
*diagnostic selector*: it returns a route only when that operation has positive
evidence and a library implementation. It returns `null` when evidence is
missing. It does **not** execute or dispatch calls, bypass validation, or
guarantee subsequent access. Future AvrDispatcher adapters may use this
metadata, but YamahaAvrNet remains independent of AvrDispatcher.

```csharp
await receiver.InitializeAsync();
var route = receiver.GetPreferredVerifiedRoute(
    "zone.main.power", YamahaFunctionOperation.Read);
Console.WriteLine(route == null ? "No verified read route yet" :
    route.Protocol + ":" + route.NativeIdentifier);
```

The optional legacy descriptor now exposes a diagnostic snapshot even when the
XML itself is unavailable. `LegacyDescriptorDiagnostic.Attempts` distinguishes
HTTP 404/410, other HTTP errors, malformed or unrecognized XML, network errors
and timeouts. This does **not** affect availability of working legacy control
paths. The sample and WPF tester display the attempt results.

Only the command mappings already implemented by the library are cataloged;
Phase 5.4 is **not** a universal Yamaha command database, automatic write
permission system, or multi-model verification. A receiver-specific hardware
check and a Windows `net48` build are still required for this revision.

## Phase 5.5: Audio signal and advanced DSP inspection

`receiver.Sound.GetSignalInfoAsync("main")` reads the receiver-advertised
`/YamahaExtendedControl/v1/main/getSignalInfo` endpoint. The returned
`YamahaAudioSignalInfo` preserves Yamaha-native `audio.format`, `audio.fs`,
`audio.error`, and any additional scalar or object fields for diagnostics.
Missing, empty, and `---` fields mean **unknown**, especially in standby or
with no active signal. An HTTP/Yamaha response_code success is evidence of
endpoint availability, **not** evidence of current playback or Dolby/DTS decoding.

`receiver.Sound.GetAdvertisedSurroundDecoderTypes("main")` exposes the native
`getFeatures` decoder list. `SetSurroundDecoderTypeAsync` is available only with
`EnableSoundSceneWrites=true`, validates the advertised zone, function and ID,
and requires a matching `getStatus` readback. It is **not yet hardware-verified**.
The method uses the documented *example* endpoint `setSurroundDecoderType`:
Yamaha's 2018 API specification contains an inconsistent URL table for this
method. Other Sound/Scene write safeguards remain unchanged.

`receiver.Sound.GetMainYncaDspStatusAsync()` also works when Extended Control
is disabled (with YNCA enabled). It performs read-only queries for
`MAIN:SOUNDPRG`, `STRAIGHT`, `PUREDIRMODE`, `ENHANCER`, `3DCINEMA` and
`ADAPTIVEDSP`, reporting UNDEFINED/RESTRICTED individually. Native YNCA
`SOUNDPRG` is **stored DSP**, while Extended Control `sound_program` can
represent the effective mode (e.g. `straight`). `surround_3d` from Extended
Control is kept distinct from the native YNCA `3DCINEMA` field. No YNCA
advanced DSP writes or guessed Dolby Atmos/DTS:X decoding interpretations are
introduced in this phase.

The console sample and tester GUI include manual audio-signal, decoder-list
and native DSP inspection. No audio playback is necessary for connectivity
testing. No new model profile or dependency on AvrDispatcher is introduced.

## Opt-in read-only receiver monitoring (Phase 5.6)

The receiver is **not** monitored automatically. Applications can start one
read-only monitoring loop after a successful `InitializeAsync()`. It uses the
existing YNCA TCP session (never a second YNCA connection), including spontaneous
updates, and the independent Extended Control `getFuncStatus` endpoint when
available. It avoids polling functions not advertised for a zone by Extended
Control; for example, RX-A3060 Zone 4 does not advertise volume or mute.

```csharp
using (var receiver = new YamahaAvrClient(new YamahaAvrOptions
{
    Host = "192.168.1.50"
}))
{
    await receiver.InitializeAsync();
    receiver.ReceiverStateChanged += (sender, args) =>
        Console.WriteLine("Receiver: " + args.State.ConnectionState +
            ", stale: " + args.State.IsStale);
    receiver.Monitor.PollCompleted += (sender, args) =>
        Console.WriteLine("Read paths: " + args.SuccessfulReads +
            ", errors: " + (args.Error ?? "none"));
    receiver.Monitor.Start(TimeSpan.FromSeconds(30));
    Console.ReadLine();
    await receiver.Monitor.StopAsync();
    await receiver.DisconnectAsync();
}
```

The monitor requires a 5-second to 1-hour polling interval; a 30-second
cadence is the sample and GUI default. Complete read failures use bounded
backoff (up to 180 seconds); a partial failure reports errors without losing
successful observations. YNCA zones carry their own `IsStale` indicators on
connection loss, while the global receiver state describes overall discovery
and HTTP freshness. A successful GET does not imply a writable feature.

`YamahaAvrNet.Sample` offers an interactive read-only monitoring session after
its speaker questions; press Enter to stop. The WPF command browser exposes
**Monitoring → Start read-only monitoring**, **Stop read-only monitoring** and
**Show receiver state snapshot**. Manual YNCA disconnect also stops polling.
No autonomous status writes, command retries or separate monitoring process are
created. Monitoring is not a replacement for an always-connected hardware
availability probe or a periodic audio-signal check.

**Validation:** The Phase 5.6 implementation is awaiting a Windows `net48`
build and hardware test. Previous RX-A3060 console output confirmed read-only
advanced DSP `3DCINEMA=Auto` and `ADAPTIVEDSP=Auto`. Without playback,
`getSignalInfo` returned `response_code=0` and `audio.error=0`, with unknown
format and sample rate. This is not evidence of any particular input codec.
