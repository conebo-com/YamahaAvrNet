# Speaker setup and native Yamaha channel IDs

This reference describes observed **RX-A3060** speaker settings and the intended data semantics of YamahaAvrNet. This version implements read-only speaker communication; it does not imply write support or compatibility with other models.

## Native channels

The following **13 native channel IDs** appeared in successful Pattern 1 and Pattern 2 `Lvl`/`Distance` aggregate GET responses:

```text
Front_L            Front_R            Center
Sur_L              Sur_R              Sur_Back_L
Sur_Back_R         Front_Presence_L   Front_Presence_R
Rear_Presence_L    Rear_Presence_R    Subwoofer_1
Subwoofer_2
```

Native IDs are preserved **verbatim**. They are not fixed array positions, and unfamiliar IDs must remain accessible rather than being discarded. Manufacturer-neutral speaker roles or physical placements are assigned only when the device configuration supports such a mapping.

## Three independent channel states

1. **Addressable:** The receiver exposes a level, distance, or other setting for a native channel.
2. **Configured:** A speaker is actually enabled in the selected pattern's speaker configuration.
3. **Currently active:** The present incoming signal and processing mode are using the channel.

These states must not be conflated. In an inspected RX-A3060 backup, Pattern 1 contained `Sur_Back_L`/`Sur_Back_R` level fields even though its surround-back configuration was `None`.

A channel's **role** is also different from its **physical placement**. For example, `Front_Presence_L` was associated with `Overhead` in one saved pattern and `Front Height` in another. Neither meaning may be hardcoded globally.

## Speaker patterns

The RX-A3060's Extended Control feature response reported **two speaker patterns**, with Pattern 1 active at the time of observation. Aggregate reads of **both** patterns' levels and distances succeeded without switching patterns. Reading an inactive pattern is **not** evidence that writing it is safe or supported.

Speaker patterns are distinct from Yamaha **Scenes**, tuner/NETUSB favorites, and other stored presets. Their counts must be obtained independently.

## Level and distance values

Legacy Yamaha XML represents physical values using `Val`, `Exp`, and `Unit`:

```text
Value = Val / 10^Exp
Level:    Val=5,   Exp=1, Unit=dB  -> +0.5 dB
Distance: Val=295, Exp=2, Unit=m   ->  2.95 m
```

Read the unit and exponent from each actual response. Do not impose the RX-A3060 example's scale on other models. YamahaAvrNet models such values with `decimal` and retains the native representation where provided.

The working GET targets are the **complete** `Lvl` and `Distance` blocks, not individual channel leaves. A targeted PUT on `Pattern_1/Front_L` level and distance was historically verified, including readback and restoration. Other write permissions remain unknown until individually established; in particular, **Pattern 2 writes have not been verified**.

## Configuration and PEQ limitations

A read-only inspection of an RX-A3060 backup found speaker configuration, channel placement, and PEQ data beneath pattern-specific paths. However, GET requests for entire `Config` and `PEQ` nodes were rejected (`RC="2"`). This does not establish whether individual child paths can be read; it certainly does not authorize writing them.

The speaker API now implements `GetActivePatternAsync`, `GetLevelsAsync`, `GetDistancesAsync`, and `GetPatternsAsync`. The latter requires a valid pattern count from Extended Control discovery, then reads each pattern explicitly; it does not brute-force unknown pattern numbers. Channel information marks addressability and observed readable level/distance fields, while configured/active speaker status and write permissions remain unknown. Both `SetLevelAsync` and `SetDistanceAsync` remain disabled and throw `NotSupportedException` **without sending a command**.

Only previously confirmed aggregate GET paths are used. This code has not yet been verified on a connected RX-A3060; the historical receiver evidence remains separate.

See [Protocol reference](PROTOCOLS.md) and [Device compatibility](COMPATIBILITY.md) for observed commands and the exact evidence boundaries.
