# Device compatibility and evidence

This document distinguishes **observed receiver behavior** from **features currently implemented by YamahaAvrNet**. Historical observations were made on a **Yamaha RX-A3060, firmware 2.86, Extended Control API 2.08**. They must not be generalized to other models or firmware versions.

**Implementation status:** The C# library now implements the read-only Extended Control `getDeviceInfo`, `getFeatures`, and `getFuncStatus` operations. Legacy XML speaker aggregate GETs are implemented and have now been exercised successfully on the user's RX-A3060 (firmware 2.86): both patterns returned 13 native level/distance channels. The Phase 3.2 C# implementation adds narrowly restricted opt-in PUT for Pattern 1 / `Front_L`; this new writer has not yet been exercised on the receiver. YNCA TCP GETs have been observed in an interactive C# RX-A3060 run. The user subsequently reported the Phase 4.2 GUI controls working on RX-A3060; exact tested zone/function combinations were not enumerated.

## Multi-protocol discovery (RX-A3060 hardware validated for Phase 5.2)

- Initialization no longer requires Extended Control: each enabled endpoint is probed with read-only commands independently. At least one usable endpoint is sufficient.
- `receiver.Protocols` reports availability for `ExtendedControl`, `LegacyXml`, and `YNCA`, with details. `Unavailable` is **not** a claim of permanent incompatibility.
- YNCA-only discovery can identify `SYS:MODELNAME` and discover a zone **only after** its `PWR` query returns a value. The four candidate subunit names are protocol knowledge, not assumed existing zones.
- A temporary YNCA discovery connection is closed after probing; explicit `Ynca.ConnectAsync()` remains necessary for ongoing notifications.
- Without Extended Control, firmware/API version, supported inputs, sound programs, scene counts, dB range and speaker pattern count remain unknown. No fallback profile invents these values.
- Phase 5.2 was exercised on the RX-A3060: ExtendedControl, LegacyXml and YNCA were all reported `Available`; four zones and two patterns were detected. This validates the reference model, **not** other models.
- Phase 5.3 adds optional bounded `desc.xml` / `UnitDesc.xml` GET metadata and descriptive native function routes. The RX-A3060 console run confirmed 69 routes and an unavailable/unverified descriptor. Its absence or incompleteness does not establish a missing function.
- Model-specific profile resolution remains future work; the RX-A3060's descriptor was observed to omit working speaker paths.

## Capability terminology

The current shared models distinguish `Unknown`, `Advertised`, `ReadVerified`, `WriteVerified`, `KnownUnsupported`, and `TemporarilyUnavailable` capability status. Evidence sources include `DeviceReported`, `ReadProbe`, `VerifiedHardwareWrite`, `KnownProfile`, `BackupInspection`, and `Inferred`.

Read, write, execute, and observe permissions are tracked independently. A missing or unverified permission remains **unknown**, not `false`; a documented endpoint or backup field is not sufficient evidence for a successful device write.

## Receiver observations

| Model / firmware | Feature or request | Evidence | Observation |
| --- | --- | --- | --- |
| RX-A3060 / 2.86 | Extended Control `getDeviceInfo`, `getFeatures`, `getFuncStatus` | Read verified | Successful historical GET responses; API 2.08 |
| RX-A3060 / 2.86 | Speaker pattern count | Device reported | `speaker_pattern_num = 2` |
| RX-A3060 / 2.86 | Active speaker pattern | Read verified | Pattern 1, via Extended Control and legacy XML |
| RX-A3060 / 2.86 | Pattern 1 and Pattern 2 `Lvl` block GET | Read verified | 13 native channel entries per pattern |
| RX-A3060 / 2.86 | Pattern 1 and Pattern 2 `Distance` block GET | Read verified | Distance data including metre/foot fields |
| RX-A3060 / 2.86 | Pattern 1, `Front_L` level PUT | Write verified | `0.0 → +0.5 → 0.0 dB`, confirmed by readback and restoration |
| RX-A3060 / 2.86 | Pattern 1, `Front_L` distance PUT | Write verified | `2.95 → 3.00 → 2.95 m`, confirmed by readback and restoration |
| RX-A3060 / 2.86 | Pattern 2 PUT while another pattern is active | Unknown | Only reading Pattern 2 has been verified |
| RX-A3060 / 2.86 | Speaker-pattern switching | Not live verified | `setSpeakerPattern` is documented/device-advertised, but a live switch has not been verified |
| RX-A3060 / 2.86 | GET of entire `Speaker_Preout` or `Pattern_1` node | Specific requests rejected | `RC="2"` for these requests; no general conclusion about child paths |
| RX-A3060 / 2.86 | GET of entire Pattern 1/2 `Config` or Pattern 1 `PEQ` | Specific requests rejected | `RC="2"`; individual subpaths have not been verified |
| RX-A3060 / 2.86 | Individual `Lvl/Front_L` or `Distance/Meter/Front_L` GET | Specific requests rejected | HTTP 400; container GET remains valid |
| Other Yamaha models / firmware | The above speaker GET/PUT paths | Unknown | Similar protocol families do **not** establish feature compatibility |

