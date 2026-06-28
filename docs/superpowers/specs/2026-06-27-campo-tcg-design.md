# Diseño del campo — TCG (Unity, 2 jugadores)

> Título de trabajo: `unity-card-game` (pendiente de nombre definitivo).
> Fecha: 2026-06-27. Estado: campo y motor base cerrados; varias áreas marcadas TBD.

## 1. Resumen

Juego de cartas 3D para 2 jugadores. Toma referencias de Hearthstone (presentación,
flujo de turnos) pero **no es un juego de combate**: no hay ataque, vida, salud ni
muerte de forma explícita. El núcleo es un **motor de tablero**: cada jugador despliega
cartas en zonas tipadas del campo para cumplir su condición de victoria, mientras
interfiere con el rival mediante cartas reactivas.

Recurso central: **Favor Divino (FD)**, generado por cartas TIERRA al tapearlas
(estilo Magic).

## 2. Layout del campo

El tablero es simétrico y está en **espejo** entre los dos jugadores (simetría puntual:
la zona que para el jugador principal queda a la derecha, para el rival queda a la
izquierda).

Por lado, de la mano hacia el centro:

```
[ Mano ]  (visible solo para su dueño)

Fila trasera (pegada a la mano):
  [ mazo ] [ tierra ][ tierra ][ tierra ][ tierra ][ tierra ][ tierra ][ tierra ]
   (al flanco)               7 espacios de TIERRA

Fila frontal (hacia el centro), orden derecha→izquierda para el jugador principal:
  espacio 1: descarte
  espacio 2: CONCEPTO
  espacio 3: SER
  espacio 4: SER
  espacio 5: SER
  espacio 6: DIA
  espacio 7: HISTORIA
```

- **Mazo:** ubicado al flanco de la fila de tierras, sin alterar la posición de las
  7 tierras. A la **derecha** en el lado del jugador principal, a la **izquierda** en
  el rival (espejo).
- El **centro** del tablero es solo la frontera entre ambos campos + el marcador de
  **fin de turno**. No hay zona de combate.

## 3. Tipos de carta

### TIERRA
- Ocupa la fila trasera (hasta 7 en campo).
- Se **tapea** para producir **1 FD** (estilo Magic).
- **Destapeo:** las tierras se destapean al inicio del **propio** turno de su
  controlador (no cada turno).
- El **FD no gastado persiste** durante el turno del rival → habilita respuestas
  reactivas (ver CONCEPTO boca abajo).
  - Ej: el rival tapea sus 7 tierras y gasta 2 FD; los 5 FD restantes siguen
    disponibles durante el turno del jugador principal para que el rival active una
    carta CONCEPTO boca abajo.

### SER
- Permanente **estacionario**: no ataca, no tiene stats de combate, no hay vida/daño.
- Cuesta **FD** al jugarse desde la mano a un espacio SER (3 espacios por lado).
- **Decae:** tras una cantidad de turnos (TBD, posiblemente por carta) pasa a descarte.
- Efectos:
  - algunos se activan **al entrar** al campo (ETB);
  - otros se **activan pagando FD extra** mientras la carta está en campo.

### CONCEPTO
- Funciona como hechizo/trampa (estilo Yu-Gi-Oh).
- Va **en el mazo**. Se juega desde la mano a la zona CONCEPTO pagando FD.
- Efectos múltiples (robo, destrucción de cartas rivales, bloqueo de efectos;
  detalles concretos TBD).
- Tras cumplir su función → **descarte**.
- Puede colocarse **boca abajo** en la zona CONCEPTO para activarse en el turno del
  rival, si queda FD sin usar del turno anterior (mecánica de trampa reactiva).

### DIA
- Hay **7 DIA** por mazo (externas a las 40-50 cartas principales).
- Se colocan al inicio de la partida **apiladas** en su zona, en orden dia1 → dia7,
  mostrando siempre la del día actual (dia1 arriba al comenzar).
- **1 activación por turno.** Cada DIA tiene **requisitos específicos + coste FD**.
- Al activarse otorga una **ventaja** y pasa a **descarte**, revelando la siguiente.
- **Activar dia7 = victoria automática.**

### HISTORIA
- **Externa** al mazo: se elige al construir el mazo y **no ocupa** ninguno de los
  40-50 espacios.
- Se coloca en el campo **al iniciar la partida**, visible para ambos jugadores,
  mostrada en **horizontal**.
- Funciona como **condición de victoria**: exige tener en campo un total de
  **5 cartas específicas**.
- Al **terminar el turno**, si se cumplen los requisitos → **victoria automática**.

### descarte
- Cementerio general. Recibe toda carta que ya no tiene lugar en el campo: activadas,
  descartadas, o enviadas desde la mano, el mazo o el campo.

## 4. Economía de FD

- FD = Favor Divino, único recurso.
- Se genera tapeando TIERRA (1 FD por tierra).
- Pool de FD **no se vacía** hasta el destapeo (inicio del propio turno).
- FD sobrante = maná abierto/reactivo durante el turno rival.

## 5. Condiciones de victoria

Un jugador gana si, **al terminar un turno**, ocurre cualquiera de:

1. **HISTORIA cumplida:** tiene en campo las 5 cartas específicas que pide su HISTORIA.
2. **DIA 7 activado:** ha activado la carta dia7.

## 6. Construcción del mazo

- **40-50 cartas principales** (TIERRA, SER, CONCEPTO).
- **+ 1 HISTORIA** (externa, elegida al construir).
- **+ 7 DIA** (externas).

## 7. Mano y robo

- **Mano inicial: 7 cartas.**
- El **primer jugador no roba** en su primer turno.
- **Tope de mano: 7.** Si al terminar el turno se tienen más de 7, el jugador
  selecciona cartas a descartar hasta quedar en 7.

## 8. Pendientes (TBD)

No bloquean el diseño del campo, pero deben resolverse antes de implementar:

- **Multiplayer:** online en red vs local hotseat vs local-con-red-después.
- **Estructura de turno / fases** detalladas (destapeo, robo, fase principal,
  fin de turno, descarte).
- **Decaimiento de SER:** número exacto de turnos (¿global o por carta?).
- **Efectos concretos** de cada CONCEPTO, SER, DIA y HISTORIA.
- **Reglas de prioridad / ventana reactiva** para activar trampas en el turno rival.
- **Presentación 3D:** cámara, disposición física de zonas, animaciones.
- **Nombre definitivo** del juego, del mazo (mazo/libro/historia) y del proyecto.
- **Cómo se pierde:** ¿solo cuando el rival gana? ¿fatiga de mazo vacío?
- **Empates / resolución simultánea** si ambos cumplen al mismo fin de turno.
