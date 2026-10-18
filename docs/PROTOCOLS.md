# Yamaha protocol reference

This document records publicly useful protocol behavior and historical receiver observations, **not an implementation-completeness claim**. The reference device is a **Yamaha RX-A3060** running firmware **2.86** with Extended Control API **2.08**. Availability and limits can differ by model, firmware, and current receiver state.

YamahaAvrNet is designed to use three protocol families together when available. The implementation includes Extended Control system discovery and the Phase 5.1 zone getStatus/sound/scene endpoints, legacy XML speaker GETs and limited opt-in verified-scope PUTs, plus a shared YNCA TCP session with typed zone status and optional guarded zone writes. Unverified functions remain disabled.

## 1. YNCA (TCP / optionally serial)

- Typical TCP port: **50000**, configurable where the receiver permits it.
- Command/status framing: `@SUBUNIT:FUNCTION=VALUE` followed by `\r\n` (CRLF).
- A GET uses `?`, e.g. `@MAIN:VOL=?\r\n`.
- Unsolicited status events can have the same format as GET responses. There is no reliable request ID; correlation must not be represented as certain when it is not.
- Distinguish `@RESTRICTED`, `@UNDEFINED`, no response, and connection/transport errors.
- A receiver may impose a one-client limit. Command consumers and monitoring therefore share **one centrally managed YNCA session**, with serialized GETs, timeouts, and reconnect-on-demand after a dropped or reset connection.
- Preserve unrecognized native subunit/function values rather than silently discarding them.

### Implemented YNCA TCP foundation (Phase 4.1 and Phase 4.2)

- The public `receiver.Ynca` facade is available only when `EnableYnca` is enabled. `ConnectAsync()` explicitly opens the TCP session; `GetAsync(subunit, function)` also reconnects if the session was closed.
- GET commands use validated ASCII identifiers and `@SUBUNIT:FUNCTION=?\r\n`. The raw public YNCA interface remains GET-only; a separate typed zone controller can send validated writes after explicit opt-in.
- A single background reader handles fragmented/combined TCP messages, strips CRLF, enforces a 4096-byte maximum line length, preserves unknown native messages and publishes `MessageReceived` events.
- Incoming values are cached by native `SUBUNIT:FUNCTION` key. Each `GetAsync` is serialized; a pending request accepts a matching observed value or `@RESTRICTED`/`@UNDEFINED`. Unsolicited updates are not falsely labeled as uniquely correlated GET replies.
- Cancellation, connect timeouts, request timeouts and connection loss are handled explicitly. After a request fault, the session is closed rather than reused with potentially delayed responses. A later explicit GET opens a new connection; there is no unrequested background reconnection or keepalive in this phase.
- The RX-A3060 running firmware 2.86 has now been **live-confirmed** reading `SYS:MODELNAME` and `MAIN:PWR`, `MAIN:VOL`, `MAIN:MUTE`, `MAIN:INP`, including a repeated `MAIN:VOL` GET. The sample observed main volume `-36.5 dB` on this run.
- The typed `ZoneControl` consumes the same TCP session: `RefreshAsync` reads `PWR`, `VOL`, `MUTE`, and `INP` for reported zones, while `GetStates` and `ZoneStateChanged` expose immutable zone-state snapshots. Absent values are not invented; cached values become `IsStale` on disconnect.
- Optional `EnableYncaZoneWrites` permits only typed, explicit power, mute, dB volume and reported-input changes. Volume requires a device-reported dB range/step; inputs require a native ID and conservative YNCA mapping. Each PUT is followed by a GET and comparison, with no PUT retry. The actual device effect remains unverified until a controlled hardware check.
- Windows WPF provides manual zone reads and individually confirmed writes. The console sample remains read-only.
- YNCA over serial, full per-model command discovery, keepalive and continuous background reconnect remain future work; reconnect occurs on the next explicit operation.

