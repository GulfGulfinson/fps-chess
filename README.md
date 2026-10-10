# FPS Chess

Schach in C# mit zwei Modi, die dieselbe Spiellogik (`Chess.Core`) nutzen:

- **2D-Brett (klassisch):** normales Schach, auch zu zweit an einem Rechner.
- **FPS Chess (Duelle):** Schach auf einem 3D-Brett, das in einem Themenraum auf einem Tisch steht. Jedes Schlagen
  wird als Duell aus der Ego-Perspektive ausgetragen, und zwar auf dem Brett selbst: Die anderen Figuren sind
  Deckung, auf dem Tisch stehen Hindernisse. Verliert die angreifende Figur, stirbt sie, und die verteidigende
  bleibt stehen. Gespielt wird gegen den Computer oder online gegen andere.

## Spielen ohne Installation

Fertige Versionen für Windows und Linux gibt es unter
[Releases](https://github.com/GulfGulfinson/fps-chess/releases): Zip herunterladen, entpacken, starten. .NET und
Godot sind eingebaut. Online spielt man über "Online spielen" im Hauptmenü, der Server ist voreingestellt.

- **Windows:** `FPSChess.exe` starten. Die Datei ist nicht signiert, deshalb warnt Windows SmartScreen beim ersten
  Start: "Weitere Informationen", dann "Trotzdem ausführen".
- **Linux:** `FPSChess.x86_64` starten (falls nötig vorher `chmod +x FPSChess.x86_64`).

Neue Versionen baut `deploy/build-client-release.sh <version>` (braucht die Godot-4.7.2-.NET-Exportvorlagen).

## Anforderungen

| # | Anforderung | Umsetzung |
|---|---|---|
| 1 | Zwei Spieler*innen an einem Rechner, nur zulässige Züge | 2D-Modus "Mensch gegen Mensch". `Game` nimmt nur Züge aus der legalen Zugliste an (inkl. Rochade, en passant, Umwandlung, Schach-, Matt- und Patt-Erkennung, Remis durch Stellungswiederholung, 50-Züge-Regel, ungenügendes Material). |
| 2 | Spiele speichern und laden | JSON-Spielstände im Godot-Benutzerordner, für beide Modi (FPS Chess gegen den Computer, im Brett-Modus). Gespeichert werden Startstellung, Zugliste und Duellausgänge; beim Laden wird jeder Zug erneut durch die Regeln geprüft, eine manipulierte Datei kann also keine illegale Stellung erzeugen. |
| 3 | Computergegner | Schach: Negamax mit Alpha-Beta-Suche (1 bis 4 Halbzüge) plus Ruhesuche über Schlagzüge, Bewertung über Material und Figur-Feld-Tabellen, Suche im Hintergrund-Thread. Duelle: ein Bot, der über ein Navigationsnetz läuft, Deckung nutzt und schießt; Reaktionszeit und Zielfehler hängen von der Stufe ab. |
| 4 | Timed Mode (optional) | Schachuhr mit Inkrement. Zeitüberschreitung verliert, außer der Gegner hat nur noch den König (dann Remis). Während eines Duells steht die Uhr. |

## Starten

Voraussetzungen: [.NET SDK](https://dotnet.microsoft.com/download) 8 oder neuer und [Godot 4.7 .NET](https://godotengine.org/download) (die Variante mit C#-Unterstützung).

```sh
# Tests der Spiellogik
dotnet test

# Spiel starten: im Godot-Editor game/project.godot öffnen und F5 drücken, oder direkt:
godot --path game

# Integrationstest des Spielservers (startet einen eigenen Server, spielt eine Online-Partie und eine gegen den Computer)
godot --headless --path game res://tools/ServerTest.tscn
```

Spielstände liegen unter Linux in `~/.local/share/godot/app_userdata/FPS Chess/saves`, unter Windows in `%APPDATA%\Godot\app_userdata\FPS Chess\saves`.

## FPS Chess

### Regeln

- Normales Schach, bis eine Figur eine andere schlagen will. Dann beginnt ein Duell zwischen beiden Figuren.
- Duell: 1 gegen 1, 3 Sekunden Countdown, dann 60 Sekunden. Wer keine Lebenspunkte mehr hat oder vom Tisch fällt,
  verliert. Läuft die Zeit ab, gewinnt die verteidigende Figur.
- Gewinnt die angreifende Figur, wird der Zug normal ausgeführt. Verliert sie, wird sie vom Brett genommen, die
  verteidigende Figur bleibt stehen, und der Zug gilt trotzdem als gespielt.
- Steht danach der eigene König im Angriff (etwa weil eine gefesselte Figur ihr Duell verloren hat), darf der
  Gegner den König schlagen. Verliert der König dieses Duell, ist die Partie vorbei.

### Figurenklassen

| Figur | Leben | Waffe | Besonderheit |
|---|---|---|---|
| Bauer | 100 | Halbautomatik-Gewehr | keine |
| Springer | 120 | Schrotflinte (8 Schrotkugeln, kurze Reichweite) | Doppelsprung |
| Läufer | 90 | Scharfschützengewehr | Zielfernrohr (rechte Maustaste) |
| Turm | 200 | Minigun (muss anlaufen) | nimmt 25 % weniger Schaden, langsamer |
| Dame | 160 | Sturmgewehr | Dash (Shift), 2 Granaten (G) |
| König | 140 | Revolver | regeneriert Leben, wenn er 3 Sekunden nicht getroffen wird |

Alle Werte stehen in `src/Chess.Core/Duels/FighterClass.cs`.

### Steuerung

| Phase | Eingabe | Aktion |
|---|---|---|
| Brett | Linksklick | eigene Figur wählen, dann Zielfeld anklicken (grün: Zug, rot: Schlagen) |
| Brett | rechte Maustaste halten, Mausrad | Kamera drehen, zoomen |
| Duell | Maus, W A S D | zielen, laufen |
| Duell | Linksklick (halten) | schießen |
| Duell | R | nachladen |
| Duell | Leertaste | springen (Springer: zweimal) |
| Duell | Shift | rennen, Dame: Dash |
| Duell | G | Granate (Dame) |
| beide | Esc | Menü (Speichern gegen den Computer, Aufgeben, Hauptmenü) |

### Grafik

Im Hauptmenü unter "Grafik (FPS Chess)": **Niedrig** rendert die 3D-Szene mit 60 % der Auflösung (hochskaliert mit
FSR) und ohne Schatten und Leuchten, **Mittel** mit 80 % und einfachen Schatten, **Hoch** in voller Auflösung. Auf
Laptops mit integrierter Grafik ist Mittel oder Niedrig die richtige Wahl.

### Online spielen

Im Hauptmenü "Online spielen": Namen eingeben und mit dem öffentlichen Server `wss://chess.gulfgulfinson.de`
verbinden. In der Lobby eine Partie erstellen (Bedenkzeit, Farbe) oder einer offenen Partie beitreten, aus der
Liste oder mit dem vierstelligen Raumcode. Bricht die Verbindung ab, verbindet sich das Spiel neu; der Platz bleibt
60 Sekunden reserviert.

Ohne Internet: "Im LAN hosten" startet den Server im eigenen Spiel, die anderen tragen die angezeigte Adresse
(`ws://<IP>:11800`) als Server ein.

## Aufbau

```
src/Chess.Core/            Spiellogik, ohne Abhängigkeit zu Godot
  Position*.cs             Stellung, FEN, legale Zuggenerierung (inkl. Schlagen eines offenen Königs)
  Game.cs, Duel.cs         Partieverlauf, Spielende, Remisregeln; Duell-Variante: Schlagzug -> PendingDuel -> ResolveDuel
  Duels/                   Duellregeln: Klassen, Waffen, Perks, Leben, Munition, Countdown, Zeitlimit
  Notation.cs              Algebraische Notation (SAN) für die Zugliste
  ChessClock.cs            Schachuhr (wird pro Frame mit der vergangenen Zeit "getickt")
  Ai/                      Computergegner und Stellungsbewertung
  Persistence/             Speichern und Laden (JSON, Format v2 mit Duellausgängen)
  Session/GameSession.cs   Ablauf einer Partie: wer ist am Zug, Computer im Hintergrund, Uhr, Duelle
tests/Chess.Core.Tests/    xUnit-Tests (u. a. Perft-Referenzwerte, Duell-Variante, Duellregeln)
game/                      Godot-Projekt (Spiel und Server in einem)
  scripts/App/             Hauptknoten, Menüs, Lobby, Spielstände, Tastenbelegung
  scripts/Board2D/         2D-Ansicht
  scripts/Match/           FPS Chess aus Sicht eines Spielers: Brett von oben, Kamera, Übergänge ins Duell
  scripts/Duel/            Ego-Figur, Waffenmodelle, Duell-HUD, Streuung
  scripts/Arena/           Tisch, Brett und Hindernisse der Themenräume (Bibliothek, Burgsaal, Labor), Figurenmodelle
  scripts/Net/             Protokoll (RPC-Kanäle, Nachrichten), Client und Server
  scripts/Net/Server/      Lobby, Räume, autoritatives Duell (eigene Physikwelt pro Raum), Duell-Bot
  scripts/Ui/              Gemeinsame Oberfläche (Theme, Texte, Dialoge)
  tools/                   Entwicklerwerkzeuge: Screenshots, Server-Integrationstest
deploy/                    Server-Release, systemd-Unit, nginx, Einrichtung auf gulfinson
docs/superpowers/specs/    Entwurf von FPS Chess
```

**Ein Codepfad für beide Gegnerarten.** FPS Chess spricht immer mit einem Spielserver. Gegen den Computer läuft
dieser Server im selben Prozess und wird über eine lokale WebSocket-Verbindung angesprochen, online ist es derselbe
Code auf gulfinson. Der Server entscheidet alles: Zulässigkeit der Züge (`Chess.Core`), Treffer (Strahlen in seiner
eigenen Kopie der Arena), Leben, Zeit und den Bot. Die Clients bewegen ihre Figur selbst und schicken Position und
Schüsse; der Server prüft, ob ein Schuss von dort kommen kann, wo die Figur zuletzt stand.

Die Ansichten enthalten keine Schachregeln. Die Korrektheit der Zuggenerierung ist über Perft-Tests abgesichert:
Für fünf Standardstellungen wird die Anzahl aller Zugfolgen bis Tiefe 3 bzw. 4 gezählt und mit den Referenzwerten
von [chessprogramming.org](https://www.chessprogramming.org/Perft_Results) verglichen.

## Server

Der öffentliche Server läuft auf gulfinson als `webapp-chess.service` (127.0.0.1:3107, nginx und Cloudflare davor).
Neue Version ausrollen:

```sh
deploy/deploy.sh
```

Das baut das Release lokal (`deploy/build-server-release.sh`), lädt es hoch und schaltet es mit dem
Deploy-Skript des Servers um. Einen Server zum Testen startet man mit
`godot --headless --path game -- --server --port 11800`.

## Lizenzen

Die Schriftart DejaVu Sans (`game/assets/fonts`) steht unter der Bitstream-Vera-/DejaVu-Lizenz, siehe `DejaVu-LICENSE.txt`. Alle 3D-Modelle werden im Code aus Grundkörpern erzeugt.
