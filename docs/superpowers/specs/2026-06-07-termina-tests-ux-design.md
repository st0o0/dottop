# Termina Integration Tests + UX-Verbesserungen

## Ziel

Aufbau einer Integration-Test-Suite mit Termina's VirtualTerminal + VirtualInput, die alle 5 Seiten abdeckt. Parallel dazu gezielte UX-Fixes die beim Testen identifiziert und sofort abgesichert werden.

## Test-Infrastruktur

### Test-Fixture Basis

Jeder Test startet eine echte Termina-App mit:
- **VirtualTerminal** (80x24 oder konfigurierbar) — fängt gerenderten Output ab
- **VirtualInputSource** — simuliert Tastatureingaben
- **Gemockte Actors** — MonitoringSupervisor mit vordefinierten Antworten (kein echtes OS-Monitoring)

### Test-Projekt

```
tests/dottop.UI.Tests/
├── dottop.UI.Tests.csproj
├── Fixtures/
│   └── DottopTestFixture.cs        # Shared Fixture: Host + VirtualTerminal + VirtualInput
├── Helpers/
│   └── ScreenAssert.cs             # Assertion-Helpers für VirtualTerminal
├── ProcessesPageTests.cs
├── ServicesPageTests.cs
├── NetworkPageTests.cs
├── PerformancePageTests.cs
├── SettingsPageTests.cs
└── NavigationTests.cs              # Cross-Page Tab-Navigation
```

### DottopTestFixture

Shared Fixture die den Host aufbaut:

```csharp
public class DottopTestFixture : IAsyncLifetime
{
    public VirtualTerminal Terminal { get; }
    public VirtualInputSource Input { get; }
    private IHost? _host;
    
    // Starten: Host mit VirtualTerminal + VirtualInput + Mocked Actors
    // Stoppen: Input.Complete(), Host shutdown
}
```

**Mocked Actor-Daten:**
- CPU: Feste Werte (ProcessorName="Test CPU", TotalPercent=42.0, 4 Cores)
- Memory: 16 GB total, 8 GB used
- Processes: 5 vordefinierte Prozesse mit unterschiedlichen CPU/RAM/Group-Werten
- Services: 3 Services (Running, Stopped, Running)
- Network: 3 Connections (Established, Listen, TimeWait)
- Disk: 2 Disks (C:, D:)
- GPU: Nicht verfügbar (NoGpuMetrics)

### ScreenAssert Helpers

```csharp
public static class ScreenAssert
{
    // Prüft ob Text irgendwo auf dem Screen vorkommt
    static void Contains(VirtualTerminal term, string text);
    
    // Prüft ob Text auf einer bestimmten Zeile vorkommt
    static void LineContains(VirtualTerminal term, int line, string text);
    
    // Prüft ob die aktive Tab-Anzeige stimmt
    static void ActiveTab(VirtualTerminal term, int tabIndex);
    
    // Wartet bis ein Text auf dem Screen erscheint (mit Timeout)
    static async Task WaitForText(VirtualTerminal term, string text, int timeoutMs = 3000);
    
    // Prüft ob Text NICHT auf dem Screen ist
    static void DoesNotContain(VirtualTerminal term, string text);
}
```

## Tests pro Seite + UX-Fixes

### 1. Navigation (Cross-Page)

**Tests:**
- App startet auf Processes-Seite (Route "/")
- D1-D5 wechselt zur korrekten Seite
- Tab-Bar zeigt aktive Seite korrekt an
- Q beendet die App

**Kein UX-Fix nötig** — Navigation ist konsistent.

### 2. Processes Page

**Tests:**
- Initial: Prozessliste wird angezeigt mit korrektem Header
- Suche: "/" aktiviert Suche, Tippen filtert, Escape beendet
- Sortierung: Tab/F6 cycled durch Sort-Spalten
- Gruppe: G cycled durch Gruppen-Filter
- Overlay: Enter öffnet Detail-Overlay mit 4 Tabs
- Overlay-Navigation: Links/Rechts wechselt Tabs
- Kill-Flow: K → Bestätigung Y/N
- Scrolling: Arrow Up/Down, PageUp/Down, Home/End

**UX-Fix: Suchleiste vereinheitlichen**
Aktuell zeigt die Processes-Suchleiste ein anderes Format als Services/Network. Vereinheitlichen auf:
- Aktiv: `"/ {searchText}█"` in Warning-Farbe (gelb) — gleich wie Services/Network
- Inaktiv: `"Group: {group} | Sort: {sort}"` in TextDim — bleibt spezifisch für Processes da andere Seiten keine Gruppen/Sort haben

### 3. Services Page

**Tests:**
- Initial: Service-Liste mit Status-Icons (▶/■)
- Suche: "/" aktiviert Suche, filtert nach Name
- Aktionen: S startet, X stoppt, R restartet — Status-Message erscheint
- Detail-Modal: Enter öffnet Detail mit Service-Infos
- Detail-Modal Aktionen: S/X/R funktionieren auch im Modal

