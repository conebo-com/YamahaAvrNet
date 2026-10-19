using System.Globalization;

namespace YamahaAvrNet.TesterGui
{
    internal static class Loc
    {
        private static readonly Dictionary<string, string> German = new()
        {
            // MainWindow
            ["Yamaha AVR Tester"] = "Yamaha AVR Tester",
            ["Host:"] = "Host:",
            ["HTTP port:"] = "HTTP-Port:",
            ["Connect"] = "Verbinden",
            ["Refresh"] = "Aktualisieren",
            ["Disconnect"] = "Trennen",
            ["Receiver web page"] = "AVR-Webseite öffnen",
            ["Language:"] = "Sprache:",
            ["YNCA TCP port:"] = "YNCA-TCP-Port:",
            ["Connect YNCA"] = "YNCA verbinden",
            ["Disconnect YNCA"] = "YNCA trennen",
            ["Not connected"] = "Nicht verbunden",
            ["Connected: {0}"] = "Verbunden: {0}",
            ["Result"] = "Ergebnis",
            ["HTTP output (operation summaries)"] = "HTTP-Ausgabe (Befehlsübersicht)",
            ["YNCA / TCP output"] = "YNCA-/TCP-Ausgabe",
            ["Speaker pattern number:"] = "Lautsprecher-Pattern:",
            ["Target value (e.g. -36.5, On, AUDIO1):"] = "Zielwert (z. B. -36.5, On, AUDIO1):",
            ["Zone ID (main, zone2, zone3, zone4):"] = "Zonen-ID (main, zone2, zone3, zone4):",
            ["YNCA subunit (e.g. MAIN):"] = "YNCA-Subunit (z. B. MAIN):",
            ["YNCA function (e.g. VOL):"] = "YNCA-Funktion (z. B. VOL):",
            ["Enable limited RX-A3060 speaker writes (reconnect required)"] = "Begrenzte RX-A3060-Lautsprecher-Schreibzugriffe (neu verbinden)",
            ["Enable YNCA zone writes (reconnect required)"] = "YNCA-Zonen schreiben (neu verbinden)",
            ["Enable sound / scene writes (reconnect required)"] = "Sound / Scenes schreiben (neu verbinden)",
            ["Send"] = "Senden",
            ["Clear outputs before each operation"] = "Ausgaben vor jedem Befehl löschen",
            ["Clear outputs"] = "Ausgaben löschen",
            ["Check receiver compatibility..."] = "Receiver-Kompatibilität prüfen...",
            ["Help the project: check whether your receiver is compatible with the functions offered here. Only read-only operations are run against your receiver, and you can send the result to the project by e-mail. Nothing is sent automatically and nothing is changed on the receiver."] = "Unterstützen Sie das Projekt: Prüfen Sie, ob Ihr Receiver mit den hier angebotenen Funktionen kompatibel ist. Dazu werden nur reine Leseoperationen gegen Ihren Receiver ausgeführt, und das Ergebnis können Sie dem Projekt per E-Mail schicken. Es wird nichts automatisch gesendet und am Receiver nichts verändert.",
            ["unknown"] = "unbekannt",
            ["Compatibility mode: AVR reports '{0}', using profile '{1}'"] = "Kompatibilitätsmodus: Der AVR meldet '{0}', verwendet wird Profil '{1}'",

            // SupportReportWindow
            ["Check receiver compatibility"] = "Receiver-Kompatibilität prüfen",
            ["Check whether your receiver works with the functions of this tester and help the project with the result.\n1. Choose the report type below. 'Anonymized' leaves out your MAC address and host.\n2. Click 'Run read-only operations'. Values are only read; nothing is changed on your receiver.\n3. Check the results in the list: green = ok, yellow = warning, red = error.\n4. Click 'Prepare for sending', copy the report and paste it into an e-mail yourself. Nothing is sent automatically."] = "Prüfen Sie, ob Ihr Receiver mit den Funktionen dieses Testers funktioniert, und helfen Sie dem Projekt mit dem Ergebnis.\n1. Wählen Sie unten den Berichtstyp. 'Anonymisiert' lässt Ihre MAC-Adresse und den Host weg.\n2. Klicken Sie auf 'Lese-Operationen ausführen'. Es werden nur Werte gelesen; an Ihrem Receiver wird nichts verändert.\n3. Prüfen Sie die Ergebnisse in der Liste: grün = ok, gelb = Warnung, rot = Fehler.\n4. Klicken Sie auf 'Zum Senden vorbereiten', kopieren Sie den Bericht und fügen Sie ihn selbst in eine E-Mail ein. Es wird nichts automatisch gesendet.",
            ["Report type:"] = "Berichtstyp:",
            ["Regular"] = "Normal",
            ["Anonymized (no MAC address, serial number or host)"] = "Anonymisiert (ohne MAC-Adresse, Seriennummer oder Host)",
            ["Run read-only operations"] = "Lese-Operationen ausführen",
            ["Result of the selected operation"] = "Ergebnis der gewählten Operation",
            ["Individual information (what ever you want to tell us ;-)"] = "Individuelle Angaben (alles, was Sie uns mitteilen möchten ;-)",
            ["The project may contact me by e-mail if there are questions about this report"] = "Das Projekt darf mich per E-Mail kontaktieren, wenn es Fragen zu diesem Bericht gibt",
            ["Keep me informed by e-mail about changes to the project"] = "Informieren Sie mich per E-Mail über Änderungen am Projekt",
            ["Prepare for sending"] = "Zum Senden vorbereiten",
            ["Running '{0}' ..."] = "'{0}' wird ausgeführt ...",
            ["Done: {0} operations ({1} ok, {2} warnings, {3} errors)."] = "Fertig: {0} Operationen ({1} ok, {2} Warnungen, {3} Fehler).",

            // JsonPreviewWindow
            ["Check report before sending"] = "Bericht vor dem Senden prüfen",
            [$"This is the report for the compatibility check of your receiver. Please read it and make sure you are happy with its content.\n1. Click 'Copy to clipboard'.\n2. Paste the report into an e-mail to {MainWindow.EMail} and send it yourself.\nNothing is sent automatically."] =
                $"Dies ist der Bericht zur Kompatibilitätsprüfung Ihres Receivers. Bitte lesen Sie ihn durch und stellen Sie sicher, dass Sie mit dem Inhalt einverstanden sind.\n1. Klicken Sie auf 'In die Zwischenablage kopieren'.\n2. Fügen Sie den Bericht in eine E-Mail an {MainWindow.EMail} ein und senden Sie sie selbst ab.\nEs wird nichts automatisch gesendet.",
            ["Copy to clipboard"] = "In die Zwischenablage kopieren",
            ["Save as TXT..."] = "Als TXT speichern...",
            ["Save report"] = "Bericht speichern",
            ["Close"] = "Schließen",
            ["Please send this report to us by e-mail: {0}"] = "Bitte senden Sie uns diesen Bericht per E-Mail: {0}",
            ["This supports the open source project. Thank you very much!"] = "Damit unterstützen Sie das Open-Source-Projekt. Vielen Dank!",
            ["Copied to the clipboard."] = "In die Zwischenablage kopiert.",
            ["The clipboard is currently not available: {0}"] = "Die Zwischenablage ist derzeit nicht verfügbar: {0}",
            ["Please try again, or select the text and copy it with Ctrl+C."] = "Bitte versuchen Sie es erneut oder markieren Sie den Text und kopieren Sie ihn mit Strg+C.",

            // ReceiverProfileDialog
            ["Receiver profile"] = "Receiver-Profil",
            ["Profile to use for the operations of this receiver:"] = "Profil, das für die Operationen dieses Receivers verwendet werden soll:",
            ["The receiver did not report a type name. Please select the most similar type from the list and enter the type of your receiver below."] = "Der Receiver hat keinen Typnamen gemeldet. Bitte wählen Sie in der Liste den ähnlichsten Typ aus und tragen Sie unten den Typ Ihres Receivers ein.",
            ["The receiver reports the type '{0}', but it is not available in this tester. Please select the most similar type from the list."] = "Der Receiver meldet den Typ '{0}', dieser ist in diesem Tester aber nicht vorhanden. Bitte wählen Sie in der Liste den ähnlichsten Typ aus.",
            ["Type of your receiver:"] = "Typ Ihres Receivers:",
            ["<select a profile>"] = "<Profil auswählen>",
            ["Accept"] = "Übernehmen",
            ["Cancel"] = "Abbrechen",

            // Command groups
            ["Overview"] = "Übersicht",
            ["Monitoring"] = "Überwachung",
            ["Speaker Setup"] = "Lautsprecher-Setup",
            ["YNCA / TCP"] = "YNCA / TCP",
            ["YNCA / Zones"] = "YNCA / Zonen",
            ["Sound / DSP"] = "Sound / DSP",
            ["Scenes"] = "Scenes",

            // Command names
            ["Receiver summary"] = "Receiver-Zusammenfassung",
            ["Read-only integration overview"] = "Lesende Integrationsübersicht",
            ["Protocol discovery results"] = "Protokollerkennung",
            ["Refresh function status"] = "Funktionsstatus aktualisieren",
            ["Available zones and sources"] = "Zonen und Eingänge",
            ["Capabilities and evidence"] = "Funktionen und Nachweise",
            ["Function routes and legacy description"] = "Funktionsrouten und Legacy-Beschreibung",
            ["Start read-only monitoring (30 s)"] = "Lesende Überwachung starten (30 s)",
            ["Stop read-only monitoring"] = "Überwachung stoppen",
            ["Show receiver state snapshot"] = "Receiver-Zustand anzeigen",
            ["Read active speaker pattern"] = "Aktives Lautsprecher-Pattern lesen",
            ["Read all advertised patterns"] = "Gemeldete Patterns anzeigen",
            ["Read speaker levels"] = "Lautsprecherpegel lesen",
            ["Read speaker distances"] = "Lautsprecherabstände lesen",
            ["Set Front_L level (verified)"] = "Front_L-Pegel setzen (begrenzt)",
            ["Set Front_L distance (verified)"] = "Front_L-Abstand setzen (begrenzt)",
            ["Read main-zone status (PWR/VOL/MUTE/INP)"] = "Main-Zonenstatus (PWR/VOL/MUTE/INP)",
            ["Read receiver model (SYS/MODELNAME)"] = "Receivermodell (SYS/MODELNAME)",
            ["Read custom YNCA function"] = "Freie YNCA-Funktion lesen",
            ["Refresh zone status"] = "Zonenstatus aktualisieren",
            ["Show observed zone state cache"] = "Beobachtete Zonenwerte anzeigen",
            ["Set zone power (On/Standby)"] = "Zonen-Power setzen (On/Standby)",
            ["Set zone mute (On/Off)"] = "Zonen-Mute setzen (On/Off)",
            ["Set zone volume (dB)"] = "Zonenlautstärke setzen (dB)",
            ["Set zone input (native ID)"] = "Zoneneingang setzen (native ID)",
            ["Read YNCA Main DSP status"] = "YNCA-Main-DSP-Status lesen",
            ["Read zone sound status"] = "Zonen-Soundstatus lesen",
            ["Read audio signal information"] = "Audio-Signalinformationen lesen",
            ["Read extended YNCA MAIN DSP"] = "Erweiterten YNCA-Main-DSP lesen",
            ["Show available decoder types"] = "Verfügbare Decoder anzeigen",
            ["Set surround decoder (native ID)"] = "Surround-Decoder setzen (native ID)",
            ["Show available sound programs"] = "Verfügbare Soundprogramme",
            ["Set sound program (native ID)"] = "Soundprogramm setzen (native ID)",
            ["Set Pure Direct (On/Off; MAIN)"] = "Pure Direct setzen (On/Off; MAIN)",
            ["Set Straight (On/Off; MAIN)"] = "Straight setzen (On/Off; MAIN)",
            ["Set Enhancer (On/Off; MAIN)"] = "Enhancer setzen (On/Off; MAIN)",
            ["Show available scene numbers"] = "Verfügbare Scene-Nummern",
            ["Recall scene by number"] = "Scene nach Nummer aufrufen"
        };

        static Loc()
        {
            IsGerman = string.Equals(
                CultureInfo.CurrentUICulture.TwoLetterISOLanguageName,
                "de",
                StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsGerman { get; set; }

        public static string Get(string englishText) =>
            IsGerman && German.TryGetValue(englishText, out var germanText) ? germanText : englishText;

        public static string Format(string englishFormat, params object?[] arguments) =>
            string.Format(CultureInfo.InvariantCulture, Get(englishFormat), arguments);
    }
}