A rejected request is not proof that an entire feature is unsupported. For example, an XML leaf may be writable even when it is not a valid standalone GET target.

## Additional RX-A3060 device reports

- Four zones were reported: `main`, `zone2`, `zone3`, `zone4`. This is **not** the speaker pattern count.
- Scene counts were reported as 12 for the main zone and 4 each for zones 2–4. Scenes and tuner/NETUSB presets are **not** speaker patterns.
- Main-zone `actual_volume_db` information reported an approximate range of **−80.5 to +16.5 dB** in **0.5 dB** steps. Its numeric `volume` scale is different and must not be mixed with dB.
- The receiver's `desc.xml` did not enumerate every working `Speaker_Preout` path, so an omitted description entry does not by itself prove lack of support.

### Implemented features vs. hardware evidence

| Area | Implemented in the C# library | Physical receiver validated with this build |
| --- | --- | --- |
| Extended Control device information (GET) | Yes | Yes: RX-A3060 firmware 2.86 |
| Extended Control feature lists, zones, scene counts and ranges (GET) | Yes | Yes: four zones, scenes and speaker patterns reported |
| Extended Control function status (GET) | Yes | Yes: active pattern 1 |
| Extended Control writes / speaker-pattern switch | No | No |
| Legacy XML active speaker pattern (GET) | Yes | Yes: active pattern 1 |
| Legacy XML speaker PUT | Yes, limited opt-in for RX-A3060 2.86 / Pattern 1 / Front_L only | Not yet using this C# writer (previous controlled hardware writes verified) |
| Legacy XML speaker levels and distances (aggregate GET) | Yes | Yes: 13 channels for both Pattern 1 and 2 |
| YNCA TCP GETs and unsolicited status notifications | Yes (Phase 4.1) | Yes: main-zone GET observations on RX-A3060 |
| Typed YNCA zone PUT with readback | Yes, manual opt-in (Phase 4.2) | User-reported GUI success on RX-A3060; per-zone details unavailable |
| Optional read-only polling / reconnect on subsequent YNCA GET | Yes (Phase 5.6, explicit Start) | Not yet tested with the Phase 5.6 build |
| Automatic monitoring at library startup / dedicated keepalive | No | No |

See [Protocol reference](PROTOCOLS.md) and [Speaker setup](SPEAKER_SETUP.md) for request formats and channel semantics.


### RX-A3060 YNCA observation (2026-10-16)

An interactive C# console run on an RX-A3060 (firmware 2.86) successfully opened
TCP/50000 and observed `SYS:MODELNAME=RX-A3060`, `MAIN:PWR=On`,
`MAIN:VOL=-36.5`, `MAIN:MUTE=Off`, and `MAIN:INP=AUDIO1`.
A subsequent explicit `MAIN:VOL` query returned `-36.5` again. These are
**read observations**, not evidence that the new Phase 4.2 C# PUT path
works or that any Zone 2/3/4 YNCA commands work. All YNCA zone writes
remain **not hardware verified** until separately checked.

### Phase 5.1 sound and scene evidence

