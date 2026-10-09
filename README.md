# FPS Chess

Schach in C#: klassisch auf einem 2D-Brett oder aus der Ego-Perspektive auf einem begehbaren 3D-Brett.
Beide Ansichten nutzen dieselbe Spiellogik (`Chess.Core`), die Regeln sind in beiden Modi identisch.

## Anforderungen

| # | Anforderung | Umsetzung |
|---|---|---|
| 1 | Zwei Spieler*innen an einem Rechner, nur zulässige Züge | Modus "Mensch gegen Mensch". `Game` nimmt nur Züge aus der legalen Zugliste an (inkl. Rochade, en passant, Umwandlung, Schach-, Matt- und Patt-Erkennung, Remis durch Stellungswiederholung, 50-Züge-Regel, ungenügendes Material). Im FPS-Modus wechselt die Kamera nach jedem Zug auf die Seite der Person am Zug. |
| 2 | Spiele speichern und laden | JSON-Spielstände im Godot-Benutzerordner. Gespeichert werden Startstellung und Zugliste; beim Laden wird jeder Zug erneut durch die Regeln geprüft, eine manipulierte Datei kann also keine illegale Stellung erzeugen. Ein Spielstand kann in jeder der beiden Ansichten fortgesetzt werden. |
| 3 | Computergegner | Negamax mit Alpha-Beta-Suche (1 bis 4 Halbzüge, im Menü wählbar) plus Ruhesuche über Schlagzüge, damit der Computer nicht mitten in einem Abtausch abbricht. Bewertung: Material plus Figur-Feld-Tabellen. Die Suche läuft in einem Hintergrund-Thread. |
| 4 | Timed Mode (optional) | Schachuhr mit Inkrement (Bullet bis Klassisch). Zeitüberschreitung verliert, außer der Gegner hat nur noch den König (dann Remis). |

## Starten

Voraussetzungen: [.NET SDK](https://dotnet.microsoft.com/download) 8 oder neuer und [Godot 4.7 .NET](https://godotengine.org/download) (die Variante mit C#-Unterstützung).

```sh
# Tests der Spiellogik
dotnet test

# Spiel starten: im Godot-Editor game/project.godot öffnen und F5 drücken, oder direkt:
godot --path game
```

Spielstände liegen unter Linux in `~/.local/share/godot/app_userdata/FPS Chess/saves`, unter Windows in `%APPDATA%\Godot\app_userdata\FPS Chess\saves`.

## Steuerung

**2D-Modus:** Figur anklicken, dann Zielfeld anklicken. Rechtsklick hebt die Auswahl auf. Die Seitenleiste bietet Zug zurück (ohne Schachuhr), Brett drehen, Speichern, Aufgeben und Hauptmenü.

**FPS-Modus:**

| Taste | Aktion |
|---|---|
| Maus | umsehen |
| W A S D | laufen |
| Shift | rennen |
| Leertaste | springen |
| Linksklick | eigene Figur wählen, dann Zielfeld oder gegnerische Figur anvisieren und klicken (Schlagen = Schuss) |
| Rechtsklick | Auswahl aufheben |
| Esc | Pause-Menü (Weiterspielen, Zug zurück, Speichern, Aufgeben, Hauptmenü) |

Grün markierte Felder sind mögliche Züge, rot markierte Felder mögliche Schlagzüge. Steht eine Figur zwischen dir und einem markierten Zielfeld, kannst du trotzdem auf das Feld zielen.

## Aufbau

```
src/Chess.Core/            Spiellogik, ohne Abhängigkeit zu Godot
  Position*.cs             Stellung, FEN, legale Zuggenerierung
  Game.cs                  Partieverlauf, Spielende, Remisregeln, Zug zurück
  Notation.cs              Algebraische Notation (SAN) für die Zugliste
  ChessClock.cs            Schachuhr (wird pro Frame mit der vergangenen Zeit "getickt")
  Ai/                      Computergegner und Stellungsbewertung
  Persistence/             Speichern und Laden (JSON)
  Session/GameSession.cs   Ablauf einer Partie für die Oberfläche: wer ist am Zug, Computer im Hintergrund, Uhr
tests/Chess.Core.Tests/    xUnit-Tests (u. a. Perft-Referenzwerte für die Zuggenerierung)
game/                      Godot-Projekt
  scripts/App/             Hauptknoten, Hauptmenü, Spielstände, Tastenbelegung
  scripts/Board2D/         2D-Ansicht
  scripts/Fps/             3D-Welt, Spielerfigur, Figurenmodelle, Effekte, HUD
  scripts/Ui/              Gemeinsame Oberfläche (Theme, Texte, Dialoge)
  tools/Capture.*          Entwicklerwerkzeug für Screenshots, nicht Teil des Spiels
```

Die Ansichten enthalten keine Schachregeln. Sie zeigen den Zustand aus `GameSession` an und reichen Klicks als Züge weiter, und die Session meldet über Events (`MovePlayed`, `GameEnded`, `PositionReset`), was passiert ist. Dadurch lässt sich die gesamte Logik ohne Godot testen, und eine weitere Ansicht bräuchte keine Änderungen am Kern.

Die Korrektheit der Zuggenerierung ist über Perft-Tests abgesichert: Für fünf Standardstellungen wird die Anzahl aller Zugfolgen bis Tiefe 3 bzw. 4 gezählt und mit den Referenzwerten von [chessprogramming.org](https://www.chessprogramming.org/Perft_Results) verglichen.

## Lizenzen

Die Schriftart DejaVu Sans (`game/assets/fonts`) steht unter der Bitstream-Vera-/DejaVu-Lizenz, siehe `DejaVu-LICENSE.txt`. Alle 3D-Modelle werden im Code aus Grundkörpern erzeugt.
