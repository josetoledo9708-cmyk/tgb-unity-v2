using System.Collections.Generic;
using System.Linq;
using Game.Core.Model;

namespace Game.Core.Data
{
    /// <summary>
    /// v0.01 — Mazos PREDETERMINADOS por HISTORIA, escritos a mano para PROBAR el catálogo completo.
    /// Cada mazo lleva las 5 piezas de su historia y, alrededor, las cartas cuyos efectos interactúan
    /// con ellas (parejas, sinergias y trampas). Entre las 7 listas se toca todo el catálogo, así que
    /// jugando una partida por historia se ejercitan todas las cartas y sus interacciones.
    ///
    /// Cada entrada es un id; repetirlo = más copias (máximo <see cref="DeckValidator.MaxCopies"/>).
    /// El relleno hasta el tamaño pedido lo hace <see cref="SampleDeckBuilder"/>.
    /// </summary>
    public static class StoryDecks
    {
        /// <summary>h1 — La Caída del Edén. Piezas: Jardín, Árbol, Adán, Eva, La Serpiente.
        /// Interacciones: pareja Adán/Eva, el Jardín da +1 duración al entrar y Río Pisón pasa a 2 FD
        /// con él en campo, Adán y Betel ordenan el tope del mazo, el Fruto Prohibido cava.</summary>
        private static readonly string[] H1 =
        {
            "t01", "t01", "t02", "t02", "sh01", "sh01", "sh02", "sh02", "sa1", "sa1", // piezas
            "t17", "t17", "t18", "t19", "t20",            // ríos: sinergia de FD con el Jardín
            "t14", "t10",                                  // Nod (Caín) / Betel (ordenar tope)
            "sh22", "sh22",                                // Set: hijo de Adán y Eva
            "sh03", "sh04",                                // Caín y Abel: la rivalidad nacida del Edén
            "c28", "c28", "c02", "c05", "c31",             // Fruto / Expulsión / Maldición / Marca
            "c16", "c19", "c04", "c01",                    // orden, RESPUESTA, FD, revivir SER
        };

        /// <summary>h2 — El Diluvio Universal. Piezas: Ararat, Egipto, Noé, Sem, Jafet.
        /// Interacciones: Sem alarga a Noé, Egipto da FD extra con el Ararat en campo, cuervo y paloma
        /// acompañan al Arca, y El Diluvio se enfrenta a sus dos RESPUESTAS.</summary>
        private static readonly string[] H2 =
        {
            "t03", "t03", "t12", "t12", "sh05", "sh05", "sh06", "sh06", "sh06b", "sh06b", // piezas
            "sa2", "sa2", "sa3", "sa3",                    // El Cuervo y La Paloma
            "sa5", "sh23", "sh11",                         // Criaturas Marinas / Matusalén / Henoc
            "c06", "c06", "c07", "c07",                    // El Arca protege; El Diluvio arrasa
            "c08", "c08", "c12", "c12", "c32",             // Rama de Olivo + RESPUESTAS anti-destrucción
            "t04", "t18", "t20",                           // Babel / ríos
            "c13", "c14",                                  // Confusión de Lenguas / Dispersión
        };

        /// <summary>h3 — La Alianza con Abraham. Piezas: Hebrón, Canaán, Abraham, Sara, Melquisedec.
        /// Interacciones: Canaán pone en campo Hebrón o Salem, Agar e Ismael traen sus dos cartas
        /// propias, y El Nacimiento de Isaac premia tener a Abraham o Sara en campo.</summary>
        private static readonly string[] H3 =
        {
            "t06", "t06", "t05", "t05", "sh07", "sh07", "sh08", "sh08", "sh19", "sh19", // piezas
            "t07", "t07",                                  // Salem: el otro objetivo de Canaán
            "sh28", "sh28", "sh10", "sh10",                // Agar / Ismael
            "sh12", "sd8", "sd7",                          // Isaac / Ángel de Agar / Tres Mensajeros
            "c35", "c35", "c36", "c38", "c38",             // Promesa a Agar / Pozo de Agar / Isaac
            "c09", "c24", "c39", "c40",                    // tutores de TIERRA
            "c25", "c10",                                  // Juramento de las Estrellas / Alianza del Fuego
        };

