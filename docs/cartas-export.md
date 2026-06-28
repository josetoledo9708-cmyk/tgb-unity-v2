# The Great Book — Catálogo de cartas (datos para diseño)

Exportado de `data/catalogo.v3.json` (v3.0-rev1). Cada entrada trae los datos de juego de la carta para crear su diseño/arte.

**Totales:** DIA 7, TIERRA 20, SER_DIVINO 9, SER_HUMANO 31, SER_ANIMAL 5, CONCEPTO 42, HISTORIA 7 — TOTAL 121.

## HISTORIA (condiciones de victoria, externas al mazo)

### La Caída del Edén  `h1`
- Génesis: Génesis 3
- Modo victoria: field_at_entrega
- 5 piezas: El Jardín del Edén, El Árbol del Fruto Prohibido, Adán, Eva, La Serpiente

### El Diluvio Universal  `h2`
- Génesis: Génesis 6-9
- Modo victoria: field_at_entrega
- 5 piezas: El Monte Ararat, Egipto, Noé, Sem, Jafet

### La Alianza con Abraham  `h3`
- Génesis: Génesis 12-18
- Modo victoria: field_at_entrega
- 5 piezas: Hebrón/Mambré, Canaán, Abraham, Sara, Melquisedec

### El Destino de Sodoma y Gomorra  `h4`
- Génesis: Génesis 18-19
- Modo victoria: field_at_entrega
- 5 piezas: Sodoma, Gomorra, Abraham, Lot, Los Ángeles de Sodoma

### El Primer Fratricidio  `h5`
- Génesis: Génesis 4
- Modo victoria: on_piece_play
- 5 piezas: El Jardín del Edén, La Tierra de Nod, Caín, Abel, La Maldición de la Tierra
- Nota: La Maldición de la Tierra es un CONCEPTO que actúa como quinta pieza. Debe jugarse de última. La Victoria III se verifica AL JUGAR esa carta (con las otras 4 piezas en campo), antes de que su efecto se resuelva.

### La Escalera al Cielo  `h6`
- Génesis: Génesis 25-33
- Modo victoria: field_at_entrega
- 5 piezas: Betel, Canaán, Jacob/Israel, Esaú, Los Ángeles de la Escalera

### José, el Salvador de Egipto  `h7`
- Génesis: Génesis 37-45
- Modo victoria: field_at_entrega
- 5 piezas: Egipto, La Tierra de Gosén, José, El Faraón, Benjamín

## DÍA (7, externas al mazo, se activan en orden)

### La Luz  `dia1`
- Coste FD: 1
- Condición: 1 TIERRA
- Efecto: Sin recompensa

### El Firmamento  `dia2`
- Coste FD: 1
- Condición: 2 TIERRAs
- Efecto: Juega 1 TIERRA extra

### La Tierra y los Mares  `dia3`
- Coste FD: 2
- Condición: 3 TIERRAs + CONCEPTO
- Efecto: Añade a mano o juega al campo 1 TIERRA (desde mazo o Retirados)

### Los Astros  `dia4`
- Coste FD: 2
- Condición: 3 TIERRAs + SER coste>=3
- Efecto: Mira top 3, añade 1 a mano, resto al fondo, +1 FD

### Los Seres del Agua  `dia5`
- Coste FD: 3
- Condición: 2 SER + 2 TIERRAs sin tapear
- Efecto: Regresa 1 SER de Retirados

### Los Animales y el Hombre  `dia6`
- Coste FD: 3
- Condición: 2 SER + 4 TIERRAs + CONCEPTO
- Efecto: Busca 1 SER en mazo

### El Descanso  `dia7`
- Coste FD: 4
- Condición: DÍAs 1-6 + 3 TIERRAs + 2 SER (uno coste<=2 y otro coste>=3)
- Efecto: VICTORIA_I

## TIERRA

### El Jardín del Edén  `t01`
- Genera FD: 1
- Especial: True
- Efecto: PASIVO: tus SER ganan +1 turno al entrar. PENALIZACIÓN: si hay un SER enemigo en campo, el rival roba 1 carta.

### El Árbol del Fruto Prohibido  `t02`
- Genera FD: 1
- Especial: True
- Efecto: AL TAPEARSE: busca en tu mazo el CONCEPTO 'La Expulsión del Edén' o 'El Fruto Prohibido' y añádelo a tu mano. Si no lo haces, busca cualquier CONCEPTO pero descarta 2 cartas.

