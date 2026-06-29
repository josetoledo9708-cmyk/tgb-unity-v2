using System.Linq;
using Game.Core.Model;
using static Game.Core.Model.EffectTrigger;

namespace Game.Core.Effects
{
    /// <summary>
    /// Registro de efectos por carta (id + trigger). Cubre el catálogo v3. Algunos efectos
    /// con dependencia de "stack"/timing (control de SER rival, copiar efectos, mirar y
    /// reordenar) están como aproximaciones documentadas; ver comentarios.
    /// </summary>
    public static class CardEffects
    {
        public static IEffectResolver BuildResolver() => new RegistryEffectResolver(BuildRegistry());

        public static EffectRegistry BuildRegistry()
        {
            var reg = new EffectRegistry();
            RegisterAll(reg);
            return reg;
        }

        public static void RegisterAll(EffectRegistry r)
        {
            Dias(r);
            Tierras(r);
            Divinos(r);
            Humanos(r);
            Animales(r);
            Conceptos(r);
        }

        // ---------------- DÍAS (recompensa al activar) ----------------
        private static void Dias(EffectRegistry r)
        {
            r.On("dia2", AlActivarElDia, c => c.Owner.TierraPlayedThisTurn = false); // juega 1 TIERRA extra
            r.On("dia3", AlActivarElDia, c => EffectApi.SearchToHand(c.Engine, c.Owner, d => d.Type == CardType.Tierra));
            r.On("dia4", AlActivarElDia, c => { EffectApi.LookTopTakeOne(c.Engine, c.Owner, 3); EffectApi.AddFd(c.Engine, c.Owner, 1); });
            r.On("dia5", AlActivarElDia, c => EffectApi.ReturnFromRetiradosToHand(c.Engine, c.Owner, d => CardTypeNames.IsSer(d.Type)));
            r.On("dia6", AlActivarElDia, c => EffectApi.SearchSerToHand(c.Engine, c.Owner));
            // dia1: sin recompensa. dia7: Victoria I (en el motor).
        }

        private static void Negate(EffectContext c)
        {
            c.State.Active.NegatedNextEffect = true;
            c.State.Emit("Respuesta: anulará el próximo efecto rival");
        }

        // ---------------- TIERRAS ----------------
        private static void Tierras(EffectRegistry r)
        {
            r.On("t01", AlTapearse, c => { if (c.Opponent.Seres.Count > 0) EffectApi.Draw(c.Engine, c.Opponent, 1); });
            r.On("t02", AlTapearse, c =>
            {
                if (!EffectApi.SearchToHand(c.Engine, c.Owner, d => d.Id == "c02" || d.Id == "c28"))
                    if (EffectApi.SearchToHand(c.Engine, c.Owner, d => d.Type == CardType.Concepto))
                        EffectApi.DiscardRandom(c.Engine, c.Owner, 2);
            });
            r.On("t04", AlTapearse, c => { c.Opponent.DiaBlockedTurns += 1; EffectApi.DiscardRandom(c.Engine, c.Owner, 1); });
            r.On("t05", AlEntrar, c => { if (EffectApi.OncePerGame(c.Owner, "t05")) EffectApi.SearchTierraToField(c.Engine, c.Owner, d => d.Id == "t06" || d.Id == "t07"); }); // Canaán
            r.On("t10", AlTapearse, c => EffectApi.LookTopReorder(c.Engine, c.Owner, 2)); // Betel
            r.On("t11", AlTapearse, c =>
            {
                EffectApi.ReturnFromRetiradosToHand(c.Engine, c.Owner, d => CardTypeNames.IsSer(d.Type));
                EffectApi.DiscardRandom(c.Engine, c.Owner, 1);
            });
            r.On("t12", AlEntrar, c => { if (EffectApi.HasTierraInField(c.Owner, "El Monte Ararat")) EffectApi.AddFd(c.Engine, c.Owner, 2); });
            r.On("t13", AlEntrar, c => EffectApi.ProtectAllSeres(c.Engine, c.Owner, 1));
            r.On("t14", AlEntrar, c =>
            {
                if (EffectApi.HasSerInField(c.Owner, "Caín"))
                {
                    EffectApi.Draw(c.Engine, c.Owner, 1);
                    foreach (var s in c.Owner.Seres.Cards) if (s.Nombre == "Caín") s.DurLeft += 1;
                }
            });
            r.On("t15", AlEntrar, c => { if (EffectApi.HasSerInField(c.Owner, "Jacob/Israel")) EffectApi.Draw(c.Engine, c.Owner, 1); });
            r.On("t16", AlEntrar, c => { if (EffectApi.OncePerGame(c.Owner, "t16")) EffectApi.SearchToHand(c.Engine, c.Owner, d => d.Nombre == "José"); });
            r.On("t17", AlTapearse, c => { EffectApi.AddFd(c.Engine, c.Owner, 1); if (EffectApi.HasTierraInField(c.Owner, "El Jardín del Edén")) EffectApi.AddFd(c.Engine, c.Owner, 1); });
            r.On("t18", AlTapearse, c => c.Engine.State.Emit("Gehón: mira 1 carta del fondo"));
            r.On("t19", AlEntrar, c =>
            {
                if (c.Owner.Mano.Count > 0 && c.Decisions.ChooseYesNo(c.State, "¿Descartar 1 para robar 1?"))
                { EffectApi.DiscardRandom(c.Engine, c.Owner, 1); EffectApi.Draw(c.Engine, c.Owner, 1); }
            });
            r.On("t20", AlEntrar, c => { if (c.Owner.Tierras.Count >= 3) EffectApi.AddFd(c.Engine, c.Owner, 1); });

            // Sodoma / Gomorra: al ser destruidas, el rival de su dueño roba.
            r.On("t08", AlSerDestruida, c => EffectApi.Draw(c.Engine, c.Opponent, 2));
            r.On("t09", AlSerDestruida, c => EffectApi.Draw(c.Engine, c.Opponent, 3));
        }

