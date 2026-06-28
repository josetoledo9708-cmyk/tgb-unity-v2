# Plan de implementación — Core C# de The Great Book

> Basado en `docs/superpowers/specs/2026-06-27-campo-tcg-design.md` y
> `data/catalogo.v3.json`. Objetivo: motor de reglas completo, puro C# (sin UnityEngine),
> determinista y serializable, jugable en hotseat local. La presentación Unity 3D y el
> netcode van encima después.

## 0. Principios de arquitectura

- **Core sin UnityEngine.** Todo el motor compila y se testea sin abrir Unity (NUnit /
  Unity EditMode). La presentación nunca contiene reglas.
- **Determinismo.** Sin aleatoriedad oculta. El barajado usa un RNG con **semilla**
  registrada en el estado (`GameState.Seed`) → replays y futura sincronización por red.
- **Command pattern.** Cada jugada es un `GameCommand` validado contra el estado. El motor:
  `Validate(state, cmd) -> bool/errores`, luego `Apply(state, cmd) -> eventos`. La capa de
  red/UI solo envía comandos y observa eventos.
- **Estado serializable.** `GameState` completo serializable (JSON primero; binario luego).
- **Eventos.** Cada cambio emite un `GameEvent` (CartaJugada, FDGenerado, SERRetirado,
  VictoriaDeclarada…) que la UI consume para animar.

## 1. Estructura de carpetas (Unity)

```
Assets/Game/
  Core/            # C# puro, sin UnityEngine. asmdef "Game.Core"
    Model/         # GameState, PlayerState, Zona, CardInstance, enums
    Data/          # CardDefinition, CatalogLoader, DeckValidator
    Commands/      # GameCommand + comandos concretos
    Engine/        # GameEngine, PhaseMachine, FDSystem, VictorySystem
    Effects/       # EffectContext, EffectApi (primitivas), registro por carta id
    Events/        # GameEvent y tipos
  Runtime/         # MonoBehaviours, vista 3D, input → comandos. asmdef "Game.Runtime"
  Tests/
    EditMode/      # NUnit. asmdef "Game.Tests" (ref Game.Core)
Resources o StreamingAssets/
  catalogo.v3.json # copia del dato (o link al de /data)
```

- `asmdef` separados garantizan que `Game.Core` **no** referencia `UnityEngine`.

## 2. Modelo de datos (Core/Model + Core/Data)

- **Enums:** `CardType {DIA, TIERRA, SER_DIVINO, SER_HUMANO, SER_ANIMAL, CONCEPTO, HISTORIA}`,
  `EffectTrigger {AL_ENTRAR, AL_ENTRAR_CONDICIONAL, AL_SALIR, AL_TAPEARSE, AL_SER_DESTRUIDA,
  EFECTO_ACTIVADO, EFECTO_PASIVO_CONTINUO, PENALIZACION, USO_UNICO, RESPUESTA,
  EFECTO_DIFERIDO, EFECTO_DE_AREA, EFECTO_GLOBAL, AL_ACTIVAR_EL_DIA, AL_FINAL_DE_LA_ENTREGA}`,
  `Phase {PRELUDIO, GENESIS, PREPARACION, ENTREGA}`,
  `VictoryMode {field_at_entrega, on_piece_play}`, `VictoryId {I, II, III}`.
- **`CardDefinition`** (inmutable, viene del catálogo): id, nombre, type, coste/fd, dur,
  actCost, turns_left, especial, textos de efecto, flags. Es el "molde".
- **`CardInstance`** (carta concreta en partida): ref a `CardDefinition` + estado mutable
  (`durLeft`, `tapped`, `faceDown`, `turnsLeftRemaining`, `ownerId`, `instanceId`).
- **`Zona`**: lista tipada con capacidad. Zonas por jugador: `Mano`, `Mazo`, `Tierras`(7),
  `Seres`(3), `Concepto`(1, permite 1 boca abajo), `Retirados`, `PilaDia`(7 ordenada),
  `Historia`(1, horizontal).
- **`PlayerState`**: zonas + `FD` actual + flags de balance + `diaActual` (1-7) +
  `historiaId` declarada.
