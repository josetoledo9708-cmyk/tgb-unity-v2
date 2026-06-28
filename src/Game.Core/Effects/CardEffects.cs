using System;
using System.Linq;
using Game.Core.Model;
using static Game.Core.Model.EffectTrigger;

namespace Game.Core.Effects
{
    /// <summary>
    /// Registro de efectos por carta. Familias 1-3 (vanilla/FD/robo, búsqueda, parejas
    /// condicionales y "activa DÍA gratis"). Las cartas no listadas aún no tienen efecto
    /// implementado (se irán añadiendo; ver lista de pendientes en el plan).
    /// </summary>
    public static class CardEffects
    {
        public static IEffectResolver BuildResolver()
        {
            var reg = new EffectRegistry();
            RegisterFamilies(reg);
            return new RegistryEffectResolver(reg);
        }

        public static void RegisterFamilies(EffectRegistry r)
        {
            // --- Animales ---
            r.On("sa2", EfectoActivado, c => EffectApi.Draw(c.Engine, c.Owner, 1));      // El Cuervo
            r.On("sa3", AlSalir, c => EffectApi.Draw(c.Engine, c.Owner, 1));             // La Paloma
            r.On("sa3", EfectoActivado, c => EffectApi.AddFd(c.Engine, c.Owner, 1));

            // --- SER humanos: FD/robo ---
            r.On("sh10", AlEntrar, c => EffectApi.AddFd(c.Engine, c.Owner, 1));          // Ismael
            r.On("sh10", EfectoActivado, c => EffectApi.Draw(c.Engine, c.Owner, 1));
            r.On("sh06", EfectoActivado, c => EffectApi.AddFd(c.Engine, c.Owner, 2));    // Sem
            r.On("sh06", AlEntrar, c =>
            {
                if (EffectApi.HasSerInField(c.Owner, "Noé"))
                    foreach (var s in c.Owner.Seres.Cards)
                        if (s.Nombre == "Noé") s.DurLeft += 1;
            });
            r.On("sh06b", AlEntrar, c =>                                                 // Jafet
            {
                if (EffectApi.HasSerInField(c.Owner, "Noé") || EffectApi.HasSerInField(c.Owner, "Sem"))
                    EffectApi.Draw(c.Engine, c.Owner, 1);
            });
            r.On("sh06b", EfectoActivado, c => EffectApi.Draw(c.Engine, c.Owner, 1));
            r.On("sh23", AlEntrar, c => EffectApi.ModifyDurAll(c.Engine, c.Owner, 1));   // Matusalén
            r.On("sh23", EfectoActivado, c => EffectApi.AddFd(c.Engine, c.Owner, 2));
            r.On("sh18", AlEntrar, c => EffectApi.Draw(c.Engine, c.Owner, 2));           // El Faraón
            r.On("sh18", EfectoActivado, c =>
                EffectApi.AddFd(c.Engine, c.Owner, Math.Min(4, c.Owner.Tierras.Count / 2)));

            // --- SER humanos: búsqueda y parejas ---
            r.On("sh04", AlEntrar, c =>                                                  // Abel
                EffectApi.SearchToHand(c.Engine, c.Owner, d => d.Nombre == "La Ofrenda de Abel"));
            r.On("sh04", EfectoActivado, c => EffectApi.AddFd(c.Engine, c.Owner, 2));
            r.On("sh05", AlEntrar, c =>                                                  // Noé
                EffectApi.SearchToHand(c.Engine, c.Owner, d => d.Nombre == "La Paloma"));
            r.On("sh05", EfectoActivado, c => EffectApi.Draw(c.Engine, c.Owner, 2));
            r.On("sh01", AlEntrar, c => PairDrawOrSearch(c, "Eva"));                     // Adán
            r.On("sh02", AlEntrar, c => PairDrawOrSearch(c, "Adán"));                    // Eva

            // --- "Activa 1 DÍA sin coste" (condición requerida) ---
            r.On("sh19", AlEntrar, c => c.Engine.ActivateDia(useFree: true));           // Melquisedec
            r.On("sh19", EfectoActivado, c => EffectApi.AddFd(c.Engine, c.Owner, 3));
            r.On("sh11", AlEntrar, c => c.Engine.ActivateDia(useFree: true));           // Henoc
            r.On("sh11", EfectoActivado, c => EffectApi.Draw(c.Engine, c.Owner, 1));
            r.On("sh07", EfectoActivado, c => c.Engine.ActivateDia(useFree: true));     // Abraham
            r.On("sh07", AlEntrar, c =>
                EffectApi.SearchToHand(c.Engine, c.Owner, d => CardTypeNames.IsSer(d.Type)));

            // --- CONCEPTOS de uso único (familia simple) ---
            r.On("c01", UsoUnico, c => EffectApi.ReturnSerFromRetiradosToField(c.Engine, c.Owner)); // Soplo de Vida
            r.On("c04", UsoUnico, c => EffectApi.AddFd(c.Engine, c.Owner, 2));          // La Ofrenda de Abel
            r.On("c09", UsoUnico, c =>                                                  // La Llamada de Abram
                EffectApi.SearchToHand(c.Engine, c.Owner, d => d.Type == CardType.Tierra));
            r.On("c24", UsoUnico, c => EffectApi.SearchTierraToField(c.Engine, c.Owner)); // La Promesa de la Tierra
            r.On("c27", UsoUnico, c =>                                                  // La Copa de Benjamín
            {
                EffectApi.Draw(c.Engine, c.Owner, 2);
                EffectApi.Draw(c.Engine, c.Opponent, 1);
            });
            r.On("c14", UsoUnico, c => EffectApi.DiscardRandom(c.Engine, c.Opponent, 2)); // La Dispersión de los Pueblos

            RegisterSpecials(r);
        }