        // ---------------- SER DIVINOS ----------------
        private static void Divinos(EffectRegistry r)
        {
            r.On("sd1", AlEntrar, c => c.Opponent.EffectsBlockedTurns = 2);             // Querubines
            r.On("sd1", EfectoActivado, c => EffectApi.ProtectOneSer(c.Engine, c.Owner, 1));
            r.On("sd2", AlEntrar, c => { EffectApi.Draw(c.Engine, c.Owner, 1); EffectApi.DiscardRandom(c.Engine, c.Opponent, 1); });
            r.On("sd2", EfectoActivado, c => EffectApi.AddFd(c.Engine, c.Owner, 1));
            r.On("sd3", AlEntrar, c => EffectApi.DestroyTierras(c.Engine, c.Owner.Id, 2)); // Los Ángeles de Sodoma
            r.On("sd3", EfectoActivado, c =>
            {
                EffectApi.SearchSerToHand(c.Engine, c.Owner);
                EffectApi.SearchSerToHand(c.Engine, c.Opponent);
            });
            r.On("sd4", AlEntrar, c => EffectApi.ProtectAllSeres(c.Engine, c.Owner, 1));  // Ángel del Sacrificio
            r.On("sd4", EfectoActivado, c => EffectApi.ReturnFromRetiradosToHand(c.Engine, c.Owner, d => CardTypeNames.IsSer(d.Type)));
            r.On("sd5", AlEntrar, c => EffectApi.Draw(c.Engine, c.Owner, 2));             // Ángel del Señor
            r.On("sd5", EfectoActivado, c => c.Engine.ActivateDia(useFree: true));
            r.On("sd6", AlEntrar, c => EffectApi.BounceSeresToHand(c.Engine, c.Opponent, c.Opponent.Seres.Count)); // Ser que Luchó
            r.On("sd6", EfectoActivado, c => c.Opponent.NegatedNextEffect = true);
            r.On("sd7", AlEntrar, c => EffectApi.Draw(c.Engine, c.Owner, 2));             // Tres Mensajeros
            r.On("sd7", EfectoActivado, c => c.Engine.ActivateDia(useFree: true));
            r.On("sd8", AlEntrar, c => EffectApi.SearchToHand(c.Engine, c.Owner, d => d.Id == "c35")); // Ángel de Agar
            r.On("sd8", EfectoActivado, c => EffectApi.AddFd(c.Engine, c.Owner, 1));
            r.On("sd9", AlEntrar, c => c.Opponent.ConceptosBlockedThisTurn = true);       // Ángel de la Torre
            r.On("sd9", EfectoActivado, c => c.Engine.State.Emit("Torre: cancela efecto de 1 TIERRA especial rival"));
        }