### El Monte Ararat  `t03`
- Genera FD: 1
- Especial: False
- Efecto: Solo genera 1 FD.

### Babel  `t04`
- Genera FD: 1
- Especial: True
- Efecto: AL TAPEARSE: retrasa 1 DÍA del rival 1 turno. PENALIZACIÓN: descarta 1 carta.

### Canaán  `t05`
- Genera FD: 1
- Especial: True
- Efecto: AL ENTRAR: puedes poner en juego 1 TIERRA 'Hebrón' o 'Salem' desde tu mano o mazo (una vez por partida).

### Hebrón/Mambré  `t06`
- Genera FD: 1
- Especial: False
- Efecto: Solo genera 1 FD.

### Salem  `t07`
- Genera FD: 1
- Especial: False
- Efecto: Solo genera 1 FD.

### Sodoma  `t08`
- Genera FD: 2
- Especial: True
- Turnos hasta autodestrucción: 3
- Efecto: Genera 2 FD. PASIVO: se destruye después de 3 turnos. AL SER DESTRUIDA: el rival roba 2 cartas. Mientras Lot esté en campo, esta TIERRA no se destruye automáticamente.

### Gomorra  `t09`
- Genera FD: 3
- Especial: True
- Turnos hasta autodestrucción: 2
- Efecto: Genera 3 FD. PASIVO: se destruye después de 2 turnos. AL SER DESTRUIDA: el rival roba 3 cartas. Mientras Lot esté en campo, esta TIERRA no se destruye automáticamente.

### Betel  `t10`
- Genera FD: 1
- Especial: True
- Efecto: AL TAPEARSE: mira las 2 cartas superiores del mazo y reordénalas.

### La Cueva de Macpelá  `t11`
- Genera FD: 1
- Especial: True
- Efecto: AL TAPEARSE: regresa 1 SER de Retirados a la mano. PENALIZACIÓN: descarta 1 carta.

### Egipto  `t12`
- Genera FD: 2
- Especial: True
- Efecto: AL ENTRAR: si El Monte Ararat está en campo, genera 2 FD adicionales este turno.

### La Tierra de Gosén  `t13`
- Genera FD: 1
- Especial: True
- Efecto: AL ENTRAR: todos tus SER en campo quedan protegidos de destrucción hasta tu siguiente turno.

### La Tierra de Nod  `t14`
- Genera FD: 1
- Especial: True
- Efecto: AL ENTRAR: si Caín está en campo, roba 1 carta y Caín gana +1 turno de duración. PASIVO: mientras Caín esté en campo, los SER del rival entran con -1 turno de duración. AL SALIR de Caín (si esta carta sigue en campo), La Tierra de Nod es enviada a Retirados.

### Peniel  `t15`
- Genera FD: 1
- Especial: True
- Efecto: AL ENTRAR: si Jacob/Israel está en campo, roba 1 carta.

### Dotán  `t16`
- Genera FD: 1
- Especial: True
- Efecto: AL ENTRAR: busca 1 SER 'José' en tu mazo y añádelo a tu mano (una vez por partida).

### Río Pisón  `t17`
- Genera FD: 1
- Especial: True
- Efecto: AL TAPEARSE: ganas +1 FD adicional. PASIVO: si El Jardín del Edén está en campo, esta TIERRA genera 2 FD en lugar de 1.

### Río Gehón  `t18`
- Genera FD: 1
- Especial: True
- Efecto: AL TAPEARSE: mira 1 carta del fondo de tu mazo.

### Río Tigris  `t19`
- Genera FD: 1
- Especial: True
- Efecto: AL ENTRAR: puedes descartar 1 carta para robar 1.

### Río Éufrates  `t20`
- Genera FD: 1
- Especial: True
- Efecto: AL ENTRAR: si tienes 3 o más TIERRAs, ganas +1 FD.

## SER · DIVINO

### Los Querubines  `sd1`
- Coste FD: 5
- Duración (turnos): 4
- Coste activación FD: 3
- Al entrar: Bloquea todos los efectos del rival durante 1 turno.
- Efecto activado: Protege 1 SER propio de destrucción hasta tu próximo turno.