- **`GameState`**: `PlayerState[2]`, `activePlayer`, `Phase`, `turnNumber`, `Seed`,
  `rngState`, `pendingResponseWindow`, log de eventos. Serializable completo.

## 3. Carga de datos y validación de mazo (Core/Data)

- **`CatalogLoader`**: parsea `catalogo.v3.json` → diccionario `id -> CardDefinition` +
  listas por tipo, DÍA (orden), HISTORIA (con piezas y `modo_victoria`).
- **`DeckValidator`**: 40–50 cartas, **máx 3 copias** por carta, debe incluir las 5 piezas
  de la HISTORIA declarada; 7 DÍA y 1 HISTORIA van fuera del mazo.
- **Tests M0/M1:** catálogo carga con conteos correctos (7 DIA, 20 TIERRA, 45 SER, 42
  CONCEPTO, 7 HISTORIA); validador acepta/rechaza mazos límite.

## 4. Setup de partida (Core/Engine)

- Barajar mazo con `Seed`. Colocar HISTORIA (horizontal) y apilar 7 DÍA (dia1 arriba).
- Robar **7** cada jugador. **El primer jugador no roba** en su Génesis del turno 1.
- Inicializar FD=0, flags en false, diaActual=1.

## 5. Máquina de fases (Core/Engine/PhaseMachine)

1. **PRELUDIO** (jugador activo): FD=0; destapear sus TIERRAs; cada SER `durLeft--`; SER a 0
   → Retirados + dispara `AL_SALIR`; TIERRAs con `turns_left` decrementan, a 0 se destruyen
   + `AL_SER_DESTRUIDA` (salvo `tierraProtected`/Lot); reset flags
   (`diaFreeUsed, sacrificioUsed, diluvioUsed, tierraProtected`).
2. **GENESIS**: robar 1 (excepción primer turno del primer jugador); si no puede robar →
   **Victoria II** del rival.
3. **PREPARACION**: acepta comandos libres (ver §6) hasta que el jugador pase.
4. **ENTREGA**: chequear **Victoria III** (`field_at_entrega`); descartar mano hasta 7;
   pasar turno (cambiar `activePlayer`).

- Tests por fase con estados construidos a mano.

## 6. Comandos (Core/Commands)

`PlayTierra`, `TapTierra`, `PlaySer`, `PlayConcepto` (boca arriba / boca abajo),
`ActivateSerEffect`, `ActivateDia`, `ActivateResponse` (turno rival, paga FD reservado),
`EndPhase`/`EndTurn`, `DiscardCards`. Cada uno: `Validate` (fase correcta, FD suficiente,
límites de zona, condición) + `Apply` (muta estado, dispara efectos, emite eventos).

- Límites forzados: 7 TIERRAs, 3 SER, 1 TIERRA nueva/turno, 1 efecto activado por SER/turno,
  1 trampa boca abajo, mano 7 al fin de Entrega.

## 7. Economía FD (Core/Engine/FDSystem)

- `TapTierra` → +fd (con efectos `AL_TAPEARSE` y penalizaciones de tierras especiales).
- Gasto valida `FD >= coste`. FD **no se vacía** al terminar tu turno → persiste para tus
  `RESPUESTA` durante el turno rival. Se resetea en **tu** próximo PRELUDIO.

## 8. Victorias (Core/Engine/VictorySystem)

- **I:** al activar dia7 en PREPARACION (requiere DÍAs 1-6 + 3 TIERRAs + 2 SER, uno coste≤2
  y otro ≥3, en orden 1→7; Isaac+Rebeca permite ignorar orden).
- **II:** robo imposible por mazo vacío en GENESIS.
- **III:** por `modo_victoria` de la HISTORIA declarada:
  - `field_at_entrega`: 5 piezas en campo al fin de ENTREGA.
  - `on_piece_play` (h5): al **jugar** `La Maldición de la Tierra` con las otras 4 piezas en
    campo, **antes** de resolver su efecto.
- Tests dedicados por cada vía + el caso h5.

## 9. Sistema de efectos (Core/Effects) — el núcleo difícil