        // ---------------- SER HUMANOS ----------------
        private static void Humanos(EffectRegistry r)
        {
            r.On("sh01", AlEntrar, c => Pair(c, "Eva"));   // Adán
            r.On("sh01", EfectoActivado, c => EffectApi.LookTopReorder(c.Engine, c.Owner, 2)); // Adán
            r.On("sh02", AlEntrar, c => Pair(c, "Adán"));   // Eva
            r.On("sh02", EfectoActivado, c => EffectApi.DiscardRandom(c.Engine, c.Owner, 1));
            r.On("sh03", AlEntrar, c => EffectApi.DestroyTierras(c.Engine, c.Owner.Id, 1)); // Caín
            r.On("sh03", EfectoActivado, c => EffectApi.SearchToHand(c.Engine, c.Owner, d => d.Type == CardType.Concepto));
            r.On("sh03", AlSalir, c =>
            {
                var nod = c.Owner.Tierras.Cards.FirstOrDefault(x => x.Nombre == "La Tierra de Nod");
                if (nod != null) { c.Owner.Tierras.Remove(nod); c.Owner.Retirados.Add(nod); c.Engine.State.Emit("La Tierra de Nod se va con Caín"); }
            });
            r.On("sh04", AlEntrar, c => EffectApi.SearchToHand(c.Engine, c.Owner, d => d.Id == "c04")); // Abel
            r.On("sh04", EfectoActivado, c => EffectApi.AddFd(c.Engine, c.Owner, 2));
            r.On("sh05", AlEntrar, c => EffectApi.SearchToHand(c.Engine, c.Owner, d => d.Nombre == "La Paloma")); // Noé
            r.On("sh05", EfectoActivado, c => EffectApi.Draw(c.Engine, c.Owner, 2));
            r.On("sh06", AlEntrar, c => { if (EffectApi.HasSerInField(c.Owner, "Noé")) foreach (var s in c.Owner.Seres.Cards) if (s.Nombre == "Noé") s.DurLeft += 1; }); // Sem
            r.On("sh06", EfectoActivado, c => EffectApi.AddFd(c.Engine, c.Owner, 2));
            r.On("sh06b", AlEntrar, c => { if (EffectApi.HasSerInField(c.Owner, "Noé") || EffectApi.HasSerInField(c.Owner, "Sem")) EffectApi.Draw(c.Engine, c.Owner, 1); }); // Jafet
            r.On("sh06b", EfectoActivado, c => EffectApi.Draw(c.Engine, c.Owner, 1));
            r.On("sh07", AlEntrar, c => EffectApi.SearchSerToHand(c.Engine, c.Owner)); // Abraham
            r.On("sh07", EfectoActivado, c => c.Engine.ActivateDia(useFree: true));
            r.On("sh08", AlEntrar, c => { if (EffectApi.HasSerInField(c.Owner, "Abraham")) EffectApi.Draw(c.Engine, c.Owner, 2); }); // Sara
            r.On("sh08", EfectoActivado, c => { EffectApi.DiscardRandom(c.Engine, c.Owner, 1); EffectApi.DiscardRandom(c.Engine, c.Opponent, 1); });
            r.On("sh09", AlEntrar, c => c.Owner.TierraProtected = true); // Lot
            r.On("sh09", EfectoActivado, c => { EffectApi.Draw(c.Engine, c.Owner, 1); EffectApi.Draw(c.Engine, c.Opponent, 1); if (EffectApi.HasSerInField(c.Owner, "Abraham")) EffectApi.AddFd(c.Engine, c.Owner, 1); });
            r.On("sh10", AlEntrar, c => EffectApi.AddFd(c.Engine, c.Owner, 1)); // Ismael
            r.On("sh10", EfectoActivado, c => EffectApi.Draw(c.Engine, c.Owner, 1));
            r.On("sh11", AlEntrar, c => c.Engine.ActivateDia(useFree: true)); // Henoc
            r.On("sh11", EfectoActivado, c => EffectApi.Draw(c.Engine, c.Owner, 1));
            r.On("sh12", AlEntrar, c => EffectApi.Draw(c.Engine, c.Owner, 2)); // Isaac
            r.On("sh12", EfectoActivado, c => c.Engine.ActivateDia(useFree: true));
            r.On("sh12b", AlEntrar, c => { if (EffectApi.HasSerInField(c.Owner, "Jacob/Israel")) EffectApi.Draw(c.Engine, c.Owner, 1); }); // Esaú
            r.On("sh12b", EfectoActivado, c => EffectApi.DestroyRivalSeresCosteMax(c.Engine, c.Owner.Id, 2, 1));
            r.On("sh13", AlEntrar, c => EffectApi.SearchToHand(c.Engine, c.Owner, d => d.Id == "c19")); // Jacob
            r.On("sh13", EfectoActivado, c => c.Engine.State.Emit("Jacob: toma control de 1 SER humano rival (1 turno)"));
            r.On("sh14", AlEntrar, c => // Rebeca
            {
                if (EffectApi.HasSerInField(c.Owner, "Isaac")) { EffectApi.Draw(c.Engine, c.Owner, 2); foreach (var s in c.Owner.Seres.Cards) if (s.Nombre == "Isaac") s.DurLeft += 1; }
                else EffectApi.Draw(c.Engine, c.Owner, 1);
            });
            r.On("sh14", EfectoActivado, c => EffectApi.SearchSerToHand(c.Engine, c.Owner));
            r.On("sh15", AlEntrar, c => // Raquel
            {
                if (EffectApi.HasSerInField(c.Owner, "Jacob/Israel")) EffectApi.Draw(c.Engine, c.Owner, 2);
                if (!EffectApi.HasSerInField(c.Owner, "Lea")) EffectApi.SearchToHand(c.Engine, c.Owner, d => d.Nombre == "Lea");
            });
            r.On("sh15", EfectoActivado, c => EffectApi.AddFd(c.Engine, c.Owner, 1));
            r.On("sh16", AlEntrar, c => // José
            {
                if (EffectApi.HasSerInField(c.Owner, "El Faraón") && EffectApi.HasTierraInField(c.Owner, "Egipto"))
                {
                    foreach (var s in c.Owner.Seres.Cards) if (s.Nombre == "El Faraón") s.Indestructible = true;
                    foreach (var t in c.Owner.Tierras.Cards) if (t.Nombre == "Egipto") t.Indestructible = true;
                    c.Engine.State.Emit("José: El Faraón y Egipto son indestructibles");
                }
            });
            r.On("sh16", EfectoActivado, c => { c.Engine.State.Emit("José: mira top 5 del mazo rival"); if (EffectApi.HasSerInField(c.Owner, "Benjamín")) EffectApi.ReturnFromRetiradosToHand(c.Engine, c.Owner, d => CardTypeNames.IsSer(d.Type)); });
            r.On("sh17", AlEntrar, c => EffectApi.Draw(c.Engine, c.Owner, 1)); // Benjamín (regreso al mazo: en fase Preludio)
            r.On("sh18", AlEntrar, c => EffectApi.Draw(c.Engine, c.Owner, 2)); // El Faraón
            r.On("sh18", EfectoActivado, c => EffectApi.AddFd(c.Engine, c.Owner, System.Math.Min(4, c.Owner.Tierras.Count / 2)));
            r.On("sh19", AlEntrar, c => c.Engine.ActivateDia(useFree: true)); // Melquisedec
            r.On("sh19", EfectoActivado, c => EffectApi.AddFd(c.Engine, c.Owner, 3));
            r.On("sh20", AlEntrar, c => EffectApi.Draw(c.Engine, c.Owner, 1)); // Putifar
            r.On("sh20", EfectoActivado, c => c.Engine.State.Emit("Putifar: bloquea el efecto activado de 1 SER rival"));
            r.On("sh21", AlEntrar, c => // Lea
            {
                if (EffectApi.HasSerInField(c.Owner, "Jacob/Israel")) { EffectApi.DiscardRandom(c.Engine, c.Owner, 1); EffectApi.DiscardRandom(c.Engine, c.Opponent, 1); EffectApi.AddFd(c.Engine, c.Owner, 1); }
                if (EffectApi.HasSerInField(c.Owner, "Raquel")) foreach (var s in c.Owner.Seres.Cards) if (s.Nombre == "Lea") s.DurLeft = System.Math.Max(0, s.DurLeft - 1);
            });
            r.On("sh21", EfectoActivado, c => c.Engine.State.Emit("Lea: pone 1 carta de la mano rival al fondo de su mazo"));
            r.On("sh22", AlEntrar, c => { if (c.Owner.Retirados.Cards.Any(x => x.Nombre == "Abel")) EffectApi.ReturnFromRetiradosToHand(c.Engine, c.Owner, d => CardTypeNames.IsSer(d.Type)); }); // Set
            r.On("sh22", EfectoActivado, c => EffectApi.Draw(c.Engine, c.Owner, 1));
            r.On("sh23", AlEntrar, c => EffectApi.ModifyDurAll(c.Engine, c.Owner, 1)); // Matusalén
            r.On("sh23", EfectoActivado, c => EffectApi.AddFd(c.Engine, c.Owner, 2));
            r.On("sh24", AlEntrar, c => { if (EffectApi.HasSerInField(c.Owner, "Tamar")) EffectApi.Draw(c.Engine, c.Owner, 2); }); // Judá
            r.On("sh24", EfectoActivado, c => EffectApi.DestroyRivalSeresCosteMax(c.Engine, c.Owner.Id, 2, 1));
            r.On("sh25", AlEntrar, c => EffectApi.ProtectOneSer(c.Engine, c.Owner, 0)); // Rubén
            r.On("sh25", EfectoActivado, c => { EffectApi.Draw(c.Engine, c.Owner, 1); EffectApi.Draw(c.Engine, c.Opponent, 1); });
            r.On("sh26", AlEntrar, c => EffectApi.DiscardRandom(c.Engine, c.Opponent, 1)); // Dina
            r.On("sh26", EfectoActivado, c => c.Engine.State.Emit("Dina: mira la mano rival"));
            r.On("sh27", AlEntrar, c => { if (EffectApi.HasSerInField(c.Owner, "Judá")) { EffectApi.Draw(c.Engine, c.Owner, 1); foreach (var s in c.Owner.Seres.Cards) if (s.Nombre == "Judá") s.DurLeft += 1; } }); // Tamar
            r.On("sh27", EfectoActivado, c => EffectApi.DiscardRandom(c.Engine, c.Opponent, 1));
            r.On("sh28", AlEntrar, c => { if (EffectApi.HasSerInField(c.Owner, "Abraham") || EffectApi.HasSerInField(c.Owner, "Sara")) EffectApi.AddFd(c.Engine, c.Owner, 2); }); // Agar
            r.On("sh28", EfectoActivado, c => EffectApi.SearchToHand(c.Engine, c.Owner, d => d.Type == CardType.Tierra));
            r.On("sh29", AlEntrar, c => c.Engine.State.Emit("Esposa de Putifar: 1 SER rival no puede activar efectos")); // La Esposa de Putifar
            r.On("sh29", EfectoActivado, c => { if (EffectApi.HasSerInField(c.Opponent, "José")) foreach (var s in c.Opponent.Seres.Cards) if (s.Nombre == "José") s.DurLeft = System.Math.Max(0, s.DurLeft - 1); });
        }