The Python [`ynca` project](https://github.com/mvdwetering/ynca) is a protocol research reference only, not a source-code port.

## 2. Yamaha Extended Control (HTTP/JSON)

The following GET endpoints returned usable data on the RX-A3060:

```text
GET /YamahaExtendedControl/v1/system/getDeviceInfo
GET /YamahaExtendedControl/v1/system/getFeatures
GET /YamahaExtendedControl/v1/system/getFuncStatus
```

The device identified itself as `RX-A3060`, firmware `2.86`, API version `2.08`. `getFeatures` reported `speaker_pattern_num = 2`, `zone_num = 4`, dynamic zone information, scene counts, available inputs/sound programs, and various `range_step` values. `getFuncStatus` reported active speaker pattern `1`.

For the main zone, the reported dB volume range was **−80.5 to +16.5 dB** with **0.5 dB** steps. The device's abstract numeric volume range (`0 ... 161` in the observed feature data) is not equivalent to dB. `zone_num`, `scene_num`, tuner/NETUSB presets, and `speaker_pattern_num` are distinct quantities.

The `setSpeakerPattern?num=1|2` function is documented and advertised by the reference receiver, but a live pattern switch was **not verified**. Do not enable it merely because the endpoint name exists.

### Implemented Extended Control behavior

- Uses an explicitly configured receiver host and HTTP port (`80` by default); there is no network scan.
- Sends only the three fixed GET endpoints above. No Extended Control write method is exposed.
- Reuses one `HttpClient` per receiver object and serializes requests, including direct `ExtendedControl` calls.
- Distinguishes HTTP status failures from Yamaha JSON `response_code` errors (`0` means success).
- Parses JSON using `Newtonsoft.Json`; unknown or missing optional fields remain unknown without rejecting an otherwise valid response.
- Requires valid response envelopes, limits response size to 2 MiB, uses a strict UTF-8 decoder and bounded JSON nesting, and supports cancellation and configurable timeout.
- Returns typed immutable snapshots: `YamahaExtendedDeviceInfo`, `YamahaExtendedFeatures`, and `YamahaExtendedFunctionStatus`.
- Reads `system.input_list` object entries and each zone's native `input_list`, `sound_program_list`, `func_list`, `scene_num`, and `range_step` independently. Additional `*_list` option arrays remain available per zone.
- Retains `volume` and `actual_volume_db` as separate native ranges; no inferred conversion is made.
- Preserves miscellaneous `getFuncStatus` keys as JSON literal strings without guessing their meaning.
- Populates the receiver's feature/capability snapshot with device-reported or read-verified evidence. Successful reads never imply write permission.

The library has **not** been live-validated on a physical RX-A3060 with the current implementation. Feature availability for each device is determined by its own GET responses. Neither `setSpeakerPattern` nor any other PUT is sent automatically.

## 3. Legacy Yamaha HTTP/XML

Endpoint:

```text
POST http://<receiver>/YamahaRemoteControl/ctrl
Content-Type: text/xml; charset=utf-8
```

### Implemented legacy XML reads

- `System/Speaker_Preout/Pattern_Sel` (active speaker pattern)
- `System/Speaker_Preout/Pattern_N/Lvl` (all native level values)
- `System/Speaker_Preout/Pattern_N/Distance` (the `Meter` aggregate)

The library builds only these known GET requests using `XDocument`; it **does not generate a standalone per-channel GET**. A reusable `HttpClient` sends `POST` to `/YamahaRemoteControl/ctrl` with `Content-Type: text/xml; charset=utf-8`. HTTP requests are serialized, bounded to 2 MiB and subject to timeout/cancellation. DTD processing and external entity resolution are disabled. Yamaha `RC` must be `0` before any XML values are parsed. Speaker channel names are kept verbatim, and their per-channel `Val`, `Exp`, and `Unit` are converted to `decimal` values using invariant culture. The parser selects `Distance/Meter`; missing or unexpected units produce explicit errors.

A successful read contributes `ReadVerified` evidence for that particular device/pattern in the client's capability snapshot. **Write support remains unknown**, and no live PUT methods are enabled.

Requests use `<YAMAHA_AV cmd="GET">` or `<YAMAHA_AV cmd="PUT">`. Evaluate the **HTTP status** and Yamaha's response attribute **`RC` independently**. On the reference device, `RC="0"` was observed for successful requests and `RC="2"` for certain rejected aggregate GET targets; these observations are **not** a universal return-code dictionary.

### Verified aggregate reads

A successful Pattern 1 level GET is structured as follows:

```xml
<YAMAHA_AV cmd="GET">
  <System>
    <Speaker_Preout>
      <Pattern_1><Lvl>GetParam</Lvl></Pattern_1>
    </Speaker_Preout>
  </System>
</YAMAHA_AV>
```

For distances, replace `Lvl` with `Distance`. For Pattern 2, use `Pattern_2`. The receiver accepted these **whole-block** GETs for both patterns, each returning native channel data.

The active pattern was also read using:

```xml
<YAMAHA_AV cmd="GET">
  <System>
    <Speaker_Preout><Pattern_Sel>GetParam</Pattern_Sel></Speaker_Preout>
  </System>
</YAMAHA_AV>
```

A separate Main Zone status GET on `Main_Zone/Basic_Status` succeeded.

By contrast, standalone GETs of `Lvl/Front_L` and `Distance/Meter/Front_L` returned **HTTP 400**, while some aggregate `Speaker_Preout`, `Pattern_1`, `Config`, and `PEQ` GETs returned `RC="2"`. **Do not infer arbitrary GET addresses from response tree structure.**

### Verified, controlled writes on the reference device

Only the following targeted Pattern 1 writes were verified by changing a value, reading it back, restoring the original, and reading it back again:

| Target | Original | Temporary value | Restored value |
| --- | ---: | ---: | ---: |
| `Pattern_1/Lvl/Front_L` | 0.0 dB | +0.5 dB | 0.0 dB |
| `Pattern_1/Distance/Meter/Front_L` | 2.95 m | 3.00 m | 2.95 m |

The request structure for a single-channel level write was:

```xml
<YAMAHA_AV cmd="PUT">
  <System>
    <Speaker_Preout>
      <Pattern_1>
        <Lvl>
          <Front_L><Val>5</Val><Exp>1</Exp><Unit>dB</Unit></Front_L>
        </Lvl>
      </Pattern_1>
    </Speaker_Preout>
  </System>
</YAMAHA_AV>
```

A corresponding distance PUT places `Front_L` under `Pattern_1/Distance/Meter` with `Val`, `Exp`, and `Unit` children. Example: `Val=300`, `Exp=2`, `Unit=m` means `3.00 m`.

The numerical rule in these responses is **physical value = `Val / 10^Exp`**, interpreted in the supplied `Unit`. For example, `Val=5`, `Exp=1` means `0.5 dB`; `Val=295`, `Exp=2` means `2.95 m`. Units and exponents must be read **per value** rather than assumed globally. Using `decimal` avoids unnecessary binary floating-point rounding for step-based settings.

**No Pattern 2 write, pattern switch, arbitrary-channel write, or general Config/PEQ write was verified.** A future writer must check pattern, channel, write capability, range, units, and the original value; perform explicit user-approved writes with readback; and never blindly retry after a PUT timeout.

### `desc.xml` is not a complete feature registry

`/YamahaRemoteControl/desc.xml` describes many menu commands but did **not** include the working `Speaker_Preout` level/distance paths observed on the RX-A3060. Treat it as partial device metadata. The mere existence of a field in a device backup does not establish that the same path supports live GET or PUT.

XML processing must disable DTD/external-entity resolution, enforce response-size limits, and build XML through APIs rather than untrusted tag interpolation.

## 4. Request examples and scope

The XML files under [`protocol-examples/rx-a3060/`](protocol-examples/rx-a3060/) contain **documented request payloads**, not simulated receiver responses. Their PUT examples are historical research artifacts and **must not be sent automatically**.

For native channel names, active-versus-addressable distinctions, and pattern-specific settings, see [Speaker setup](SPEAKER_SETUP.md). For per-model evidence, see [Compatibility](COMPATIBILITY.md).

## Restricted PUT implementation (Phase 3.2)

The library can now issue the **previously hardware-verified** legacy XML `PUT` path for `Pattern_1/Lvl/Front_L` and `Pattern_1/Distance/Meter/Front_L`, but only with explicit write opt-in, RX-A3060 firmware 2.86 identity, active Pattern 1 and confirmed native units/exponents. Only the reference values 0.0/0.5 dB or 2.95/3.00 m are accepted. Whole-aggregate GET reads provide preflight and post-write verification. HTTP success and Yamaha `RC=0` are checked separately; no automatic retries are made. This C# path still requires a real-device run before claiming it was verified in the new implementation.

## Sound programs and scene actions (Phase 5.1)

For a device reporting Extended Control zone features, the following additional
endpoints are used. Every HTTP response is validated for both HTTP success and
Yamaha `response_code == 0`:

```text
GET /YamahaExtendedControl/v1/{zone}/getStatus
GET /YamahaExtendedControl/v1/{zone}/setSoundProgram?program={advertised-native-id}
GET /YamahaExtendedControl/v1/{zone}/recallScene?num={advertised-scene-number}
```

The latter two HTTP GETs **have side effects** and are not used during discovery.
Native `sound_program_list` IDs are not interchangeable with `@MAIN:SOUNDPRG` display
values. The YNCA `MAIN:STRAIGHT`, `MAIN:PUREDIRMODE` and `MAIN:ENHANCER` fields are
handled through the existing shared YNCA session (On/Off only, with readback).
Scene recall is a command action: Yamaha success confirms receipt/acceptance, not
that every scene-dependent state was independently verified. Operations require an
explicit opt-in and must never be automatically retried after an uncertain timeout.

References: Yamaha Extended Control API Specification (zone getStatus,
setSoundProgram, recallScene); Yamaha YNCA MAIN function definitions. Individual
feature availability must still be determined from the receiver.