### Los Ángeles de la Escalera  `sd2`
- Coste FD: 3
- Duración (turnos): 2
- Coste activación FD: 0
- Al entrar: Roba 1 carta. El rival descarta 1 carta.
- Efecto activado: Genera +1 FD.

### Los Ángeles de Sodoma  `sd3`
- Coste FD: 5
- Duración (turnos): 2
- Coste activación FD: 2
- Al entrar: Destruye 2 TIERRAs en cualquier lado del campo.
- Efecto activado: Ambos jugadores añaden 1 SER del mazo a su mano.

### El Ángel del Sacrificio  `sd4`
- Coste FD: 3
- Duración (turnos): 3
- Coste activación FD: 2
- Al entrar: Cancela efecto que destruya 1 SER propio.
- Efecto activado: Regresa 1 SER de Retirados a la mano.

### El Ángel del Señor  `sd5`
- Coste FD: 4
- Duración (turnos): 2
- Coste activación FD: 2
- Al entrar: Roba 2 cartas.
- Efecto activado: Activa 1 DÍA sin coste (condición requerida).

### El Ser que Luchó con Jacob  `sd6`
- Coste FD: 5
- Duración (turnos): 2
- Coste activación FD: 3
- Al entrar: Todos los SER del rival vuelven a su mano.
- Efecto activado: Niega el próximo efecto activado del rival.

### Los Tres Mensajeros  `sd7`
- Coste FD: 4
- Duración (turnos): 2
- Coste activación FD: 2
- Al entrar: Roba 2 cartas.
- Efecto activado: Activa 1 DÍA sin coste (condición requerida).

### El Ángel de Agar  `sd8`
- Coste FD: 2
- Duración (turnos): 2
- Coste activación FD: 1
- Al entrar: Busca el CONCEPTO 'La Promesa a Agar' y añádelo a tu mano.
- Efecto activado: Genera +1 FD.

### El Ángel de la Torre  `sd9`
- Coste FD: 3
- Duración (turnos): 2
- Coste activación FD: 2
- Al entrar: El rival no puede jugar CONCEPTOS este turno.
- Efecto activado: Cancela el efecto de 1 TIERRA especial rival.

## SER · HUMANO

### Adán  `sh01`
- Coste FD: 3
- Duración (turnos): 4
- Coste activación FD: 2
- Al entrar: Si Eva está en campo roba 1 carta; si no, búscala en el mazo y añádela a tu mano.
- Efecto activado: Mira las 2 cartas superiores del mazo y acomódalas en el orden que prefieras.

### Eva  `sh02`
- Coste FD: 3
- Duración (turnos): 4
- Coste activación FD: 2
- Al entrar: Si Adán está en campo roba 1 carta; si no, búscalo en el mazo y añádelo a tu mano.
- Efecto activado: El rival revela su mano completa, pero tú debes descartar 1 carta.

### Caín  `sh03`
- Coste FD: 2
- Duración (turnos): 3
- Coste activación FD: 2
- Al entrar: Envía 1 TIERRA del campo a Retirados.
- Efecto activado: Añade 2 cartas CONCEPTO del mazo a tu mano, luego descarta 1 de las 2.

### Abel  `sh04`
- Coste FD: 2
- Duración (turnos): 3
- Coste activación FD: 1
- Al entrar: Añade La Ofrenda de Abel a tu mano desde el mazo.
- Efecto activado: Genera +2 FD.

### Noé  `sh05`
- Coste FD: 5
- Duración (turnos): 5
- Coste activación FD: 3
- Al entrar: Añade La Paloma a tu mano desde el mazo.
- Efecto activado: Roba 2 cartas.

### Sem  `sh06`
- Coste FD: 2
- Duración (turnos): 3
- Coste activación FD: 1
- Al entrar: Si Noé está en campo, Noé gana +1 turno de duración.
- Efecto activado: Genera +2 FD.

### Jafet  `sh06b`
- Coste FD: 1
- Duración (turnos): 2
- Coste activación FD: 1
- Al entrar: Si Noé o Sem están en campo, roba 1 carta.
- Efecto activado: Roba 1 carta.