        // ---------------- SER ANIMALES ----------------
        private static void Animales(EffectRegistry r)
        {
            r.On("sa1", AlEntrar, c => EffectApi.DiscardRandom(c.Engine, c.Opponent, 1)); // La Serpiente
            r.On("sa1", EfectoActivado, c => c.Opponent.NegatedNextEffect = true);
            r.On("sa2", AlEntrar, c => EffectApi.Reveal(c.Engine, c.Opponent.Mazo.Top, "Carta superior del mazo rival")); // El Cuervo
            r.On("sa2", EfectoActivado, c => EffectApi.Draw(c.Engine, c.Owner, 1));
            r.On("sa3", AlSalir, c => EffectApi.Draw(c.Engine, c.Owner, 1)); // La Paloma
            r.On("sa3", EfectoActivado, c => EffectApi.AddFd(c.Engine, c.Owner, 1));
            r.On("sa4", AlEntrar, c => EffectApi.ProtectAllSeres(c.Engine, c.Owner, 1)); // El Carnero del Zarzal
            r.On("sa4", EfectoActivado, c => EffectApi.AddFd(c.Engine, c.Owner, 1));
            r.On("sa5", AlEntrar, c => { if (c.Owner.DiasActivados.Contains(5)) EffectApi.Draw(c.Engine, c.Owner, 1); }); // Criaturas Marinas
            r.On("sa5", EfectoActivado, c => c.Engine.State.Emit("Criaturas Marinas: puede regresar a la mano en vez de Retirados"));
        }

