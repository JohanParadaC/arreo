# Arreo — guía para Claude Code

Co-op de 1 a 5 llaneros en la Orinoquía colombiana: arrear ganado, enlazar con soga y levantar el hato. Unity 6, red con FishNet, lanzamiento en Steam Early Access.

Documentación completa (visión, decisiones, arquitectura, roadmap): https://claude.ai/artifact/KmV7E1NSBNZBZaRNNbPpXT

## Estado actual

- Fantasía: mezcla, caos en el arreo y calma en el hato.
- Fase: prototipo (semanas 1–8 hechas: la semana 5 puso los caballos en red, la 6 la manada, la soga, la barajustada, la puerta y el corral, la 7 el ciclo mínimo: arrear hasta la feria del pueblo, vender y ver la misma plata del hato, y la 8 el guardado y la medición con 5 jugadores). Ver el roadmap antes de proponer trabajo fuera de la semana en curso.
- Guardado (regla 5): el anfitrión guarda `arreo-save.json` (JSON, `SaveData.CurrentVersion` = 2) en `Application.persistentDataPath`, o en la carpeta de `-arreoSaveDir`. Guarda el reloj, la plata (con espacio para billeteras por jugador, D75), cada res (en el hato o vendida, y dónde está) y cada jugador. Se guarda solo al empezar un día de juego y al detener el anfitrión o cerrar; F5 guarda y F9 carga. El menú ofrece "Continuar partida". Un archivo dañado o de una versión desconocida no tumba el juego: queda como `arreo-save.bad-<fecha>.json` y empieza una partida nueva. Si cambia la forma del guardado, subir la versión y migrar en `SaveSerializer`. Los jugadores se reconocen por el orden en que entran (`player1` es el anfitrión) hasta tener ids de Steam.
- Reloj: `NetworkClock` corre el `GameClock` en el anfitrión; un día dura 15 minutos reales (`Data/Clock/ClockSettings_Proto.asset`, D14).
- Rendimiento medido (semana 8, esta portátil: i5-12450H, RTX 3050 Laptop, 16 GB): anfitrión más 4 clientes con bots, todo en la misma máquina. Con 20 y con 50 reses, el anfitrión y el cliente que dibuja quedan en 60 FPS (p99 de 17 ms; el anfitrión trabaja unos 5 ms por frame), la subida del anfitrión es de unos 14 KB/s con 20 reses y 21 KB/s con 50, y el anfitrión usa unos 680 MB.
- Reses vendidas o perdidas (D29): salen del hato para siempre, pero conservan su índice en `ActiveHerd` (se ocultan), así los ids de red de las demás no se corren. En el prototipo, R repone la manada entera.
- Co-op tipo Stardew Valley (D73): todos comparten el mismo hato, pero cada jugador es independiente.
- Plata (D75, 2026-10-30): compartida del hato por defecto, con opción de separarla por jugador al crear la partida, como en Stardew. El modelo (`Wallets`) soporta los dos modos; la semana 7 solo pone en red la compartida.
- Desarrollador: Johan, solo, ~15 h por semana.

## Stack

| Pieza | Elección |
| --- | --- |
| Motor | Unity 6 LTS, C# |
| Red | FishNet 4.7.3 (instalado por git desde GitHub, no de la Asset Store; la etiqueta 4.7.4 no compila: su plugin Yak viene a medias), anfitrión-cliente |
| Transporte local | Tugboat (incluido en FishNet) |
| Transporte Steam | FishySteamworks + Steamworks.NET; App ID de pruebas 480 hasta pagar Steam Direct. Se monta en la semana 12, no antes; nada del juego debe depender del transporte |
| Pruebas | Unity Test Framework (EditMode y PlayMode), Multiplayer Play Mode |
| CI | GitHub Actions + GameCI |
| Control de versiones | GitHub con Git LFS |
| Plataforma | Solo Windows |
| Render pipeline | URP (plantilla Universal 3D) |

Pendiente de confirmar: UI de la libreta (propuesta: UI Toolkit).

