# Errata y decisiones — Catálogo v3.0 → v3.0-rev1

> Registro de las correcciones aplicadas al catálogo recibido (120 cartas, JSON v3.0) y
> de las decisiones de reglas tomadas al reconciliarlo con el diseño del campo.
> Catálogo corregido: `data/catalogo.v3.json`.

## Decisiones de reglas (confirmadas por el diseñador)

1. **Economía FD = modelo del diseño del campo, no el del JSON.**
   FD se resetea a 0 en el PRELUDIO propio (al destapear). El FD sin gastar **persiste
   durante el turno del rival** y es el único combustible de los CONCEPTO boca abajo
   (RESPUESTA). Se descarta la mecánica de "tapear tierras durante el turno rival".
2. **Máx TIERRAs en campo = 7** (override del `TIERRAs_maximas: 15`).
3. **dia7 (Victoria I):** además de DÍAs 1–6 + 3 TIERRAs, requiere 2 SER en campo: uno de
   coste **≤2** y otro de coste **≥3** (antes decía `SER<=2 + SER>=3`, imposible).
4. **Retirados = Descarte:** una sola zona (cementerio general).

## Decisiones confirmadas (ronda 2)

5. **Primer jugador no roba en su turno 1** (regla del diseño del campo; prevalece sobre
   el "Roba 1 carta" incondicional de GENESIS).
6. **`diaFreeUsed`:** se quita **Canaán** (su efecto no activa DÍA) y se añade
   **La Alianza del Fuego** (c10). Lista final: sd5, sd7, sh07, sh11, sh12, sh19, c10.
7. **Activación de DÍA = modelo A:** 1 activación base/turno pagando `coste`; las cartas de
   `diaFreeUsed` dan 1 activación EXTRA sin coste; la **condición siempre se exige**.
8. **Trampas CONCEPTO boca abajo:** máximo **1 a la vez** (1 zona CONCEPTO; campo sin cambios).
9. **Mazo: 40–50 cartas** (rango), debe incluir las 5 piezas, **máximo 3 copias por carta**.
10. **Victoria III:** los SER pieza **decaen normal** (sin regla no-decae). Riesgo de
    Benjamín (dur=1) aceptado. **h5** usa `modo_victoria: on_piece_play` (variación buscada).

## Erratas de datos aplicadas

- **Eliminada `t21` "Peniel"** — duplicado vacío de `t15` (sin `fd`/`efecto`).
- **dia7 condición** corregida en `dias[]` y en `catalogo_cartas.DIA[]`.
- **Conteo corregido:**
  - `TIERRA`: 21 → **20** (sin el stub).
  - `SER_HUMANO`: 29 → **31** (el original contaba de menos; el array tiene sh01–sh29 +
    `sh06b` Jafet + `sh12b` Esaú).
  - `TOTAL`: 120 → **121**. El 120 original resultaba de dos errores que se cancelaban
    (contaba t21 de más y SER_HUMANO de menos).
- `limites_campo.TIERRAs_maximas`: 15 → **7**.
- Texto del recurso FD y de PRELUDIO/RESPUESTA ajustado al modelo canónico.

## Pendientes / riesgos abiertos (no bloquean)

- **Balance Victoria III:** requiere 3 SER simultáneos (= ocupa las 3 ranuras SER) que
  además **decaen**. Caso extremo **Benjamín (h7, `dur=1`)**: entra y se va al siguiente
  PRELUDIO, casi imposible de mantener en campo. Revisar antes de balancear.
- **h5 (El Primer Fratricidio):** su 5ª pieza `La Maldición de la Tierra` es CONCEPTO de
  uso único; Victoria III se verifica **al entrar** (excepción a "al fin de ENTREGA").
  Requiere lógica especial.
- **Set de cartas "sacrificio"** para `sacrificioUsed`: definir qué cartas cuentan tras
  retirar "La Venta de José" (candidatas: `c15 El Sacrificio de Isaac`,
  `c37 La Prueba de Abraham`).
- **`sh17` Benjamín:** su campo `activado` describe un disparo al fin de turno (vuelve al
  mazo), no un efecto activado por FD. Reclasificar al implementar.
- **Coste de `actCost: 0`** (sd2, sa5): confirmar que es activación gratuita intencional.