### Abraham  `sh07`
- Coste FD: 5
- Duración (turnos): 5
- Coste activación FD: 2
- Al entrar: Busca 1 SER en el mazo y añádelo a tu mano. Si eliges a Lot, puedes ponerlo directamente en campo (pagando su coste o no, según reglas).
- Efecto activado: Activa 1 DÍA sin coste (condición requerida).

### Sara  `sh08`
- Coste FD: 3
- Duración (turnos): 4
- Coste activación FD: 4
- Al entrar: Si Abraham está en campo, roba 2 cartas.
- Efecto activado: Ambos jugadores descartan 1 carta.

### Lot  `sh09`
- Coste FD: 3
- Duración (turnos): 4
- Coste activación FD: 2
- Al entrar: Anula cualquier efecto que destruya una TIERRA hasta tu próximo turno. Si Abraham está en campo, esta protección dura 2 turnos.
- Efecto activado: Ambos jugadores roban 1 carta. Si Abraham está en campo, ganas +1 FD adicional.

### Ismael  `sh10`
- Coste FD: 3
- Duración (turnos): 4
- Coste activación FD: 2
- Al entrar: Genera +1 FD.
- Efecto activado: Roba 1 carta.

### Henoc  `sh11`
- Coste FD: 3
- Duración (turnos): 3
- Coste activación FD: 2
- Al entrar: Activa 1 DÍA sin pagar FD (condición requerida).
- Efecto activado: Roba 1 carta.
- Nota: Solo exime el coste FD, la condición del DÍA sigue siendo requerida.

### Isaac  `sh12`
- Coste FD: 4
- Duración (turnos): 4
- Coste activación FD: 2
- Al entrar: Roba 2 cartas.
- Efecto activado: Activa 1 DÍA sin coste (condición requerida). Si Rebeca está en campo, puedes activar cualquier DÍA cuya condición cumpla, ignorando el orden numérico.

### Esaú  `sh12b`
- Coste FD: 3
- Duración (turnos): 4
- Coste activación FD: 2
- Al entrar: Si Jacob/Israel está en campo, roba 1 carta.
- Efecto activado: Destruye 1 SER rival de coste <=2.

### Jacob/Israel  `sh13`
- Coste FD: 5
- Duración (turnos): 5
- Coste activación FD: 2
- Al entrar: Añade 'La Lucha de Jacob con Dios' a tu mano desde el mazo.
- Efecto activado: Elige 1 SER humano rival. Durante 1 turno, ese SER se considera bajo tu control (no puede atacar ni activar efectos, solo bloquea).

### Rebeca  `sh14`
- Coste FD: 3
- Duración (turnos): 4
- Coste activación FD: 2
- Al entrar: Roba 1 carta. Si Isaac está en campo, roba 2 cartas en su lugar e Isaac gana +1 turno de duración (si está en campo).
- Efecto activado: Busca 1 SER en el mazo.

### Raquel  `sh15`
- Coste FD: 3
- Duración (turnos): 4
- Coste activación FD: 2
- Al entrar: Si Jacob está en campo, roba 2 cartas. Si Lea no está en campo, puedes buscarla en el mazo y añadirla a tu mano.
- Efecto activado: Genera +1 FD.

### José  `sh16`
- Coste FD: 5
- Duración (turnos): 5
- Coste activación FD: 2
- Al entrar: Si El Faraón y Egipto están en campo, ambas cartas no podrán ser destruidas por ningún efecto por el resto de la partida.
- Efecto activado: Mira las 5 cartas superiores del mazo rival. Si Benjamín está en campo, puedes devolver 1 SER de Retirados a tu mano.

### Benjamín  `sh17`
- Coste FD: 3
- Duración (turnos): 1
- Coste activación FD: 1
- Al entrar: Roba 1 carta.
- Efecto activado: Cuando esta carta termina su turno en campo, regresa al mazo en lugar de ir a Retirados. Si José está en campo, roba 1 carta adicional al regresar al mazo.

### El Faraón  `sh18`
- Coste FD: 5
- Duración (turnos): 4
- Coste activación FD: 4
- Al entrar: Roba 2 cartas.
- Efecto activado: Genera FD igual a la mitad de tus TIERRAs en campo (máx 4 FD).

### Melquisedec  `sh19`
- Coste FD: 4
- Duración (turnos): 3
- Coste activación FD: 2
- Al entrar: Activa 1 DÍA sin coste (condición requerida).
- Efecto activado: Genera +3 FD.

