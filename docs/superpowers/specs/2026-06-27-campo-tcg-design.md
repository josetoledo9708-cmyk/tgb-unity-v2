# Diseño del campo y motor — The Great Book (Unity, 2 jugadores)

> Título de trabajo del proyecto: `unity-card-game`.
> Juego: **The Great Book** (TCG bíblico, Génesis). Fecha: 2026-06-27.
> Estado: campo + motor base cerrados. Catálogo v3.0 (120 cartas) recibido y reconciliado
> contra este spec (ver `docs/.../2026-06-27-catalogo-errata.md`).

## 1. Resumen

Juego de cartas 3D para 2 jugadores, temática Génesis. Referencias de presentación tipo
Hearthstone, pero **no es un juego de combate**: no hay ataque, vida, salud ni muerte
explícitos. Es un **motor de tablero**: cada jugador despliega cartas en zonas tipadas
para cumplir una de tres condiciones de victoria, interfiriendo con el rival mediante
cartas reactivas.

Recurso central: **Favor Divino (FD)**, generado tapeando cartas TIERRA (estilo Magic).

## 2. Layout del campo

Tablero simétrico, en **espejo** entre jugadores (simetría puntual: lo que para el
jugador principal queda a la derecha, para el rival queda a la izquierda).

Por lado, de la mano hacia el centro:

```
[ Mano ]  (visible solo para su dueño)

Fila trasera (pegada a la mano):
  [ mazo ] [ tierra ]×7
   al flanco        7 espacios de TIERRA (máx en campo = 7)

Fila frontal (hacia el centro), orden derecha→izquierda para el jugador principal:
  1: descarte (= Retirados)   2: CONCEPTO   3: SER   4: SER   5: SER   6: DIA   7: HISTORIA
```

- **Mazo** al flanco de las tierras, sin alterar la posición de las 7 tierras: a la
  **derecha** en el jugador principal, **izquierda** en el rival.
- El **centro** es solo la frontera entre campos + marcador de fin de turno. Sin combate.
- **Retirados** y **descarte** son la **misma zona** (cementerio general). Recibe toda
  carta que sale del campo: SER que decaen, CONCEPTO usados, descartes, y cartas enviadas
  desde mano/mazo/campo por efectos.

## 3. Límites de campo

| Límite | Valor |
|---|---|
| SER simultáneos | 3 |
| TIERRAs en campo | 7 |
| Cartas en mano al fin de ENTREGA | 7 |
| CONCEPTO boca abajo (trampas armadas) | 1 a la vez |

## 4. Estructura del turno (4 fases)

1. **PRELUDIO**
   - Resetea tu FD a 0 y **destapea** todas tus TIERRAs.
   - Cada SER pierde 1 turno de duración; SER con duración 0 → Retirados y dispara `AL_SALIR`.
   - TIERRAs con `turns_left` (Sodoma, Gomorra) decrementan; al llegar a 0 se destruyen y
     disparan `AL_SER_DESTRUIDA` (salvo Lot en campo).
   - Resetea flags de balance: `diaFreeUsed`, `sacrificioUsed`, `diluvioUsed`,
     `tierraProtected`.
2. **GENESIS**
   - Roba 1 carta. **Excepción:** el primer jugador no roba en su turno 1.
   - Si no puedes robar por mazo vacío → el rival gana (Victoria II).
3. **PREPARACION** (acciones libres, en cualquier orden)
   - Tapear TIERRAs para generar FD.
   - Jugar 1 TIERRA nueva por turno (gratis); máx 7 en campo.
   - Jugar SER pagando su coste FD (máx 3 en campo).
   - Jugar CONCEPTO pagando su coste FD.
   - Activar EFECTO de un SER pagando `actCost` (1 vez por SER por turno).
   - Activar el DÍA actual (ver §7 para el modelo de activación).
4. **ENTREGA**
   - Verifica Victoria III (5 piezas de la Historia en campo).
   - Descarta hasta el límite de 7 cartas en mano.

## 5. Economía de FD (modelo canónico)

- FD = Favor Divino, único recurso. 1 TIERRA tapeada = 1 FD (salvo tierras especiales).
- FD se **resetea a 0 en tu Preludio** (cuando destapeas).
- **FD sin gastar persiste durante el turno del rival** → es el combustible de tus
  CONCEPTO boca abajo (RESPUESTA). Para tener trampas activas debes **pre-tapear y
  reservar FD** antes de terminar tu turno.
- Las cartas tipo RESPUESTA se pagan con ese FD reservado (no se tapean tierras durante
  el turno rival; ese punto del JSON v3.0 se descarta a favor de este modelo).

## 6. Condiciones de victoria (se verifican al fin de ENTREGA salvo nota)

