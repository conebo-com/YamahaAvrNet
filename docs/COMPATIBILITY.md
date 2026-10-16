# Device compatibility and evidence

This document distinguishes **observed receiver behavior** from **features currently implemented by YamahaAvrNet**. Historical observations were made on a **Yamaha RX-A3060, firmware 2.86, Extended Control API 2.08**. They must not be generalized to other models or firmware versions.

**Implementation status:** The C# library now implements the read-only Extended Control `getDeviceInfo`, `getFeatures`, and `getFuncStatus` operations. Legacy XML speaker GET/PUT and YNCA are **not implemented**. This build has not yet been verified against a physical receiver; historical hardware evidence below is separate from the implementation status.

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
| Extended Control device information (GET) | Yes | Not yet |
| Extended Control feature lists, zones, scene counts and ranges (GET) | Yes | Not yet |
| Extended Control function status (GET) | Yes | Not yet |
| Extended Control writes / speaker-pattern switch | No | No |
| Legacy XML speaker levels and distances | No | No (historical RX-A3060 evidence above) |
| YNCA state monitoring and commands | No | No |

See [Protocol reference](PROTOCOLS.md) and [Speaker setup](SPEAKER_SETUP.md) for request formats and channel semantics.