        /// <summary>h4 — El Destino de Sodoma y Gomorra. Piezas: Sodoma, Gomorra, Abraham, Lot, Ángeles.
        /// Interacciones: Sodoma y Gomorra se autodestruyen salvo con Lot en campo y al caer hacen robar
        /// al rival; La Destrucción de Sodoma choca con las RESPUESTAS anti-destrucción.</summary>
        private static readonly string[] H4 =
        {
            "t08", "t08", "t09", "t09", "sh07", "sh07", "sh09", "sh09", "sd3", "sd3", // piezas
            "sd1", "sd5",                                  // Querubines / Ángel del Señor
            "c11", "c11", "c32", "c32", "c12",             // destruir TIERRA vs. quien lo cancela
            "c31", "c15", "c37",                           // Marca de Caín / sacrificios
            "sd4", "sa4",                                  // Ángel del Sacrificio / Carnero del Zarzal
            "t11", "t17", "t19",                           // Macpelá (Abraham) / ríos
            "c20", "c42",                                  // recuperar duración
            "c03", "c29",                                  // RESPUESTAS que cancelan activar un DÍA
        };

        /// <summary>h5 — El Primer Fratricidio. Piezas: Jardín, Nod, Caín, Abel, La Maldición.
        /// Interacciones: Nod alarga a Caín y le deja robar, Caín destruye TIERRA, la Ofrenda de Abel
        /// da FD y la Marca de Caín protege; Set llega tras la muerte de Abel.</summary>
        private static readonly string[] H5 =
        {
            "t01", "t01", "t14", "t14", "sh03", "sh03", "sh04", "sh04", "c05", "c05", // piezas
            "sh22", "sh22", "sh01", "sh02",                // Set / Adán / Eva
            "c04", "c04", "c31", "c31",                    // Ofrenda de Abel / Marca de Caín
            "c01", "c22", "c36",                           // regresar SER de Retirados
            "t02", "t17", "t18", "t20",                    // Árbol + ríos
            "c12", "c19",                                  // RESPUESTAS
            "c14", "c34",                                  // descarte del rival
        };

        /// <summary>h6 — La Escalera al Cielo. Piezas: Betel, Canaán, Jacob, Esaú, Ángeles de la Escalera.
        /// Interacciones: Betel ordena el tope y El Sueño de la Escalera lo repite; Esaú manda SER a
        /// Retirados y El Perdón de José lo cancela; Raquel, Lea, Rubén y Dina orbitan a Jacob.</summary>
        private static readonly string[] H6 =
        {
            "t10", "t10", "t05", "t05", "sh13", "sh13", "sh12b", "sh12b", "sd2", "sd2", // piezas
            "sh15", "sh15", "sh21", "sh21",                // Raquel y Lea
            "sh14", "sh25", "sh26", "sh12",                // Rebeca / Rubén / Dina / Isaac
            "sd6", "sd9",                                  // Ser que Luchó con Jacob / Ángel de la Torre
            "c16", "c16", "c23", "c40", "c40",             // Escalera / Robo de la Bendición / Piedra
            "c19", "c21",                                  // RESPUESTAS
            "c20", "c42", "t15",                           // Bendición / Censo / Peniel
        };

        /// <summary>h7 — José, el Salvador de Egipto. Piezas: Egipto, Gosén, José, El Faraón, Benjamín.
        /// Interacciones: José y Benjamín se buscan entre sí, Dotán tutorea a José, los tres sueños
        /// cavan y espían, y La Túnica de Colores alarga al siguiente SER que juegues.</summary>
        private static readonly string[] H7 =
        {
            "t12", "t12", "t13", "t13", "sh16", "sh16", "sh18", "sh18", "sh17", "sh17", // piezas
            "t16", "t16",                                  // Dotán: busca a José
            "sh20", "sh29", "sh24", "sh27",                // Putifar / su esposa / Judá / Tamar
            "c41", "c41", "c30", "c30", "c33", "c26",      // los sueños: cava y espía
            "c17", "c17", "c18", "c27", "c22",             // Túnica / Pozo de José / Copa / se da a conocer
            "c21", "c34", "t19",                           // Perdón de José / Panadero / Tigris
        };

