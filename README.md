# YamahaAvrNet

YamahaAvrNet is an independent C# library for controlling Yamaha AV receivers. Its intended protocol coverage includes **YNCA** (TCP, optionally serial), **Yamaha Extended Control** (HTTP/JSON), and **legacy Yamaha HTTP/XML**. The design is independent of the Python `ynca` implementation; `ynca` is used only as a technical protocol reference.

**Target framework:** .NET Framework 4.8 (`net48`)  
**Language baseline:** C# 7.3  
**License:** MIT (see [LICENSE](LICENSE))

> **Current implementation status:** The repository contains the Phase 1 API contracts, receiver and speaker models, exception types, sample application, and WPF application skeleton. **Receiver communication is not implemented yet.** Connection, initialization, refresh, and speaker operations that require a protocol provider throw `NotSupportedException`; they do not contact an AVR. The documented RX-A3060 hardware observations are not claims that these features already work in this C# library.

## Solution and projects

Open **`YamahaAvrNet.slnx`**. The repository does not use a `.sln` file.

| Project | Purpose | Framework |
| --- | --- | --- |
| `AvrNet.Abstractions/` | Manufacturer-neutral interfaces and shared models | `net48` |
| `YamahaAvrNet.csproj` + core source folders in the repository root | Standalone Yamaha library and Yamaha-specific API contracts | `net48` |
| `YamahaAvrNet.Sample/` | Console application demonstrating the current API skeleton | `net48` |
| `YamahaAvrNet.TesterGui/` | Standalone WPF control and diagnostic application (currently a UI skeleton) | .NET Framework 4.8 |

All Yamaha library source files are located directly under the repository root (for example, `PublicApi/`, `Features/` and `Models/`). The root `YamahaAvrNet.csproj` explicitly includes only these source folders, preventing the sibling applications and shared abstractions from being compiled into the Yamaha assembly. There are no `src/`, `samples/` or `tools/` wrapper directories.

`YamahaAvrNet` can be referenced without `AvrDispatcher` and without WPF. The manufacturer-neutral abstractions are currently provisional and will need to be aligned with the latest `net48` DenonMarantzAvrNet API before relying on cross-manufacturer compatibility.

## Requirements and build

- Windows with Visual Studio 2022 **17.13 or later** and the **.NET desktop development** workload.
- **.NET Framework 4.8 Developer Pack / Targeting Pack** (the runtime alone is not sufficient for compilation).
- A compatible MSBuild version. If using the `dotnet` CLI to open a `.slnx` solution, use .NET SDK **9.0.200 or later**. The SDK version does not change the projects' `net48` target.

From a Developer PowerShell for Visual Studio in the repository root:

```powershell
msbuild .\YamahaAvrNet.slnx /restore /p:Configuration=Debug
```

To launch the UI, open `YamahaAvrNet.slnx` in Visual Studio and select **`YamahaAvrNet.TesterGui`** as the startup project. Select `YamahaAvrNet.Sample` to run the console application instead.

The WPF application is a **manual** control and diagnostic interface, not an automated test suite. The core library contains no WPF dependency. The `net48` target does not imply supported Linux or Raspberry Pi execution.

## API and design principles

- `YamahaAvrClient` is the application-facing entry point, with lifecycle operations and access to feature-specific controllers. It is **not yet a working network client**.
- `YamahaAvrOptions` defines host, HTTP and YNCA ports, enabled protocols, and a request timeout. Constructing the client does not open a connection.
- Device features, zones, inputs, speaker patterns, and native channel IDs must be discovered from actual receiver responses wherever possible; no universal fixed mapping is assumed.
- Capability status and evidence are tracked separately. An unknown capability is **not** treated as unsupported, and successful reads do not imply write permission.
- Volume in the shared abstraction is expressed in **dB**. Speaker levels and distances use `decimal` for measured values, with native unit and exponent retained.
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