## Reglas de arquitectura (no negociables)

1. **Cada jugador mueve su caballo; el anfitrión manda en todo lo demás** (D74, 2026-10-29; reemplaza "el anfitrión tiene toda la autoridad"). Cada cliente tiene autoridad sobre el movimiento de su propio caballo: aplica su `HorseIntent` localmente y los demás lo ven interpolado. El anfitrión tiene autoridad sobre manada, soga, economía, reloj y guardado: un cliente nunca decide si enlazó una res, si ganó plata o si una res murió; envía una intención con `ServerRpc` y el servidor valida. Cuando la soga arrastre o tumbe a un jinete, el anfitrión se lo ordena al cliente dueño de ese caballo.
2. **Manada en dos niveles.** Reses abstractas (datos por potrero) y reses activas (30–50 con física e IA de manada). Nunca instanciar todo el hato como GameObjects.
3. **La soga física solo existe en el servidor.** A los clientes llegan los dos extremos y la tensión; ellos dibujan la curva con un LineRenderer.
4. **El canto es una grabación, no voz en vivo.** Cada jugador graba su canto una vez (WAV mono 16 kHz, 5–15 s) y lo guarda en su perfil local. Al entrar a una partida se envía en partes al anfitrión, que lo reparte a los demás, una sola vez por sesión. Al mantener el botón suena en 3D para todos y el servidor aplica el efecto en la manada. Sin grabación, se usa el canto fijo del juego. No hay chat de voz.
5. **Reloj, estaciones y guardado viven en el anfitrión.** El guardado es JSON con número de versión desde el primer día.
6. **Separar lógica pura de MonoBehaviours.** Las reglas (economía, reproducción, conteos, hambre, cansancio y sueño) van en clases C# planas que se pueden probar sin escena.

## Estructura

```
Assets/_Project/Scripts/
  Core/      tiempo, estaciones, guardado, eventos
  Net/       conexión, lobbies, transportes
  Herd/      manada abstracta y activa, boids
  Rope/      soga física y visual
  Horse/     control del caballo
  Player/    jinete y acciones a pie
  Voice/     grabación, envío y reproducción del canto
  Economy/   plata, feria, encargos
  UI/        libreta del hato
Assets/Tests/EditMode/  Assets/Tests/PlayMode/
```

- `Assets/_Project/Prototype/`: escenas y scripts **desechables** de las semanas 2–8 (por ejemplo `Proto_Sabana`, que se regenera desde el menú Arreo → Prototype). No construir sobre ellos.
- `Assets/_Project/Data/`: ScriptableObjects con datos de diseño (por ejemplo `Horse/HorseSettings_Proto.asset`, o `Economy/EconomySettings_Proto.asset` con el precio por res, que por ahora es un valor de relleno).
- Una assembly definition por carpeta. Namespace `Arreo.<Carpeta>`, por ejemplo `Arreo.Herd`.
- Las dependencias van de los sistemas hacia `Core`, nunca al revés.

## Convenciones de código

- Código, nombres de clases y comentarios en inglés; términos llaneros sin traducir cuando son el nombre del concepto (`Cabrestero`, `Jaguey`, `HerdPunta`).
- PascalCase para tipos y métodos públicos, `_camelCase` para campos privados, `[SerializeField] private` en vez de campos públicos.
- Datos de diseño (razas, precios, encargos) en ScriptableObjects, no en constantes.
- Nada de `Find*` ni `GetComponent` dentro de `Update`.
- Sin singletons nuevos sin justificarlo en el registro de decisiones.

## Pruebas

- **Test primero** en toda lógica pura: escribir la prueba EditMode antes del código.
- Red y física: pruebas PlayMode y verificación manual con Multiplayer Play Mode (ParrelSync si falla con FishNet).
- Correr las pruebas en local antes de subir, con el Editor cerrado: `.\run-tests.ps1` (EditMode) o `.\run-tests.ps1 -Platform PlayMode`. Toma la versión de Unity de `ProjectVersion.txt`; equivale a:

