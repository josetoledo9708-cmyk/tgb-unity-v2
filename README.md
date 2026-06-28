# The Great Book — TCG 3D (Unity, 2 jugadores)

Juego de cartas bíblico (Génesis). Motor de tablero sin combate: armas cartas en campo para
cumplir una HISTORIA o llegar al DÍA 7. Recurso = Favor Divino (FD) por tapear TIERRAs.

Diseño en `docs/superpowers/specs/` y `docs/superpowers/plans/`. Catálogo en
`data/catalogo.v3.json` (copiado a `Assets/StreamingAssets/` para Unity).

## Estructura (fuente única del Core)

```
Assets/                      <- proyecto Unity
  Game/Core/                 <- MOTOR, C# puro (asmdef Game.Core, sin UnityEngine)
    Model/ Engine/ Effects/ Data/ View/
  Game/Runtime/              <- capa Unity (asmdef Game.Runtime): loader Newtonsoft + bootstrap
  Game/Tests/EditMode/       <- NUnit EditMode (humo)
  StreamingAssets/catalogo.v3.json
Packages/ ProjectSettings/   <- proyecto Unity
dev/                         <- build .NET de desarrollo (Unity lo ignora)
  Game.Core.Dev.csproj       <- compila Assets/Game/Core/**.cs + CatalogLoader.cs (System.Text.Json)
tests/Game.Core.Tests/       <- batería completa (runner propio), corre con dotnet
data/catalogo.v3.json        <- catálogo canónico (copiar a StreamingAssets al cambiar)
```

El Core es **el mismo código** para Unity y para los tests .NET: Unity lo compila vía
`Game.Core.asmdef`; el build de dev lo compila vía `dev/Game.Core.Dev.csproj`. La única
diferencia es el cargador JSON (Unity = Newtonsoft; dev = System.Text.Json).

## Desarrollo / tests sin Unity

```
dotnet run --project tests/Game.Core.Tests
```

Corre la batería completa (M0–M7, ~65 casos) sin abrir el editor.

## Abrir en Unity

1. Unity Hub → Add → seleccionar esta carpeta (editor 6000.4.x, URP).
2. Unity importa, genera `.meta`/`Library` y resuelve paquetes (Newtonsoft, Test Framework).
3. Demo jugable: en una escena vacía, añadir un GameObject con `HotseatView`
   (`Assets/Game/Runtime/View`) y Play. Genera cámara/luz y dibuja el campo en 3D (cartas
   por código). Clic en carta de tu mano = jugarla; clic en tu TIERRA = tapearla; botón
   "Terminar turno". (`GameBootstrap` es la variante headless que solo vuelca al Console.)
4. Tests: Window → General → Test Runner → EditMode → Run All.

> La capa `View` es un punto de partida autogenerado (sin prefabs ni arte). La presentación
> final (prefabs de carta, animaciones, arrastrar y soltar) se construye encima del mismo motor.

> Si cambias `data/catalogo.v3.json`, copia la nueva versión a
> `Assets/StreamingAssets/catalogo.v3.json`.
