# WheelFix

[![CI](https://github.com/SKU-1zx/WheelFix/actions/workflows/ci.yml/badge.svg)](https://github.com/SKU-1zx/WheelFix/actions/workflows/ci.yml)
[![Licenza: MIT](https://img.shields.io/badge/Licenza-MIT-blue.svg)](LICENSE)

**Un filtro anti-rimbalzo leggero per la rotellina del mouse su Windows.**

[English](README.md)

![Anteprima dell'interfaccia di WheelFix](docs/interface-preview.svg)

Quando un encoder meccanico si usura può inviare brevi impulsi nella direzione
sbagliata: scorri due scatti verso il basso e la pagina torna indietro di uno.
WheelFix corregge questi impulsi contrari prima che arrivino alle normali
applicazioni Windows.

## Funzioni

- Filtro globale della rotellina verticale, senza ritardo nella direzione normale.
- Pausa di fine raffica regolabile da 400 a 500 ms.
- Preset Rapido, Bilanciato e Aggressivo.
- Contatore degli impulsi contrari corretti.
- Controlli nella tray e avvio facoltativo con Windows.
- Log diagnostico locale, apribile direttamente dal menu della tray.
- Interfaccia automatica italiano/inglese in base alla lingua di Windows.
- Nessun installer, driver, telemetria, rete o privilegio amministrativo.
- Gli impulsi coerenti passano; quelli contrari vengono sostituiti con `SendInput`.

## Download e avvio

1. Scarica lo ZIP Windows più recente da
   [Releases](https://github.com/SKU-1zx/WheelFix/releases/latest).
2. Estrailo in una cartella stabile.
3. Avvia `WheelFix.exe`.
4. Parti da **Bilanciato (450 ms)** e usa normalmente la rotellina.

Chiudendo la finestra WheelFix resta attivo nell'area di notifica. Con il tasto
destro sull'icona puoi mettere in pausa il filtro, cambiare intensità, attivare
l'avvio automatico, aprire il log diagnostico o uscire.

## Regolazione

| Preset | Pausa | Quando usarlo |
| --- | ---: | --- |
| Rapido | 400 ms | Pausa breve per invertire |
| Bilanciato | 450 ms | Valore predefinito; verificato nei test simulati |
| Aggressivo | 500 ms | Pausa di mezzo secondo per invertire |

Una raffica mantiene la direzione del primo evento finché non arrivano eventi
per la pausa selezionata. Per invertire direzione, ferma brevemente la
rotellina. Una pausa maggiore blocca errori più lunghi ma rallenta l'inversione.

Il contatore **Impulsi contrari corretti** conferma se il filtro sta intervenendo.
Se aumenta mentre il salto indesiderato sparisce, l'impostazione è corretta.

## Come funziona

WheelFix installa un normale hook utente `WH_MOUSE_LL` e osserva soltanto i
messaggi della rotellina verticale. Il primo evento dopo una pausa avvia una
raffica: gli eventi in quella direzione passano subito e tutti quelli contrari
vengono corretti conservandone la quantità di scroll.

Movimento, pulsanti, scorrimento orizzontale ed eventi iniettati da altri
software non vengono toccati. Gli eventi sintetici sono esclusi prima di aggiornare stato e timeout.

## Log diagnostico

Scegli **Apri log diagnostico** dal menu della tray per aprire:

`%LOCALAPPDATA%\WheelFix\WheelFix.log`

Il log registra avvio e arresto di WheelFix, stato dell'hook, modifiche alle
impostazioni, raw delta/direzione, stato, pausa, output e motivo della correzione,
reset a IDLE ed errori di `SendInput`. Non registra movimenti, clic o nomi delle
applicazioni e nulla viene inviato in rete. Il file viene azzerato
automaticamente prima di superare 1 MB.

Il reset dopo la pausa viene valutato prima del successivo evento fisico;
il log riporta anche il timestamp della scadenza (`reset_at`). Non esistono
timer di reiniezione o code di scroll. La nuova impostazione `GestureGapMs`
parte da 450 ms senza ereditare i vecchi valori di `BurstGapMs`.

## Limiti

- Applicazioni e giochi che leggono direttamente il dispositivo tramite Raw
  Input possono saltare un hook Windows in modalità utente.
- WheelFix riduce i sintomi del rimbalzo, ma non ripara fisicamente l'encoder.
- Una vera inversione viene accettata solo dopo la pausa configurata.
- Se il primo impulso dopo la pausa è errato, viene agganciato quel verso:
  UP/DOWN casuali non permettono di ricostruire l’intenzione fisica.
- Pause raw di almeno 450 ms (con il preset predefinito) iniziano una nuova gesture,
  anche se fisicamente si stava ancora girando la rotellina.
- `SendInput` può fallire verso applicazioni elevate: il log mostra
  `sendinput_failed`, senza far passare il tick contrario né ritentarlo.
- La versione attuale filtra soltanto la rotellina verticale.
- L'eseguibile non è firmato digitalmente, quindi Windows può mostrare un
  avviso di reputazione al primo avvio.

Un driver kernel coprirebbe più percorsi di input, ma richiederebbe
installazione, elevazione e firma. È volutamente fuori dallo scopo di questa
piccola utility portatile.

## Compilazione dai sorgenti

Su Windows 10/11 con .NET Framework 4.x:

```bat
build-and-run.cmd
```

Viene usato il compilatore C# già incluso in .NET Framework e l'eseguibile viene
creato in `dist\WheelFix.exe`. Non servono pacchetti NuGet o download.

La repository include anche `WheelFix.csproj`, compatibile con .NET Framework
4.8 e apribile direttamente con Visual Studio o MSBuild.

Per eseguire i test indipendenti del motore:

```bat
test.cmd
```

## Avvio automatico e rimozione

L'opzione **Avvia automaticamente con Windows** scrive un solo valore nella
chiave dell'utente corrente
`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. Le preferenze sono
salvate in `HKCU\Software\WheelFix`.

Per rimuovere WheelFix, disattiva l'avvio automatico, scegli **Esci** dalla tray
ed elimina la cartella. Se vuoi rimuovere anche il log diagnostico, elimina
`%LOCALAPPDATA%\WheelFix`.

## Contributi

Una segnalazione è particolarmente utile se include modello del mouse, versione
di Windows, applicazione interessata e blocco direzione minimo che risolve il
problema.
Il flusso di sviluppo è descritto in [CONTRIBUTING.md](CONTRIBUTING.md).

## Licenza

[MIT](LICENSE) © 2026 SKU-1zx.
