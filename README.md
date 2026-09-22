# Hora

Hora zeigt die eigenen Zeitbuchungen (Worklogs) aus Jira Cloud pro **Tag**, pro **Woche** und pro
**Monat** an – mit Summen, Sollstunden, Differenz und Fortschritt sowie Zwischensummen pro Ticket.
Native Windows-App (WPF, .NET 8), heller und dunkler Modus – wahlweise dem Windows-Modus folgend.

## Für Nutzerinnen und Nutzer

1. `Hora.exe` an einen beliebigen Ort kopieren und starten. Es wird nichts installiert.
2. Beim ersten Start anmelden:
   - **Jira-URL**: `https://advisori-ftc.atlassian.net` (vorbelegt)
   - **E-Mail**: die eigene Atlassian-E-Mail
   - **API-Token**: über den Link „API-Token erstellen“ bei Atlassian anlegen
     (<https://id.atlassian.com/manage-profile/security/api-tokens>) und einfügen.
     Ein Passwort funktioniert nicht.
   - „Angemeldet bleiben“ speichert den Token verschlüsselt (Windows DPAPI) für den eigenen
     Windows-Benutzer auf diesem Rechner.
3. Die Übersicht zeigt standardmäßig den heutigen Tag.
   - Mit `◄` und `►` blättern: im Tagesmodus um einen Tag, im Wochenmodus um sieben Tage, im
     Monatsmodus um einen Monat.
   - Der Segmentschalter „Monat | Woche | Tag“ rechts oben wechselt die Ansicht.
   - „Heute“ springt zurück auf den aktuellen Tag, „Aktualisieren“ holt die Buchungen neu von Jira.
   - Der grüne Kreis mit den Initialen ganz rechts öffnet die **Profilseite** (siehe unten).
     Abgemeldet wird dort.
4. Klick auf einen Ticket-Schlüssel öffnet das Ticket im Browser. Jedes Ticket hat eine feste
   Farbe, die aus seinem Schlüssel abgeleitet wird – dasselbe Ticket sieht in allen Ansichten
   gleich aus.

### Die drei Ansichten

| Ansicht | Inhalt |
|---|---|
| **Tag** | Eine Zeile je Buchung: Start bis Ende, Ticket, Titel, Dauer. Ein vorhandener Kommentar steht als graue Unterzeile darunter. Lange Texte sind abgeschnitten; der Mauszeiger zeigt den vollen Text. |
| **Woche** | Sieben Tageszeilen mit Wochentag, Datum, Fortschrittsbalken gegen das Tagessoll und Tagessumme. Der heutige Tag ist fett und trägt links einen grünen Streifen. Ein Klick auf eine Zeile wechselt in die Tagesansicht dieses Tages. |
| **Monat** | Kalendergitter: links die Kalenderwoche mit Wochensumme, dann die sieben Wochentage. Jede Zelle zeigt Tageszahl, Tagessumme und einen schmalen Balken gegen das Tagessoll. Der heutige Tag ist grün umrandet, Tage außerhalb des Monats sind blass und nicht anklickbar. Ein Klick auf eine Zelle wechselt in die Tagesansicht dieses Tages. |

Über allen drei Ansichten steht die Kennzahlenkarte mit **ERFASST**, **SOLL** und **DIFFERENZ**
für den gerade angezeigten Zeitraum sowie rechts ein Fortschrittsbalken mit Prozentwert. Eine
negative Differenz steht in Rot. Ist das Soll null (zum Beispiel an einem Wochenende), zeigt der
Prozentwert „–“.

Unter der jeweiligen Ansicht steht der Abschnitt **PRO TICKET** in zwei Spalten, sortiert nach
Gesamtdauer absteigend. Gibt es im Zeitraum keine Buchungen, entfällt der Abschnitt.

### Die Profilseite

Ein Klick auf den Avatar rechts oben ersetzt die Übersicht durch die Profilseite. Der Zurück-Pfeil
links oben und „Fertig“ rechts oben führen zurück; die Übersicht steht danach unverändert da, ohne
neuen Abruf bei Jira.

Oben stehen Name, E-Mail-Adresse und – falls eingetragen – die Rolle als Abzeichen. Darunter vier
Abschnitte:

| Abschnitt | Einstellung | Wirkung |
|---|---|---|
| **DARSTELLUNG** | Theme „System \| Hell \| Dunkel“ | „System“ folgt der Windows-Einstellung für Apps; „Hell“ und „Dunkel“ setzen den Modus fest. Der Schalter zeigt die **gewählte** Einstellung, nicht das Ergebnis |
| | Startansicht „Monat \| Woche \| Tag“ | welcher Modus beim **nächsten Programmstart** aktiv ist. Standard: Tag |
| | Zeitformat „7h 45m \| 7,75h“ | betrifft **jede** Dauer in der App |
| **ZEITERFASSUNG** | Arbeitstage „Mo Di Mi Do Fr Sa So“ | sieben Umschalter; die Unterzeile nennt die Anzahl, zum Beispiel „5 Tage pro Woche“ |
| | Sollstunden pro Tag | Schrittschalter in halben Stunden, 0 bis 24; gilt für **alle ausgewählten** Arbeitstage |
| | Erinnerung bei fehlender Buchung | merkt sich nur die Wahl, siehe unten |
| **JIRA** | Instanz | die eigene Jira-Adresse; ein Klick öffnet sie im Browser |
| | Letzte Synchronisation | Zeitpunkt des letzten erfolgreichen Abrufs, z. B. „17.09.2026, 09:12“. Gab es noch keinen: „–“ |
| **ÜBER HORA** | Version | die Version der laufenden Fassung, z. B. „1.1.0“ – nützlich, wenn es um einen Fehler geht |

Unten links steht „Abmelden“. Davor fragt die App nach; erst nach „Ja“ wird der gespeicherte Token
gelöscht. Vorausgewählt ist „Nein“, ein Fehlklick kostet also nichts.

**Jede Änderung wirkt sofort und wird sofort gespeichert.** Es gibt keinen Speichern-Knopf;
„Fertig“ und der Zurück-Pfeil navigieren nur.

Zwei Einschränkungen, die man kennen sollte:

- Die **Rolle** liefert Jira nicht. Sie ist ein freies Textfeld in `settings.json` (`role`) und auf
  der Profilseite nur Anzeige, nicht bearbeitbar. Solange sie leer ist, bleibt das Abzeichen
  ausgeblendet.
- Die **Erinnerung** merkt sich ausschließlich die Wahl. Eine tatsächliche Benachrichtigung baut die
  App bewusst **nicht**: Sie liefe ohnehin nur, solange Hora offen ist. Die Unterzeile „Werktags um
  17:30 Uhr“ beschreibt, was eine spätere Erinnerung täte.

### Zeitformat

„7h 45m“ ist die bisherige Schreibweise und der Standard. „7,75h“ zeigt Dezimalstunden mit Komma und
zwei Nachkommastellen; glatte Stunden stehen ohne Nachkommastellen da („8h“, nicht „8,00h“). Die Wahl
gilt für **alle** Dauern: einzelne Buchungen, Tages-, Wochen- und Monatssummen, die Zwischensummen
pro Ticket, die Kennzahlen ERFASST/SOLL/DIFFERENZ und den Wert im Schrittschalter der Profilseite.

### Dunkelmodus

Der Segmentschalter „Theme“ auf der Profilseite kennt drei Möglichkeiten: **System**, **Hell** und
**Dunkel**.

- **System** folgt der Windows-Einstellung für Apps (Einstellungen → Personalisierung → Farben →
  „Standard-App-Modus auswählen“). Stellt man Windows um, während Hora läuft, färbt sich die App
  sofort mit um – **ohne Neustart**. Ist die Einstellung nicht lesbar, gilt hell.
- **Hell** und **Dunkel** legen den Modus fest; Windows wird dann nicht mehr beachtet.

Das Design wird ausschließlich hier gewählt. Die frühere Umschalt-Pille oben rechts in der
Fensterleiste ist entfallen: Sie kannte nur zwei Zustände und verließ damit bei jedem Klick die
Wahl „System“.

Gespeichert wird in `settings.json` die **Wahl** (`"System"`, `"Light"` oder `"Dark"`), nicht das
Ergebnis; sie wird beim nächsten Start wiederhergestellt. Standard ist hell.

### Sollstunden und Arbeitstage

Die Sollstunden stehen für jeden Wochentag einzeln in `settings.json` und lassen sich dort ändern.
Standard sind Montag bis Freitag je 8 Stunden, Samstag und Sonntag null.

Auf der Profilseite bestimmt die Zeile **Arbeitstage** mit sieben Umschaltern „Mo Di Mi Do Fr Sa So“,
welche Tage zur Arbeitswoche gehören; die Unterzeile nennt die Anzahl („5 Tage pro Woche“, bei genau
einem Tag „1 Tag pro Woche“). Als Arbeitstag gilt jeder Tag, dessen Sollzeit größer null ist –
Samstag und Sonntag also ebenso, wenn man sie auswählt.

- Wird ein Tag **ausgewählt**, bekommt er den Wert, der gerade im Schrittschalter steht.
- Wird ein Tag **abgewählt**, wird seine Sollzeit null.
- Sind **alle** Tage abgewählt, ist das Wochensoll null. Die Kennzahlenkarte zeigt dann „–“ als
  Prozentwert; SOLL steht auf `0h` und DIFFERENZ zeigt die erfasste Zeit als Überstunden.

Der Schrittschalter „Sollstunden pro Tag“ gilt für **alle ausgewählten** Arbeitstage gleichzeitig.
Schrittweite ist eine halbe Stunde, der Bereich 0 bis 24.

Wer in `settings.json` **ungleiche** Tage einträgt – etwa freitags nur vier Stunden –, behält sie,
solange er die Bedienelemente nicht benutzt. Die Profilseite zeigt in diesem Fall den Wert des ersten
Arbeitstags, der nicht null ist. Erst ein Druck auf „−“ oder „+“ zieht alle ausgewählten Tage auf
denselben Wert. Für eine ungleiche Woche bleibt die Datei also weiterhin der richtige Ort.

Aus diesen Werten ergeben sich **SOLL** und **DIFFERENZ** in der Kennzahlenkarte sowie die
Fortschrittsbalken in Wochen- und Monatsansicht: Ein Tag ist voll, wenn seine Buchungen das
Tagessoll erreichen. Am Wochenende (Tagessoll null) bemisst sich der Balken der Wochenansicht
stattdessen am größten Tageswert derselben Woche. Feiertage, Urlaub und Krankheit werden **nicht**
berücksichtigt.

Die Datei entsteht nicht schon beim ersten Start, sondern erst bei der ersten erfolgreichen
Anmeldung (oder beim ersten Umschalten des Designs). Bis dahin gelten die Standardwerte. Von Hand
geänderte Werte werden beim nächsten Start übernommen und bleiben auch bei einer erneuten Anmeldung
erhalten: Die Anmeldung schreibt nur `baseUrl` und `email` neu, `schedule` und `theme` rührt sie
nicht an.

Beispiel für `%LOCALAPPDATA%\Hora\settings.json`:

```json
{
  "baseUrl": "https://advisori-ftc.atlassian.net",
  "email": "vorname.nachname@advisori.de",
  "schedule": {
    "monday": 8,
    "tuesday": 8,
    "wednesday": 8,
    "thursday": 8,
    "friday": 8,
    "saturday": 0,
    "sunday": 0
  },
  "theme": "Light",
  "role": "",
  "startView": "Day",
  "timeFormat": "HoursMinutes",
  "reminderEnabled": false,
  "lastSyncAt": null
}
```

- `schedule`: Sollstunden je Wochentag als Dezimalzahl, zum Beispiel `6.5` für sechseinhalb
  Stunden. Negative oder unsinnige Werte zählen als null. Für eine Viertagewoche genügt es,
  `friday` auf `0` zu setzen – oder auf der Profilseite den Umschalter „Fr“ abzuwählen. Welche Tage
  Arbeitstage sind, steht in genau diesen sieben Werten: alles über null zählt.
- `theme`: `"System"`, `"Light"` oder `"Dark"`. Gespeichert wird die Wahl, nicht das Ergebnis:
  Bei `"System"` entscheidet die Windows-Einstellung für Apps, wie die App tatsächlich aussieht.
  Ein unbekannter Wert gilt als hell. Die App schreibt diesen Wert selbst, sobald umgeschaltet wird.
- `role`: freie Rollenbezeichnung für das Abzeichen der Profilseite, zum Beispiel
  `"Senior Consultant"`. Standard ist leer; dann bleibt das Abzeichen ausgeblendet. Jira liefert
  diesen Wert nicht, er lässt sich **nur hier** setzen.
- `startView`: `"Day"`, `"Week"` oder `"Month"` – die Ansicht beim Programmstart. Standard `"Day"`.
- `timeFormat`: `"HoursMinutes"` für „7h 45m“ oder `"DecimalHours"` für „7,75h“.
  Standard `"HoursMinutes"`.
- `reminderEnabled`: `true` oder `false`. Merkt nur die Wahl; eine Benachrichtigung entsteht daraus
  nicht. Standard `false`.
- `lastSyncAt`: Zeitpunkt des letzten erfolgreichen Abrufs, von der App selbst geschrieben. `null`,
  solange es keinen gab.
- `baseUrl` und `email` belegen nur das Login-Formular vor; jede erfolgreiche Anmeldung
  überschreibt diese beiden Werte mit dem, was im Formular stand.

Die fünf zuletzt genannten Felder kamen mit der Profilseite dazu. Eine ältere `settings.json` ohne
sie wird weiterhin gelesen; die fehlenden Felder bekommen dann ihre Standardwerte.

### Dateien der App

Alle Dateien liegen unter `%LOCALAPPDATA%\Hora\`:

| Datei | Inhalt |
|---|---|
| `credentials.dat` | verschlüsselte Zugangsdaten (nur bei „Angemeldet bleiben“) |
| `settings.json` | Jira-URL, E-Mail, Sollstunden (`schedule`), Design (`theme`) und die Einstellungen der Profilseite |
| `hora.log` | Fehlerprotokoll, hilfreich bei Supportfragen. Enthält niemals den Token. |

### Zwei Varianten der Exe

- `dist/self-contained/Hora.exe`: läuft ohne installiertes .NET, rund 64 MB. Die benötigten
  Montserrat-Schnitte sind in die Programmdatei eingebettet, die App sieht deshalb auch auf einem
  Rechner ohne installierte Schriftart gleich aus.
- `dist/framework-dependent/Hora.exe`: rund 1,4 MB, benötigt die .NET 8 Desktop Runtime
  (<https://dotnet.microsoft.com/download/dotnet/8.0>).

Weiterzugeben ist ausschließlich `Hora.exe`. Symboldateien (`.pdb`) entstehen im Release-Build
bewusst nicht mehr; liegen in einem älteren Auslieferungsordner noch welche, gehören sie nicht mit
in die Weitergabe.

## Für Entwicklerinnen und Entwickler

Voraussetzung: .NET SDK 8 (`winget install Microsoft.DotNet.SDK.8`).

```powershell
dotnet build
dotnet test
dotnet run --project src/Hora.App
```

Veröffentlichen:

```powershell
dotnet publish src/Hora.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o dist/self-contained
dotnet publish src/Hora.App -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o dist/framework-dependent
```

Die Versionsnummer steht in `src/Hora.App/Hora.App.csproj` (`<Version>`). Von dort liest
`AppVersion.Current` sie zur Laufzeit; sie erscheint im Fenstertitel und auf der Profilseite unter
„ÜBER HORA“. Beim Erstellen eines Releases wird sie hochgezählt:

| Art der Änderung | Beispiel |
|---|---|
| Fehlerbehebung | 1.2.3 → 1.2.**4** |
| neue Funktion | 1.2.3 → 1.**3**.0 |
| erste Zahl | **nur** auf ausdrückliche Ansage, nie von selbst |

Begonnen wurde bei 1.0.0 (erstes Release mit Anwendungssymbol und bündigen Ticketzeilen).

Projektstruktur:

- `src/Hora.Core`: Jira-Client (REST API v3), Aggregation, Sollstunden, Speicher, Logger. Keine UI.
- `src/Hora.App`: WPF-Oberfläche, MVVM mit CommunityToolkit.Mvvm.
- `tests/Hora.Core.Tests`, `tests/Hora.App.Tests`: xUnit.
- `docs/superpowers/specs`: Design-Spezifikationen. `docs/superpowers/plans`: Implementierungspläne.

Farben und Schrift:

- **Alle** Farbwerte stehen ausschließlich in `src/Hora.App/Styles/Tokens.Light.xaml` und
  `Tokens.Dark.xaml`. Beide Dateien führen dieselben Schlüssel; ein Schlüssel, den nur eine Seite
  kennt, lässt das Umschalten abstürzen (ein Test wacht darüber).
- Im übrigen XAML wird auf Farben ausnahmslos über `DynamicResource` verwiesen – nur so schaltet
  der Dunkelmodus zur Laufzeit um.
- Die Schrift Montserrat liegt als `Resource` unter `src/Hora.App/Styles/Fonts/` und wird über eine
  `pack://`-Uri angesprochen, nicht über den Namen einer installierten Schriftart.

Das Hora-Zeichen gibt es in zwei Fassungen, beide aus derselben Vorlage `assets/hora-icon.svg`:

- `src/Hora.App/Views/HoraLogo.xaml` – Vektor für die Oberfläche (Fensterleiste, Anmeldekarte). Er
  bezieht seine Farben über `DynamicResource` und schaltet deshalb mit dem Design um. Beim Einsetzen
  genügt die Höhe; die Breite folgt dem Seitenverhältnis 15:16.
- `src/Hora.App/Assets/hora.ico` – sieben Größen für Taskleiste, Alt-Tab und Explorer, eingebunden
  über `<ApplicationIcon>` und `Window.Icon`. Eine `.ico` kann nicht mit dem Design umschalten,
  deshalb trägt sie eine helle Kachel: Das Zeichen der Vorlage allein wäre auf einer dunklen
  Taskleiste unsichtbar. Neu erzeugen mit
  `powershell -ExecutionPolicy Bypass -File assets\build-app-icon.ps1` (`-NoTile` für die Fassung
  ohne Kachel).

Zeitzonen: Tagesgrenzen richten sich nach der lokalen Windows-Zeitzone. Weicht die
Jira-Profil-Zeitzone davon ab, steht ein Hinweis in `hora.log`.

## Nicht enthalten

Eine tatsächliche Erinnerung (der Schalter merkt sich nur die Wahl), Bearbeiten der Rolle in der
Oberfläche (nur `settings.json`), **unterschiedliche** Sollstunden je Wochentag über die Oberfläche
(welche Tage Arbeitstage sind, lässt sich dort wählen; wie viele Stunden ein einzelner Tag abweichend
haben soll, nur in `settings.json`), Feiertage, Urlaub, Krankheit, Teilzeitmodelle mit wechselnden
Wochen, Anlegen oder Ändern von Buchungen, Tempo Timesheets, Export, Jahresansicht, mehrere Konten,
Auto-Update, Jira Server/Data Center.
