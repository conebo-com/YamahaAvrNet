using System;
using System.Collections.Generic;

namespace YamahaAvrNet.TesterGui
{
    /// <summary>
    /// UI-only labels. Native command identifiers and diagnostic output stay in English.
    /// No dependency on the core library or the future AvrDispatcher.
    /// </summary>
    internal static class GuiText
    {
        private static readonly IDictionary<string, string> German = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "Yamaha AVR Tester", "Yamaha AVR Tester" },
            { "Host:", "Host:" },
            { "HTTP port:", "HTTP-Port:" },
            { "Connect", "Verbinden" },
            { "Refresh", "Aktualisieren" },
            { "Disconnect", "Trennen" },
            { "Receiver web page", "AVR-Webseite öffnen" },
            { "Language:", "Sprache:" },
            { "YNCA TCP port:", "YNCA-TCP-Port:" },
            { "Connect YNCA", "YNCA verbinden" },
            { "Disconnect YNCA", "YNCA trennen" },
            { "Not connected", "Nicht verbunden" },
            { "Result", "Ergebnis" },
            { "HTTP output (operation summaries)", "HTTP-Ausgabe (Befehlsübersicht)" },
            { "YNCA / TCP output", "YNCA-/TCP-Ausgabe" },
            { "Speaker pattern number:", "Lautsprecher-Pattern:" },
            { "Target value (e.g. -36.5, On, AUDIO1):", "Zielwert (z. B. -36.5, On, AUDIO1):" },
            { "Zone ID (main, zone2, zone3, zone4):", "Zonen-ID (main, zone2, zone3, zone4):" },
            { "YNCA subunit (e.g. MAIN):", "YNCA-Subunit (z. B. MAIN):" },
            { "YNCA function (e.g. VOL):", "YNCA-Funktion (z. B. VOL):" },
            { "Enable limited RX-A3060 speaker writes (reconnect required)", "Begrenzte RX-A3060-Lautsprecher-Schreibzugriffe (neu verbinden)" },
            { "Enable YNCA zone writes (reconnect required)", "YNCA-Zonen schreiben (neu verbinden)" },
            { "Enable sound / scene writes (reconnect required)", "Sound / Scenes schreiben (neu verbinden)" },
            { "Send", "Senden" },
            { "Clear outputs before each operation", "Ausgaben vor jedem Befehl löschen" },
            { "Clear outputs", "Ausgaben löschen" },
            { "Compatibility report...", "Kompatibilitätsbericht..." },
            { "Compatibility report: review a local, read-only discovery snapshot. Nothing is uploaded automatically.", "Kompatibilitätsbericht: Lokale, lesende Momentaufnahme prüfen. Es wird nichts automatisch übertragen." },
            { "Overview", "Übersicht" },
            { "Monitoring", "Überwachung" },
            { "Speaker Setup", "Lautsprecher-Setup" },
            { "YNCA / TCP", "YNCA / TCP" },
            { "YNCA / Zones", "YNCA / Zonen" },
            { "Sound / DSP", "Sound / DSP" },
            { "Scenes", "Scenes" },
            { "Receiver summary", "Receiver-Zusammenfassung" },
            { "Read-only integration overview", "Lesende Integrationsübersicht" },
            { "Protocol discovery results", "Protokollerkennung" },
            { "Refresh function status", "Funktionsstatus aktualisieren" },
            { "Available zones and sources", "Zonen und Eingänge" },
            { "Capabilities and evidence", "Funktionen und Nachweise" },
            { "Function routes and legacy description", "Funktionsrouten und Legacy-Beschreibung" },
            { "Start read-only monitoring (30 s)", "Lesende Überwachung starten (30 s)" },
            { "Stop read-only monitoring", "Überwachung stoppen" },
            { "Show receiver state snapshot", "Receiver-Zustand anzeigen" },
            { "Read active speaker pattern", "Aktives Lautsprecher-Pattern lesen" },
            { "Read all advertised patterns", "Gemeldete Patterns anzeigen" },
            { "Read speaker levels", "Lautsprecherpegel lesen" },
            { "Read speaker distances", "Lautsprecherabstände lesen" },
            { "Set Front_L level (verified)", "Front_L-Pegel setzen (begrenzt)" },
            { "Set Front_L distance (verified)", "Front_L-Abstand setzen (begrenzt)" },
            { "Read main-zone status (PWR/VOL/MUTE/INP)", "Main-Zonenstatus (PWR/VOL/MUTE/INP)" },
            { "Read receiver model (SYS/MODELNAME)", "Receivermodell (SYS/MODELNAME)" },
            { "Read custom YNCA function", "Freie YNCA-Funktion lesen" },
            { "Refresh zone status", "Zonenstatus aktualisieren" },
            { "Show observed zone state cache", "Beobachtete Zonenwerte anzeigen" },
            { "Set zone power (On/Standby)", "Zonen-Power setzen (On/Standby)" },
            { "Set zone mute (On/Off)", "Zonen-Mute setzen (On/Off)" },
            { "Set zone volume (dB)", "Zonenlautstärke setzen (dB)" },
            { "Set zone input (native ID)", "Zoneneingang setzen (native ID)" },
            { "Read YNCA Main DSP status", "YNCA-Main-DSP-Status lesen" },
            { "Read zone sound status", "Zonen-Soundstatus lesen" },
            { "Read audio signal information", "Audio-Signalinformationen lesen" },
            { "Read extended YNCA MAIN DSP", "Erweiterten YNCA-Main-DSP lesen" },
            { "Show available decoder types", "Verfügbare Decoder anzeigen" },
            { "Set surround decoder (native ID)", "Surround-Decoder setzen (native ID)" },
            { "Show available sound programs", "Verfügbare Soundprogramme" },
            { "Set sound program (native ID)", "Soundprogramm setzen (native ID)" },
            { "Set Pure Direct (On/Off; MAIN)", "Pure Direct setzen (On/Off; MAIN)" },
            { "Set Straight (On/Off; MAIN)", "Straight setzen (On/Off; MAIN)" },
            { "Set Enhancer (On/Off; MAIN)", "Enhancer setzen (On/Off; MAIN)" },
            { "Show available scene numbers", "Verfügbare Scene-Nummern" },
            { "Recall scene by number", "Scene nach Nummer aufrufen" },
            { "Select a command.", "Befehl auswählen." }
        };

        internal static string Get(string english, bool useGerman)
        {
            if (!useGerman || english == null) return english;
            string translated;
            return German.TryGetValue(english, out translated) ? translated : english;
        }
    }
}