        /// <summary>Familias 4-6: destrucción/control y casos especiales documentados.</summary>
        public static void RegisterSpecials(EffectRegistry r)
        {
            r.On("c07", UsoUnico, c => EffectApi.DestroyAllSeresBothSides(c.Engine));        // El Diluvio
            r.On("c08", UsoUnico, c => EffectApi.SearchTierraToField(c.Engine, c.Owner));    // La Rama de Olivo
            r.On("c11", UsoUnico, c => EffectApi.DestroyTierras(c.Engine, c.Owner.Id, 1));   // La Destrucción de Sodoma
            r.On("sd3", AlEntrar, c => EffectApi.DestroyTierras(c.Engine, c.Owner.Id, 2));   // Los Ángeles de Sodoma

            r.On("sh09", AlEntrar, c => c.Owner.TierraProtected = true);                     // Lot

            // Sodoma / Gomorra: al ser destruidas, el rival de su dueño roba.
            r.On("t08", AlSerDestruida, c => EffectApi.Draw(c.Engine, c.Opponent, 2));       // Sodoma
            r.On("t09", AlSerDestruida, c => EffectApi.Draw(c.Engine, c.Opponent, 3));       // Gomorra

            // Caín: al entrar destruye 1 TIERRA; al salir, La Tierra de Nod lo sigue a Retirados.
            r.On("sh03", AlEntrar, c => EffectApi.DestroyTierras(c.Engine, c.Owner.Id, 1));
            r.On("sh03", AlSalir, c =>
            {
                var nod = c.Owner.Tierras.Cards.FirstOrDefault(x => x.Nombre == "La Tierra de Nod");
                if (nod != null)
                {
                    c.Owner.Tierras.Remove(nod);
                    c.Owner.Retirados.Add(nod);
                    c.Engine.State.Emit("La Tierra de Nod se va con Caín");
                }
            });
        }

        private static void PairDrawOrSearch(EffectContext c, string pareja)
        {
            if (EffectApi.HasSerInField(c.Owner, pareja))
                EffectApi.Draw(c.Engine, c.Owner, 1);
            else
                EffectApi.SearchToHand(c.Engine, c.Owner, d => d.Nombre == pareja);
        }
    }
}