**Decisión:** efectos como **handlers C# por `id` de carta**, sobre una **API de
primitivas** compartida (no un DSL de texto). El texto del JSON queda como referencia
humana; la lógica vive en código tipado y testeable.

- **`EffectApi`** (primitivas reutilizables): `Draw(n)`, `Discard(n, random?)`,
  `SearchDeck(filtro, aMano|aCampo)`, `Destroy(target)`, `SendToRetirados`,
  `ReturnFromRetirados`, `Protect(target, turnos)`, `AddFD(n)`, `ModifyDur(target, n)`,
  `RevealHand`, `LookTop(n, deck)`, `CancelEffect`, `PlayDiaFree`, etc.
- **`EffectContext`**: `state`, `self` (CardInstance), `owner`, `opponent`, `targets`,
  cola de elección del jugador (para targets/decisiones).
- **`EffectRegistry`**: `Dictionary<string id, Action<EffectContext>>` por trigger. Cada
  carta registra solo los triggers que usa (`AL_ENTRAR`, `EFECTO_ACTIVADO`, `AL_SALIR`…).
- **Targeting / decisiones del jugador:** se modela como *requests* que el motor emite y la
  UI/IA responde (en hotseat, el jugador activo elige). Mantiene el Core determinista.
- **Implementación incremental por familias** (no las 121 de golpe):
  1. Primitivas + cartas "vanilla" (solo generan FD, robos simples): Ararat, Cuervo, Paloma…
  2. Tutores/búsqueda: Canaán, Dotán, Llamada de Abram, Promesa de la Tierra…
  3. Condicionales de pareja: Adán/Eva, Noé/Sem/Jafet, Abraham/Sara/Lot…
  4. Destrucción/control: Ángeles de Sodoma, El Diluvio, Caín, Esaú/Judá…
  5. RESPUESTA/cancelación: Espada de Fuego, Arco Iris, Perdón de José, Velo de la Noche…
  6. Especiales: Sodoma/Gomorra+Lot, Tierra de Nod↔Caín, Benjamín (vuelve al mazo),
     Isaac+Rebeca, José+Faraón+Egipto (indestructibles).
- **Flags de balance** integrados: `diaFreeUsed`, `sacrificioUsed`, `diluvioUsed`,
  `tierraProtected`.
- Cada familia entra con sus tests.

## 10. Eventos y enganche con la UI (Core/Events)

- `GameEvent` tipados emitidos por cada `Apply`. La capa Unity (Runtime) se suscribe y
  reproduce animaciones. Para hotseat, una `GameView` simple basta para validar el motor
  antes de invertir en 3D.

## 11. Milestones y orden de entrega

| M | Entregable | Tests |
|---|---|---|
| M0 | Estructura + asmdef + modelos + `CatalogLoader` | Carga catálogo, conteos |
| M1 | `GameState` + `DeckValidator` + setup/barajado con semilla | Validación mazo, setup determinista |
| M2 | `PhaseMachine` + comandos básicos (TIERRA/SER/CONCEPTO/tap/endturn) + FDSystem | Cada fase, límites de campo, FD |
| M3 | `VictorySystem` (I, II, III incl. h5) | Una prueba por vía de victoria |
| M4 | `EffectApi` + `EffectRegistry` + familias 1-3 | Efectos por familia |
| M5 | Familias 4-6 + flags de balance + casos especiales | Interacciones complejas |
| M6 | `GameView` mínima de consola/hotseat para jugar una partida completa | Partida end-to-end |
| M7 (después) | Presentación Unity 3D | — |
| M8 (después) | Netcode encima del Core | — |

## 12. Riesgos / decisiones abiertas

- **Targeting interactivo** en hotseat vs futura IA: el modelo de *requests* debe quedar
  limpio desde M4 para no reescribir.
- **Balance Victoria III** (3 SER que decaen) — validar jugando tras M6.
- Cartas con texto ambiguo (p.ej. Benjamín `activado` = disparo de fin de turno) se
  resuelven como casos especiales documentados en el código.
- **Nombre de carpeta/proyecto** y assets 3D quedan fuera de este plan.

## 13. Estado de implementación (2026-06-27)