        // ---------------- CONCEPTOS ----------------
        private static void Conceptos(EffectRegistry r)
        {
            r.On("c01", UsoUnico, c => EffectApi.ReturnSerFromRetiradosToField(c.Engine, c.Owner)); // El Soplo de Vida
            r.On("c02", UsoUnico, c => EffectApi.BounceSeresToHand(c.Engine, c.Opponent, 2));       // La Expulsión del Edén
            r.On("c03", Respuesta, Negate);                                                          // La Espada de Fuego Giratoria
            r.On("c04", UsoUnico, c => EffectApi.AddFd(c.Engine, c.Owner, 2));                       // La Ofrenda de Abel
            r.On("c05", UsoUnico, c => c.Opponent.TierrasNoFdTurns = EffectApi.HasSerInField(c.Owner, "Caín") ? 2 : 1); // La Maldición de la Tierra
            r.On("c06", UsoUnico, c => EffectApi.ProtectAllSeres(c.Engine, c.Owner, 0));             // El Arca de Noé
            r.On("c07", UsoUnico, c => EffectApi.DestroyAllSeresBothSides(c.Engine));                // El Diluvio
            r.On("c08", UsoUnico, c => EffectApi.SearchTierraToField(c.Engine, c.Owner));            // La Rama de Olivo
            r.On("c09", UsoUnico, c => EffectApi.SearchToHand(c.Engine, c.Owner, d => d.Type == CardType.Tierra)); // La Llamada de Abram
            r.On("c10", UsoUnico, c => { c.Engine.ActivateDia(useFree: true); EffectApi.Draw(c.Engine, c.Owner, 1); }); // La Alianza del Fuego
            r.On("c11", UsoUnico, c => EffectApi.DestroyTierras(c.Engine, c.Owner.Id, 1));           // La Destrucción de Sodoma
            r.On("c12", Respuesta, c => { Negate(c); EffectApi.Draw(c.Engine, c.Owner, 1); });       // El Arco Iris
            r.On("c13", UsoUnico, c => c.State.ActivatedEffectsDisabled = true);                     // La Confusión de Lenguas
            r.On("c14", UsoUnico, c => EffectApi.DiscardRandom(c.Engine, c.Opponent, 2));            // La Dispersión de los Pueblos
            r.On("c15", UsoUnico, c => { EffectApi.SacrificeOwnSer(c.Engine, c.Owner); EffectApi.Draw(c.Engine, c.Owner, 3); }); // El Sacrificio de Isaac
            r.On("c16", UsoUnico, c => { EffectApi.LookTopReorder(c.Engine, c.Owner, 3); EffectApi.Draw(c.Engine, c.Owner, 1); }); // El Sueño de la Escalera
            r.On("c17", UsoUnico, c => c.Owner.NextSerDurBonus += 2);                                // La Túnica de Colores
            r.On("c18", UsoUnico, c =>                                                               // El Pozo de José
            {
                var ser = c.Decisions.ChooseCard(c.State, c.Opponent.Seres.Cards.ToList(), "SER rival al fondo del mazo", true);
                if (ser != null) { c.Opponent.Seres.Remove(ser); ser.Tapped = false; ser.DurLeft = ser.Def.Dur ?? 0; c.Opponent.Mazo.Add(ser); }
            });
            r.On("c19", Respuesta, c => { Negate(c); var top = c.Owner.Seres.Cards.OrderByDescending(s => s.DurLeft).FirstOrDefault(); if (top != null) top.DurLeft = System.Math.Max(0, top.DurLeft - 1); }); // La Lucha de Jacob con Dios
            r.On("c20", UsoUnico, c => EffectApi.ModifyDurAll(c.Engine, c.Owner, 1));                // La Bendición de Jacob
            r.On("c21", Respuesta, c => { Negate(c); EffectApi.Draw(c.Engine, c.Owner, 1); });       // El Perdón de José
            r.On("c22", UsoUnico, c => { EffectApi.ReturnFromRetiradosToHand(c.Engine, c.Owner, d => CardTypeNames.IsSer(d.Type)); EffectApi.Draw(c.Engine, c.Owner, 1); }); // José se da a Conocer
            r.On("c23", UsoUnico, c => { c.Engine.State.Emit("Robo de la Bendición: copia el último AL_ENTRAR rival"); EffectApi.Draw(c.Engine, c.Owner, 1); }); // aproximación
            r.On("c24", UsoUnico, c => EffectApi.SearchTierraToField(c.Engine, c.Owner));            // La Promesa de la Tierra
            r.On("c25", UsoUnico, c => EffectApi.ModifyDurAll(c.Engine, c.Owner, 2));                // El Juramento de las Estrellas
            r.On("c26", UsoUnico, c => { EffectApi.MillOpponentDeck(c.Engine, c.Opponent, 1); c.Engine.State.Emit("Sueño del Faraón: mira top 5 rival, descarta 1"); }); // El Sueño del Faraón
            r.On("c27", UsoUnico, c => { EffectApi.Draw(c.Engine, c.Owner, 2); EffectApi.Draw(c.Engine, c.Opponent, 1); }); // La Copa de Benjamín
            r.On("c28", UsoUnico, c => EffectApi.LookTopTakeOne(c.Engine, c.Owner, 3));              // El Fruto Prohibido
            r.On("c29", Respuesta, c => { Negate(c); EffectApi.Draw(c.Engine, c.Owner, 1); });       // El Velo de la Noche
            r.On("c30", UsoUnico, c => { bool got = EffectApi.LookTopTakeOne(c.Engine, c.Owner, 5, d => CardTypeNames.IsSer(d.Type)); if (got && c.Owner.Mano.Cards.Any(x => x.Nombre == "José")) EffectApi.Draw(c.Engine, c.Owner, 1); }); // El Sueño de los Haces
            r.On("c31", UsoUnico, c => EffectApi.ProtectOneSer(c.Engine, c.Owner, 0));               // La Marca de Caín
            r.On("c32", Respuesta, c => { Negate(c); EffectApi.Draw(c.Engine, c.Owner, 1); });       // La Promesa del Arco Iris
            r.On("c33", UsoUnico, c => c.Engine.State.Emit("Sueño del Copero: mira top 3 rival, 1 al fondo")); // El Sueño del Copero
            r.On("c34", UsoUnico, c => { EffectApi.DiscardRandom(c.Engine, c.Opponent, 1); EffectApi.Draw(c.Engine, c.Owner, 1); }); // El Sueño del Panadero
            r.On("c35", UsoUnico, c => EffectApi.SearchSerToHand(c.Engine, c.Owner));                // La Promesa a Agar
            r.On("c36", UsoUnico, c => EffectApi.ReturnFromRetiradosToHand(c.Engine, c.Owner, d => CardTypeNames.IsSer(d.Type))); // El Pozo de Agar
            r.On("c37", UsoUnico, c => { EffectApi.SacrificeOwnSer(c.Engine, c.Owner); EffectApi.SearchToHand(c.Engine, c.Owner, d => d.Type == CardType.Concepto); }); // La Prueba de Abraham
            r.On("c38", UsoUnico, c => { EffectApi.Draw(c.Engine, c.Owner, 2); if (EffectApi.HasSerInField(c.Owner, "Abraham") || EffectApi.HasSerInField(c.Owner, "Sara")) EffectApi.Draw(c.Engine, c.Owner, 1); }); // El Nacimiento de Isaac
            r.On("c39", UsoUnico, c => EffectApi.SearchTierraToField(c.Engine, c.Owner));            // El Viaje del Siervo
            r.On("c40", UsoUnico, c =>                                                               // La Piedra de Jacob
            {
                var tierra = c.Decisions.ChooseCard(c.State, c.Owner.Mano.Cards.Where(x => x.Type == CardType.Tierra).ToList(), "Colocar TIERRA", true);
                if (tierra != null) c.Engine.PlayTierraFree(tierra);
            });
            r.On("c41", UsoUnico, c => EffectApi.LookTopTakeOne(c.Engine, c.Owner, 5, d => CardTypeNames.IsSer(d.Type))); // Los Sueños de José
            r.On("c42", UsoUnico, c => { EffectApi.ModifyDurAll(c.Engine, c.Owner, 2); EffectApi.Draw(c.Engine, c.Owner, 1); }); // El Censo de Jacob
        }

        private static void Pair(EffectContext c, string pareja)
        {
            if (EffectApi.HasSerInField(c.Owner, pareja)) EffectApi.Draw(c.Engine, c.Owner, 1);
            else EffectApi.SearchToHand(c.Engine, c.Owner, d => d.Nombre == pareja);
        }
    }
}