### Putifar  `sh20`
- Coste FD: 3
- Duración (turnos): 3
- Coste activación FD: 2
- Al entrar: Roba 1 carta.
- Efecto activado: Bloquea el efecto activado de 1 SER rival este turno.

### Lea  `sh21`
- Coste FD: 3
- Duración (turnos): 4
- Coste activación FD: 2
- Al entrar: Si Jacob está en campo, ambos jugadores descartan 1 carta y tú ganas +1 FD. Si Raquel está en campo, Lea pierde 1 turno de duración.
- Efecto activado: El rival revela su mano. Tú eliges 1 carta y la pones en el fondo de su mazo.

### Set  `sh22`
- Coste FD: 2
- Duración (turnos): 3
- Coste activación FD: 1
- Al entrar: Si Abel está en Retirados, regresa 1 SER de Retirados a tu mano.
- Efecto activado: Roba 1 carta.

### Matusalén  `sh23`
- Coste FD: 4
- Duración (turnos): 6
- Coste activación FD: 2
- Al entrar: Todos tus SER ganan +1 turno de duración.
- Efecto activado: Gana +2 FD.

### Judá  `sh24`
- Coste FD: 3
- Duración (turnos): 4
- Coste activación FD: 2
- Al entrar: Si Tamar está en campo, roba 2 cartas.
- Efecto activado: Destruye 1 SER rival de coste <=2.

### Rubén  `sh25`
- Coste FD: 3
- Duración (turnos): 4
- Coste activación FD: 2
- Al entrar: Protege a 1 SER propio de destrucción este turno.
- Efecto activado: Ambos jugadores roban 1 carta.

### Dina  `sh26`
- Coste FD: 2
- Duración (turnos): 3
- Coste activación FD: 1
- Al entrar: El rival descarta 1 carta al azar.
- Efecto activado: Mira la mano del rival.

### Tamar  `sh27`
- Coste FD: 3
- Duración (turnos): 3
- Coste activación FD: 2
- Al entrar: Si Judá está en campo, roba 1 carta y él gana +1 turno.
- Efecto activado: El rival revela su mano, tú descartas 1 de ellas.

### Agar  `sh28`
- Coste FD: 2
- Duración (turnos): 3
- Coste activación FD: 1
- Al entrar: Si Abraham o Sara están en campo, ganas +2 FD.
- Efecto activado: Busca 1 TIERRA en el mazo.

### La Esposa de Putifar  `sh29`
- Coste FD: 3
- Duración (turnos): 2
- Coste activación FD: 2
- Al entrar: Elige 1 SER rival. Ese SER no puede activar efectos este turno.
- Efecto activado: Si José está en campo, él pierde 1 turno de duración.

## SER · ANIMAL

### La Serpiente  `sa1`
- Coste FD: 2
- Duración (turnos): 3
- Coste activación FD: 1
- Al entrar: El rival descarta 1 carta aleatoria.
- Efecto activado: Niega el efecto activado de 1 SER rival.

### El Cuervo  `sa2`
- Coste FD: 1
- Duración (turnos): 2
- Coste activación FD: 1
- Al entrar: Mira la carta superior del mazo rival.
- Efecto activado: Roba 1 carta.

### La Paloma  `sa3`
- Coste FD: 1
- Duración (turnos): 2
- Coste activación FD: 1
- Efecto activado: Genera +1 FD.
- Al salir: Roba 1 carta.

### El Carnero del Zarzal  `sa4`
- Coste FD: 2
- Duración (turnos): 3
- Coste activación FD: 1
- Al entrar: Cancela el próximo efecto que destruya 1 SER propio.
- Efecto activado: Genera +1 FD.

### Criaturas Marinas  `sa5`
- Coste FD: 1
- Duración (turnos): 2
- Coste activación FD: 0
- Al entrar: Si el DÍA 5 ha sido activado esta partida, roba 1 carta.
- Efecto activado: Una vez por turno, puedes devolver esta carta a tu mano en lugar de enviarla a Retirados al final de su duración.

## CONCEPTO

### El Soplo de Vida  `c01`
- Coste FD: 3
- Subtipo: Uso único
- Efecto: Regresa 1 SER de Retirados al campo.

