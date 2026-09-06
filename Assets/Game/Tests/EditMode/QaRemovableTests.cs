using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// El sistema de pruebas de efectos vive en una carpeta aparte y debe poder BORRARSE de una
    /// pieza sin romper el juego. Esto lo comprueba: ningún archivo de fuera puede nombrarlo.
    ///
    /// Este test se delataría a sí mismo si escribiera los nombres enteros, así que los arma a
    /// trozos (mismo truco que en el proyecto de origen del sistema).
    /// </summary>
    public class QaRemovableTests
    {
        // Armados a trozos a propósito: si aparecieran literales, el escaneo se encontraría a sí mismo.
        private static readonly string Carpeta = "Ga" + "me/Qa";
        private static readonly string Namespc = "Ga" + "me.Qa";
        private static readonly string Asmdef = "Ga" + "me.Qa";

        [Test]
        public void Ningun_Archivo_Del_Juego_Nombra_El_Sistema_De_Pruebas()
        {
            string assets = Application.dataPath;
            string qaDir = Path.Combine(assets, "Game", "Qa").Replace('\\', '/');

            var culpables = Directory.GetFiles(assets, "*.cs", SearchOption.AllDirectories)
                .Select(p => p.Replace('\\', '/'))
                .Where(p => !p.StartsWith(qaDir))                 // los de dentro sí pueden
                .Where(p => !p.EndsWith("QaRemovableTests.cs"))   // y este mismo
                .Where(p => { var t = File.ReadAllText(p); return t.Contains(Namespc) || t.Contains(Carpeta); })
                .Select(p => p.Substring(assets.Length))
                .ToList();

            Assert.IsEmpty(culpables,
                "Estos archivos del juego nombran el sistema de pruebas, así que borrarlo rompería "
                + "la compilación:\n" + string.Join("\n", culpables));
        }

        [Test]
        public void Ningun_Asmdef_Del_Juego_Referencia_El_Del_Sistema_De_Pruebas()
        {
            string assets = Application.dataPath;
            string qaDir = Path.Combine(assets, "Game", "Qa").Replace('\\', '/');

            var culpables = Directory.GetFiles(assets, "*.asmdef", SearchOption.AllDirectories)
                .Select(p => p.Replace('\\', '/'))
                .Where(p => !p.StartsWith(qaDir))
                .Where(p => File.ReadAllText(p).Contains(Asmdef))
                .Select(p => p.Substring(assets.Length))
                .ToList();

            Assert.IsEmpty(culpables,
                "Estos asmdef referencian el ensamblado de pruebas:\n" + string.Join("\n", culpables));
        }
    }
}