| Operation | Implementation | RX-A3060 live evidence for this C# code |
| --- | --- | --- |
| Extended `getStatus` sound/DSP fields | Read-only API | Confirmed by RX-A3060 console output: Main `straight`, `pure_direct=Off`, `enhancer=Off`, `dolby_surround` decoder; zone-specific partial/unknown fields |
| Advertised sound program IDs and zone Scene counts | Extended `getFeatures` | Confirmed by RX-A3060 console output: 27 Main programs, 12 Main scenes; 4 scenes in each secondary zone |
| `setSoundProgram` with native IDs and status readback | Opt-in, per-zone validated | Not yet exercised with this implementation |
| YNCA MAIN Straight / Pure Direct / Enhancer writes | Opt-in, YNCA readback | Not yet exercised with this implementation |
| Extended `recallScene` | Opt-in, accepts only advertised zones/scenes | Not yet exercised; receiver response cannot prove full scene execution |

Live RX-A3060 YNCA readback also observed `MAIN:SOUNDPRG=The Bottom Line`, `MAIN:STRAIGHT=On`, `MAIN:PUREDIRMODE=Off`, and `MAIN:ENHANCER=Off` while power was `Standby`. Extended `program=straight` and YNCA `SOUNDPRG=The Bottom Line` must not be conflated: active playback mode and stored DSP program are distinct concepts. This new evidence predates the multi-protocol-discovery refactor, which still requires validation.

Read-only discovery does not authorize write probing, and user-reported success
for prior Power/Volume/Mute/Input control is not evidence for the new sound paths.

### Phase 5.3 RX-A3060 run and Phase 5.4 implementation

The user ran the Phase 5.3 console against RX-A3060 firmware 2.86 and observed
69 advertised/verified native routes (65 Extended Control entries and 4 YNCA
zone-power read observations). All three enabled protocols were available.
Both `/YamahaRemoteControl/desc.xml` and optional descriptor discovery remain
unverified or unavailable; no inference is made about working legacy speaker
paths.

Phase 5.4 extends the route catalog to **known library commands**, additional
runtime YNCA GETs, Extended Control sound status reads, and successful legacy
speaker block reads. It separately exposes implemented operations and verified
permissions. Writes remain disabled by default; successful controller readbacks
or accepted scene actions are recorded for the *current client instance* only.
The historical limited RX-A3060 speaker write evidence does not authorize writes
to any other model/firmware/pattern/channel. Phase 5.4 capability routing was exercised on the RX-A3060 FW 2.86:
142 function routes were displayed, along with successful YNCA and
Legacy XML reads. A complete Windows build of Phase 5.5 remains pending.


## Phase 5.5 evidence

The RX-A3060 FW 2.86 has advertised `main.signal_info` and a
`surr_decoder_type_list` in its Extended Control features. The new
`getSignalInfo` and surround decoder write methods have **not** yet been
executed on that receiver; other models are not implied to support them.
YNCA `3DCINEMA`/`ADAPTIVEDSP` reads are optional and their availability is
unknown until the specific unit responds. The rest of the multi-protocol
discovery and speaker-safety constraints are unchanged.

### Phase 5.5 hardware confirmation and Phase 5.6 monitoring

On the RX-A3060 (firmware 2.86), Phase 5.5 read-only console observations
confirmed `MAIN:3DCINEMA=Auto` and `MAIN:ADAPTIVEDSP=Auto`, with stored
`MAIN:SOUNDPRG=Sci-Fi`, Straight Off, Pure Direct Off and Enhancer Off.
Extended Control `getSignalInfo` returned `response_code=0`, `audio.error=0`,
but an empty `audio.format` and `audio.fs` while no signal was playing.
These fields should remain **unknown**, not inferred from prior DSP settings.

Phase 5.6 implements explicit read-only HTTP/YNCA monitoring, read-only zone
function filtering using the receiver's feature list, independent per-zone
staleness and caller-facing state/poll events. The new monitoring behavior has
not yet been verified against a physical receiver or compiled on Windows.
Other Yamaha models are not automatically considered compatible solely because
they share a protocol family. Read-only polling grants no write permissions.