**UX-Fix: Aktionen im Detail-Modal**
Aktuell muss man das Modal schließen um S/X/R zu nutzen. Fix: S/X/R auch im Detail-Modal unterstützen. Nach Aktion: StatusMessage aktualisieren, Service-Liste refreshen, Modal bleibt offen mit aktualisierten Daten.

### 4. Network Page

**Tests:**
- Initial: Connection-Liste mit Prozessname, PID, Protocol, Endpoints, State
- Suche: Filtert nach Prozessname, PID, Endpoints
- Farbcodierung: Established = weiß, andere = grau
- Detail-Modal: Enter öffnet Detail mit vollständigen Endpoint-Adressen

**UX-Fix: Detail-Modal hinzufügen**
Aktuell fehlt ein Detail-Modal. Neues Modal zeigt:
- Prozessname + PID
- Protocol
- Local Endpoint (vollständig, nicht abgeschnitten)
- Remote Endpoint (vollständig, nicht abgeschnitten)
- Connection State
- Escape schließt Modal

### 5. Performance Page

**Tests:**
- Initial: CPU/RAM/Disk/Network Panels werden angezeigt
- Graph-Rendering: GraphNodes zeigen Daten
- Detail-Modal: Enter/Tab öffnet Detail
- Detail-Navigation: Links/Rechts cycled Sektionen
- Disk-Detail: Up/Down wechselt Disks

**UX-Fix: Status-Bar Keyboard-Hints**
Aktuell zeigt die Status-Bar auf der Performance-Seite nur statischen Text. Fix: Zeige `"Enter/Tab: Detail | 1-5: Navigate | Q: Quit"` in der Status-Bar.

### 6. Settings Page

**Tests:**
- Initial: 6 Settings-Zeilen mit aktuellen Werten
- Navigation: Up/Down wählt Zeile, Home/End springt
- Werte ändern: Links/Rechts cycled Optionen
- Speichern: Wert ändert sich sofort, "✓ Saved" erscheint

**UX-Fix: Home/End Support**
Aktuell kein Home/End in Settings. Fix: Home → erste Zeile, End → letzte Zeile.

## Test-Reihenfolge

1. **Test-Infrastruktur** (Fixture, Helpers, Projekt-Setup)
2. **NavigationTests** (Tab-Wechsel, App-Start)
3. **ProcessesPageTests** + Suchleisten-Fix
4. **ServicesPageTests** + Modal-Aktionen-Fix
5. **NetworkPageTests** + Detail-Modal-Fix
6. **PerformancePageTests** + Status-Bar-Hints
7. **SettingsPageTests** + Home/End-Fix

## Technische Details

### Actor-Mocking Strategie

Der MonitoringSupervisor wird durch einen Test-Actor ersetzt der vordefinierte Antworten liefert:

```csharp
public class TestSupervisorActor : ReceiveActor
{
    public TestSupervisorActor(TestData data)
    {
        Receive<StartCpuMonitoring>(_ => 
            Sender.Tell(CreateStream(data.CpuSnapshots)));
        Receive<StartProcessMonitoring>(_ => 
            Sender.Tell(CreateStream(data.ProcessSnapshots)));
        Receive<GetServices>(_ => 
            Sender.Tell(data.Services));
        Receive<KillProcess>(msg => 
            Sender.Tell(new ActionSuccess($"Killed {msg.Pid}")));
        // ... etc
    }
}
```

### Timing in Tests

Termina's Input-Processing ist asynchron. Zwischen Key-Events braucht es kurze Delays:

```csharp
input.EnqueueKey(ConsoleKey.Slash);      // Suche öffnen
await Task.Delay(100);                    // Warten auf Rendering
input.EnqueueString("chrome");            // Suchtext
await Task.Delay(200);                    // Warten auf Filter
ScreenAssert.Contains(terminal, "chrome"); // Prüfen
```

### VirtualTerminal Assertions

```csharp
// Prüfe Tab-Bar
ScreenAssert.LineContains(terminal, 0, "Processes");
ScreenAssert.ActiveTab(terminal, 0);  // Checks cyan/selection color

// Prüfe Prozessliste
ScreenAssert.Contains(terminal, "chrome");
ScreenAssert.Contains(terminal, "42.0%");  // CPU

// Prüfe Overlay
input.EnqueueKey(ConsoleKey.Enter);
await Task.Delay(200);
ScreenAssert.Contains(terminal, "PID:");
ScreenAssert.Contains(terminal, "CPU:");
```

## Scope-Grenzen

**In Scope:**
- Integration-Tests für alle 5 Seiten + Navigation
- UX-Fixes: Suchleiste, Services-Modal-Aktionen, Network-Detail-Modal, Performance-Hints, Settings Home/End
- Test-Infrastruktur mit Fixture + Helpers

**Out of Scope:**
- Snapshot-Testing (kein Output-Diffing)
- Performance-Benchmarks
- Accessibility-Testing
- Theme-Tests (Dark/Light/Nord visuell verifizieren)
- GPU-spezifische Tests (GPU immer deaktiviert in Tests)