```
Unity.exe -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results-editmode.xml
```

- **CI activo:** `.github/workflows/tests.yml` corre las pruebas EditMode con GameCI en cada push (y a mano con `workflow_dispatch`). Los resultados salen como check "EditMode Test Results" en GitHub. Secrets: `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`.
- No marcar una tarea como terminada si alguna prueba falla, en local o en el CI.
- **Red:** `ProtoSabanaSceneTests` (PlayMode) crea una partida real con FishNet en la escena. Dos jugadores a mano: Multiplayer Play Mode (Window > Multiplayer > Multiplayer Play Mode, activar Player 2). Medir fluidez con latencia: menú Arreo > Prototype > Build Network Test Player (development build: el simulador de latencia de FishNet no existe en builds normales) y correr dos copias con `-arreoNet host|client -arreoMeasure <s> -arreoLatency <ms por trayecto> -arreoLoss <0-1> -arreoHerd <reses>`. Cada copia escribe un informe (fluidez del caballo remoto y de cada res, bytes recibidos por segundo, la soga y la caída del cliente, warnings). Con `-arreoLatency 75` FishNet mide unos 215 ms de ping (150 simulados más su propio tick).
- **Las pruebas que crean partida guardan en una carpeta temporal** (`TestSaves.UseFolder()` antes de hospedar): si no, el guardado automático al detener el anfitrión pisaría la partida guardada de verdad.
- **Cinco jugadores:** `.\measure-players.ps1 -Herd 20` (o `-Herd 50`) corre el Network Test Player como anfitrión más 4 clientes con bots (`-arreoWaitPlayers`) en esta máquina y resume cada informe: FPS y percentiles, en qué se va el frame (marcadores del Profiler leídos con `ProfilerRecorder`), memoria y bytes recibidos; la subida del anfitrión es la suma de lo que reciben los clientes. MPPM solo llega a 4 jugadores.
- **Dos jugadores automáticos:** las pruebas PlayMode `ProtoFairNetworkTests` y `ProtoSaveTests` hospedan en el Editor y lanzan el Network Test Player como cliente en segundo plano. En la primera, el cliente vende en la feria (`-arreoSellTest <informe>`, `ProtoSaleBot`) y la prueba compara la plata y las reses que ve cada máquina. En la segunda, el cliente se para en un punto conocido (`-arreoGoTo x,z`, `ProtoGoToBot`), se guarda, se cierra todo, se continúa y se comprueba que todo volvió donde estaba. Antes de correr, compila el Network Test Player si alguna fuente de `Assets/_Project` es más nueva que la última compilación de la prueba (marca `Build/NetworkTest/built-for-tests.txt`): esa primera corrida tarda unos minutos más.

## Cómo trabajar en este repo

- Antes de cambiar algo grande, revisar el registro de decisiones. Una decisión sellada (D01–D70) no se cambia en silencio: si hay que tocarla, proponer el cambio y anotarlo con fecha.
- Trabajar dentro de la semana en curso del roadmap; lo que no toca, va a una lista de ideas.
- Commits pequeños, uno por cambio con sentido.
- Si algo del prototipo es desechable (semanas 2–6), decirlo explícitamente en vez de pulirlo.

## Glosario llanero

| Término | Significado |
| --- | --- |
| Hato | Finca ganadera; la base del jugador |
| Punta de ganado | Grupo de reses que se arrea junto |
| Arreo | Mover la manada de un lugar a otro |
| Cabrestero | Quien guía la manada cantando |
| Barajustada | Estampida del ganado |
| Cuatrero | Ladrón de ganado |
| Jagüey | Pozo o depósito de agua para el ganado |
| Morichal | Zona de palmas de moriche junto al agua |
| Coleo | Deporte de tumbar el toro por la cola desde el caballo |
| Chimó | Pasta de tabaco que se masca en el llano |
| Herrar | Marcar el ganado con el hierro del hato |