| ID | Nombre | Condición |
|---|---|---|
| Victoria I | Los 7 Días | Activar el DÍA 7 durante PREPARACION (los DÍA se activan en orden 1→7) |
| Victoria II | Fin del Mazo | El rival no puede robar en GENESIS por mazo vacío |
| Victoria III | La Historia | Las 5 piezas de la HISTORIA declarada están en campo al fin de ENTREGA |

- **dia7 (Victoria I) condición:** haber activado DÍAs 1–6 + tener 3 TIERRAs + tener
  2 SER en campo, uno de coste **≤2** y otro de coste **≥3**.
- **Victoria III — modo de verificación por HISTORIA** (campo `modo_victoria`):
  - `field_at_entrega` (default, h1–h4, h6, h7): las 5 piezas en campo al fin de ENTREGA.
  - `on_piece_play` (solo **h5**): su 5ª pieza `La Maldición de la Tierra` es un CONCEPTO
    de uso único; la victoria se verifica **al jugar** esa carta (con las otras 4 piezas
    ya en campo), antes de resolver su efecto. Es una jugada "all-in": si falta una pieza,
    el CONCEPTO resuelve normal y va a Retirados.

### Activación de DÍA (modelo A)

- **1 activación base por turno:** activa el DÍA actual (en orden 1→7) pagando su `coste`
  en FD y cumpliendo su condición. Avanza 1 día.
- **Activaciones gratis (extra):** las cartas de la lista `diaFreeUsed` permiten activar
  un DÍA **sin pagar el coste FD**, pero **la condición del DÍA siempre se exige**. La
  regla `diaFreeUsed` limita estas activaciones gratis a **1 por turno**.
- Lista `diaFreeUsed`: El Ángel del Señor (sd5), Los Tres Mensajeros (sd7), Abraham (sh07),
  Henoc (sh11), Isaac (sh12), Melquisedec (sh19), La Alianza del Fuego (c10).
- Isaac + Rebeca en campo: puede activar cualquier DÍA cuya condición cumpla, **ignorando
  el orden numérico** (excepción a la regla de orden).

## 7. HISTORIA y DIA (cartas externas al mazo)

- **HISTORIA:** se elige al construir el mazo, no ocupa espacios del mazo. Se coloca al
  inicio, visible para ambos, en **horizontal**. Define las 5 piezas requeridas (Victoria III).
- **DIA (×7):** externas. Apiladas dia1→dia7, siempre visible la del día actual. 1
  activación por turno, en orden, con requisitos + coste FD. Al activarse → Retirados y
  revela la siguiente. Activar dia7 = Victoria I.

## 8. Construcción del mazo

- **40 a 50 cartas** (rango). Debe incluir las **5 piezas** de la HISTORIA elegida
  (al menos 1 de cada).
- **Máximo 3 copias** por carta.
- Curva orientativa (para 40): ~10-12 TIERRA, ~15-18 SER, ~8-10 CONCEPTO, contando las
  piezas dentro de sus tipos.
- **+ 1 HISTORIA** y **+ 7 DIA**, fuera del mazo.

## 9. Tipos de carta (resumen; detalle por carta en el catálogo)

- **TIERRA** — produce FD al tapearse. Algunas especiales con efectos
  (`AL_ENTRAR`, `AL_TAPEARSE`, `AL_SER_DESTRUIDA`, pasivos, penalizaciones).
- **SER** (Divino / Humano / Animal) — permanente estacionario con `dur` (turnos de vida).
  Cuesta FD. Efectos `AL_ENTRAR` y `EFECTO_ACTIVADO` (paga `actCost`, 1/turno). Decae a
  Retirados cuando `dur` llega a 0.
- **CONCEPTO** — hechizo/trampa estilo Yu-Gi-Oh. Uso único → Retirados, o boca abajo como
  RESPUESTA en turno rival (con FD reservado).
- **DIA / HISTORIA** — ver §7.

## 10. Pendientes (TBD)

- **Multiplayer:** online en red vs local hotseat vs local-con-red-después.
- **Presentación 3D:** cámara, disposición física, animaciones.
- **Nombre definitivo** del proyecto y del "mazo" (mazo/libro/historia).
- **Resolución simultánea / empates** si ambos cumplen al mismo fin de turno.
- **Set de cartas "sacrificio"** para la regla `sacrificioUsed` (tras retirar La Venta de
  José; candidatas c15, c37).

> Nota: `sh17` Benjamín se deja tal cual en el catálogo. Su texto en `activado` describe un
> disparo de fin de turno (vuelve al mazo en vez de a Retirados), no un efecto activado por
> FD; se maneja como caso especial en el motor sin cambiar el dato.

### Decisiones cerradas (antes TBD)

- Activación de DÍA: modelo A (ver §6). Trampas CONCEPTO: 1 boca abajo a la vez.
- Mazo: 40-50 cartas, máx 3 copias por carta, incluye las 5 piezas.
- Victoria III: los SER pieza decaen normal (sin no-decae especial); h5 usa
  `on_piece_play`. Riesgo de Benjamín (dur=1) aceptado como parte del diseño.