        // ---------------- VARIANTE B ----------------
        // Segundo mazo por historia, con cartas DISTINTAS a las de su variante A. Sirve para que en
        // una misma partida cada jugador lleve un conjunto diferente y se vean interacciones cruzadas
        // (P0 juega la A, P1 la B).

        /// <summary>h1-B — Edén por control: la Serpiente y el Árbol acompañados de cartas que
        /// castigan al rival (descarte, bloqueo de efectos) en vez de la rampa de ríos de la A.</summary>
        private static readonly string[] H1B =
        {
            "t01", "t01", "t02", "t02", "sh01", "sh01", "sh02", "sh02", "sa1", "sa1", // piezas
            "t11", "t11", "t15", "t16",                    // Macpelá / Peniel / Dotán
            "sh26", "sh26", "sh22",                        // Dina / Set
            "sd1", "sd1", "sd9",                           // Querubines (bloquean efectos) / Ángel de la Torre
            "c13", "c13", "c14", "c34",                    // Confusión de Lenguas / descarte
            "c02", "c02", "c18",                           // Expulsión / Pozo de José (rebote)
            "c31", "c20", "c40",                           // Marca / Bendición / Piedra de Jacob
        };

        /// <summary>h2-B — Diluvio agresivo: en vez de proteger con el Arca, acelera con TIERRAs de
        /// mucho FD (Sodoma, Gomorra, sostenidas por Lot) y remata con sacrificios y destrucción.</summary>
        private static readonly string[] H2B =
        {
            "t03", "t03", "t12", "t12", "sh05", "sh05", "sh06", "sh06", "sh06b", "sh06b", // piezas
            "t08", "t08", "t09", "t09",                    // Sodoma y Gomorra: FD alto y riesgo
            "sh09", "sh09",                                // Lot: evita que se autodestruyan
            "sa4", "sd4", "sd5",                           // Carnero / Ángel del Sacrificio / Ángel del Señor
            "c15", "c15", "c37",                           // sacrificios (excluyentes entre sí)
            "c11", "c11", "c07",                           // destrucción
            "c25", "c17", "c42",                           // duración
            "c03", "c29",                                  // RESPUESTAS anti-DÍA
        };

        /// <summary>h3-B — Abraham por DÍAs: busca cerrar el ciclo de los 7 DÍAs (Victoria I) con
        /// activación y FD, en lugar de la línea de tutores de la variante A.</summary>
        private static readonly string[] H3B =
        {
            "t06", "t06", "t05", "t05", "sh07", "sh07", "sh08", "sh08", "sh19", "sh19", // piezas
            "t07", "t07", "t17", "t18",                    // Salem / ríos
            "c10", "c10", "c04", "c04",                    // Alianza del Fuego (DÍA gratis) / Ofrenda (FD)
            "sh12", "sh12", "sh23",                        // Isaac / Matusalén
            "sd7", "sd8", "sd6",                           // mensajeros y ángeles
            "c38", "c27", "c33",                           // robo
            "c19", "c21", "c32",                           // RESPUESTAS
            "c05", "t04",                                  // Maldición / Babel: frenan al rival
        };

        /// <summary>h4-B — Sodoma sin Lot: deja que las TIERRAs se autodestruyan para disparar sus
        /// efectos AL SER DESTRUIDA, y las recupera con Rama de Olivo y tutores de TIERRA.</summary>
        private static readonly string[] H4B =
        {
            "t08", "t08", "t09", "t09", "sh07", "sh07", "sh09", "sh09", "sd3", "sd3", // piezas
            "c08", "c08", "c24", "c24", "c09", "c39",      // recuperar y buscar TIERRA
            "c40", "c40",                                  // Piedra de Jacob: TIERRA extra por turno
            "t05", "t06", "t07", "t12",                    // Canaán, Hebrón, Salem, Egipto
            "sh19", "sh28", "sh10",                        // Melquisedec / Agar / Ismael
            "c35", "c36", "c01",                           // tutores de SER / recuperación
            "c12", "c21",                                  // RESPUESTAS
        };