Core construido en `src/Game.Core` (.NET puro, net9.0) + `tests/Game.Core.Tests`
(runner propio). Correr: `dotnet run --project tests/Game.Core.Tests`. **55 tests verdes.**

| M | Estado | Notas |
|---|--------|-------|
| M0 | ✅ | modelo + `CatalogLoader` |
| M1 | ✅ | `GameState`, zonas, `DeckValidator`, setup con semilla |
| M2 | ✅ | fases, comandos, FD, Victoria II |
| M3 | ✅ | condiciones DÍA, Victorias I y III (incl. h5 on_piece_play) |
| M4 | ✅ | `EffectApi` + `EffectRegistry`; familias 1-3 |
| M5 | ✅ | destrucción/control + casos especiales (Lot, Sodoma/Gomorra, Benjamín, Caín/Nod) |
| M6 | ✅ | `ConsoleView` + e2e + determinismo |
| Efectos | ✅ | **109/114 cartas** + recompensas de DÍA + `ActivateResponse` (trampas reactivas) |
| M7 | 🟡 | Scaffold Unity listo (Core en Assets, asmdefs, loader Newtonsoft, StreamingAssets, EditMode). Falta: abrir editor + presentación 3D |
| M8 | ⏳ | Netcode encima del Core |

**Cobertura de efectos: 109 de 114 cartas** con handler. Las 5 sin efecto son intencionales:
`dia1` (sin recompensa), `dia7` (Victoria I en el motor), `t03`/`t06`/`t07` (TIERRAs básicas).
Estados nuevos (Indestructible, DiaBlocked, TierrasNoFd, NextSerDurBonus, EffectsBlocked,
NegatedNextEffect, ConceptosBlocked, OncePerGame) se resetean/decrementan en Preludio.

### Aproximaciones documentadas (sin stack/timing completo)

- **Control de SER rival** (Jacob `activado`, La Esposa de Putifar, Putifar `activado`):
  emiten log; no transfieren control real (falta un modelo de "control temporal").
- **Copiar efecto** (c23 El Robo de la Bendición): aproximado a robar 1 (no copia el último
  AL_ENTRAR rival; falta historial de efectos).
- **Mirar y reordenar** (Betel, Adán `activado`, c16, c33, Gehón): emiten log; `LookTopTakeOne`
  no reordena el resto al fondo.
- **RESPUESTA / negación**: `NegatedNextEffect` anula el *próximo* efecto del rival (no un
  efecto concreto en vuelo; suficiente para trampas, no para una pila de prioridad real).

### Pendientes para "motor completo"

- **Targeting interactivo real** (hoy `AutoDecisionProvider` elige la 1ª opción legal).
- **Ventana de prioridad / pila** para respuestas encadenadas y negaciones precisas.
- **IA oponente** para un jugador solo.
- **Serialización** explícita de `GameState` a JSON/binario (el modelo ya es serializable).
- Pulir las aproximaciones de arriba cuando exista la pila de efectos.

## 14. Port a Unity (M7) — estado

**Hecho (scaffold):**
- Core movido a `Assets/Game/Core` (asmdef `Game.Core`, `noEngineReferences`). Fuente única:
  Unity lo compila vía asmdef; el build dev `.NET` vía `dev/Game.Core.Dev.csproj`.
- `Assets/Game/Runtime` (asmdef): `UnityCatalogLoader` (Newtonsoft) + `GameBootstrap` (demo).
- `Assets/Game/Tests/EditMode` (asmdef + NUnit smoke: conteos, turno, Victoria III).
- `Assets/StreamingAssets/catalogo.v3.json`, `Packages/manifest.json`, `ProjectVersion.txt`.

**Falta (requiere el editor Unity, no disponible en este entorno):**
1. Abrir en Unity Hub (genera `.meta`/`Library`, resuelve paquetes).
2. Escena + cámara oblicua; GameObject con `GameBootstrap` para verificar (Console).
3. Presentación 3D: prefabs de carta, layout del campo (espejo, 7 tierras, 3 SER, etc.),
   arrastrar/soltar, animaciones, mapear input -> comandos del `GameEngine`.
4. Correr EditMode tests desde el Test Runner.