### La Expulsión del Edén  `c02`
- Coste FD: 3
- Subtipo: Uso único
- Efecto: Devuelve hasta 2 SER del rival a su mano. El rival elige cuáles. Si entre los devueltos hay algún Adán o Eva, el rival además descarta 1 carta.

### La Espada de Fuego Giratoria  `c03`
- Coste FD: 2
- Subtipo: RESPUESTA
- Efecto: Cancela efecto que permita activar DÍA o recuperar TIERRA.

### La Ofrenda de Abel  `c04`
- Coste FD: 1
- Subtipo: Uso único
- Efecto: Genera +2 FD.

### La Maldición de la Tierra  `c05`
- Coste FD: 2
- Subtipo: Uso único
- Efecto: Las TIERRAs del rival generan 0 FD durante 1 turno. Si Caín está en campo, la duración aumenta a 2 turnos.

### El Arca de Noé  `c06`
- Coste FD: 3
- Subtipo: Uso único
- Efecto: Todos tus SER en campo quedan protegidos de destrucción este turno.

### El Diluvio  `c07`
- Coste FD: 5
- Subtipo: Uso único
- Efecto: Destruye todos los SER de ambos jugadores. No puede jugarse el mismo turno que La Rama de Olivo.

### La Rama de Olivo  `c08`
- Coste FD: 2
- Subtipo: Uso único
- Efecto: Coloca 1 TIERRA directamente en campo desde tu mano, mazo o Retirados. No puedes jugar TIERRAs el siguiente turno. No puede jugarse el mismo turno que El Diluvio.

### La Llamada de Abram  `c09`
- Coste FD: 2
- Subtipo: Uso único
- Efecto: Busca 1 TIERRA en el mazo y añádela a tu mano.

### La Alianza del Fuego  `c10`
- Coste FD: 3
- Subtipo: Uso único
- Efecto: Activa 1 DÍA sin coste (condición requerida). Roba 1 carta.

### La Destrucción de Sodoma  `c11`
- Coste FD: 3
- Subtipo: Uso único
- Efecto: Destruye 1 TIERRA del rival. Si es Sodoma o Gomorra, sus efectos AL SER DESTRUIDA se disparan.

### El Arco Iris / La Alianza de Noé  `c12`
- Coste FD: 2
- Subtipo: RESPUESTA
- Efecto: Cancela efecto que destruya TIERRAs o envíe SER a Retirados. Roba 1 carta.

### La Confusión de Lenguas  `c13`
- Coste FD: 4
- Subtipo: Uso único
- Efecto: Todos los SER de ambos jugadores pierden sus efectos activados hasta el próximo Preludio.

### La Dispersión de los Pueblos  `c14`
- Coste FD: 3
- Subtipo: Uso único
- Efecto: El rival descarta 2 cartas.

### El Sacrificio de Isaac  `c15`
- Coste FD: 2
- Subtipo: Uso único
- Efecto: Envía 1 SER propio a Retirados. Roba 3 cartas. No puede usarse el mismo turno que otra carta de sacrificio.

### El Sueño de la Escalera  `c16`
- Coste FD: 2
- Subtipo: Uso único
- Efecto: Mira las 3 cartas superiores del mazo y reordénalas. Roba 1.

### La Túnica de Colores  `c17`
- Coste FD: 1
- Subtipo: Uso único
- Efecto: El próximo SER que juegues este turno entra con +2 turnos de duración.

### El Pozo de José  `c18`
- Coste FD: 2
- Subtipo: Uso único
- Efecto: Envía 1 SER rival al fondo de su mazo en lugar de a Retirados.

### La Lucha de Jacob con Dios  `c19`
- Coste FD: 3
- Subtipo: RESPUESTA
- Efecto: Niega cualquier efecto rival. Tu SER de mayor duración pierde 1 turno.

### La Bendición de Jacob  `c20`
- Coste FD: 2
- Subtipo: Uso único
- Efecto: Todos tus SER en campo recuperan 1 turno de duración.

### El Perdón de José  `c21`
- Coste FD: 2
- Subtipo: RESPUESTA
- Efecto: Cancela destrucción o envío de SER a Retirados. Roba 1 carta.