        /// <summary>h5-B — Fratricidio con Retirados: Caín y Abel apoyados en cartas que reviven y
        /// reciclan desde Retirados, en vez de la línea de protección de la variante A.</summary>
        private static readonly string[] H5B =
        {
            "t01", "t01", "t14", "t14", "sh03", "sh03", "sh04", "sh04", "c05", "c05", // piezas
            "c01", "c01", "c22", "c22", "c36", "c36",      // regresar SER de Retirados
            "c15", "c37",                                  // sacrificios que llenan Retirados
            "sh11", "sh23", "sh22",                        // Henoc / Matusalén / Set
            "sa2", "sa3", "sa5",                           // animales baratos
            "t10", "t16", "t19",                           // Betel / Dotán / Tigris
            "c26", "c33",                                  // espiar el mazo rival
        };

        /// <summary>h6-B — Escalera por duración: Jacob y los suyos sostenidos con cartas que suman
        /// turnos (Túnica, Juramento, Censo), frente a la línea de orden-de-mazo de la variante A.</summary>
        private static readonly string[] H6B =
        {
            "t10", "t10", "t05", "t05", "sh13", "sh13", "sh12b", "sh12b", "sd2", "sd2", // piezas
            "c17", "c17", "c25", "c25", "c20", "c42",      // duración
            "sh27", "sh24", "sh25", "sh21",                // Tamar / Judá / Rubén / Lea
            "sd6", "sd6", "sd1",                           // Ser que Luchó con Jacob / Querubines
            "t15", "t15", "t11",                           // Peniel / Macpelá
            "c31", "c06",                                  // protección
            "c29", "c03",                                  // RESPUESTAS anti-DÍA
        };

        /// <summary>h7-B — José por presión: en vez de cavar con los sueños, ataca la mano y el mazo
        /// del rival (Panadero, Copero, Faraón) y rebota sus SER.</summary>
        private static readonly string[] H7B =
        {
            "t12", "t12", "t13", "t13", "sh16", "sh16", "sh18", "sh18", "sh17", "sh17", // piezas
            "c34", "c34", "c26", "c26", "c33", "c33",      // atacar mano y mazo rival
            "c14", "c18", "c02",                           // descarte y rebote
            "sh20", "sh29", "sh26",                        // Putifar / su esposa / Dina
            "sd9", "sd5",                                  // Ángel de la Torre / Ángel del Señor
            "t16", "t04", "t20",                           // Dotán / Babel / Éufrates
            "c13", "c27",                                  // Confusión / Copa
        };

        private static readonly Dictionary<string, string[]> ByHistoria = new()
        {
            { "h1", H1 }, { "h2", H2 }, { "h3", H3 }, { "h4", H4 },
            { "h5", H5 }, { "h6", H6 }, { "h7", H7 },
        };

        private static readonly Dictionary<string, string[]> ByHistoriaB = new()
        {
            { "h1", H1B }, { "h2", H2B }, { "h3", H3B }, { "h4", H4B },
            { "h5", H5B }, { "h6", H6B }, { "h7", H7B },
        };

        /// <summary>Cuántas variantes de mazo hay por historia.</summary>
        public const int Variants = 2;

        /// <summary>Lista curada de esa historia. <paramref name="variant"/> 0 = A, 1 = B.
        /// Devuelve null si esa historia no tiene mazo definido.</summary>
        public static IReadOnlyList<string>? For(string historiaId, int variant = 0)
        {
            var tabla = (variant % Variants == 1) ? ByHistoriaB : ByHistoria;
            return tabla.TryGetValue(historiaId, out var list) ? list : null;
        }

        /// <summary>Ids del catálogo que NINGÚN mazo curado incluye (hueco de cobertura de pruebas).</summary>
        public static IReadOnlyList<string> NotCovered(CardCatalog cat)
        {
            var used = new HashSet<string>(
                ByHistoria.Values.Concat(ByHistoriaB.Values).SelectMany(v => v));
            return cat.Cards.Values
                .Where(d => d.Type != CardType.Dia && d.Type != CardType.Historia)
                .Select(d => d.Id)
                .Where(id => !used.Contains(id))
                .OrderBy(id => id)
                .ToList();
        }
    }
}