### José se da a Conocer  `c22`
- Coste FD: 3
- Subtipo: Uso único
- Efecto: Regresa 1 SER de Retirados a la mano. Roba 1 carta.

### El Robo de la Bendición  `c23`
- Coste FD: 2
- Subtipo: Uso único
- Efecto: Copia el último efecto AL ENTRAR del rival.

### La Promesa de la Tierra  `c24`
- Coste FD: 2
- Subtipo: Uso único
- Efecto: Busca 1 TIERRA en el mazo y ponla directamente en campo.

### El Juramento de las Estrellas  `c25`
- Coste FD: 3
- Subtipo: Uso único
- Efecto: Todos tus SER en campo ganan +2 turnos de duración.

### El Sueño del Faraón  `c26`
- Coste FD: 2
- Subtipo: Uso único
- Efecto: Mira las 5 cartas superiores del mazo rival. Descarta 1 de ellas.

### La Copa de Benjamín  `c27`
- Coste FD: 2
- Subtipo: Uso único
- Efecto: Roba 2 cartas. El rival roba 1 carta.

### El Fruto Prohibido  `c28`
- Coste FD: 2
- Subtipo: Uso único
- Efecto: Mira las 3 cartas superiores de tu mazo. Añade 1 a tu mano y envía el resto a Retirados.

### El Velo de la Noche  `c29`
- Coste FD: 1
- Subtipo: RESPUESTA
- Efecto: Cancela la activación de un DÍA por parte del rival. Roba 1 carta.

### El Sueño de los Haces  `c30`
- Coste FD: 2
- Subtipo: Uso único
- Efecto: Mira las 5 cartas superiores de tu mazo. Puedes poner 1 SER de ellas en tu mano y devolver el resto en cualquier orden al fondo de tu mazo. Si ese SER es José, además roba 1 carta.

### La Marca de Caín  `c31`
- Coste FD: 1
- Subtipo: Uso único
- Efecto: Protege a 1 SER propio de ser destruido o enviado a Retirados este turno.

### La Promesa del Arco Iris  `c32`
- Coste FD: 2
- Subtipo: RESPUESTA
- Efecto: Cancela cualquier efecto que destruya TIERRAs. Roba 1 carta.

### El Sueño del Copero  `c33`
- Coste FD: 1
- Subtipo: Uso único
- Efecto: Mira las 3 cartas superiores del mazo rival. Pon 1 de ellas en el fondo.

### El Sueño del Panadero  `c34`
- Coste FD: 1
- Subtipo: Uso único
- Efecto: El rival descarta 1 carta al azar. Tú robas 1 carta.

### La Promesa a Agar  `c35`
- Coste FD: 2
- Subtipo: Uso único
- Efecto: Busca 1 SER en el mazo y añádelo a tu mano. Si es Ismael, gana +1 FD.

### El Pozo de Agar  `c36`
- Coste FD: 2
- Subtipo: Uso único
- Efecto: Regresa 1 SER de Retirados a tu mano. Si es Agar o Ismael, roba 1 carta extra.

### La Prueba de Abraham  `c37`
- Coste FD: 2
- Subtipo: Uso único
- Efecto: Sacrifica 1 SER propio. Busca 1 CONCEPTO en el mazo y añádelo a tu mano.

### El Nacimiento de Isaac  `c38`
- Coste FD: 2
- Subtipo: Uso único
- Efecto: Roba 2 cartas. Si Abraham o Sara están en campo, roba 1 adicional.

### El Viaje del Siervo  `c39`
- Coste FD: 2
- Subtipo: Uso único
- Efecto: Busca 1 TIERRA en el mazo y añádela a tu mano. Luego puedes jugarla gratis este turno.

### La Piedra de Jacob  `c40`
- Coste FD: 1
- Subtipo: Uso único
- Efecto: Coloca 1 TIERRA de tu mano directamente en campo (no consume tu jugada de TIERRA).

### Los Sueños de José  `c41`
- Coste FD: 2
- Subtipo: Uso único
- Efecto: Mira las 5 cartas superiores de tu mazo. Puedes poner 1 SER en tu mano y el resto al fondo en cualquier orden.

### El Censo de Jacob  `c42`
- Coste FD: 3
- Subtipo: Uso único
- Efecto: Todos tus SER en campo recuperan 2 turnos de duración. Roba 1 carta.
