# SIMPLE Unity Template

This project allows to adapt a GAMA simulation to a VR environment created with Unity. It provides the VR developer with a game and connection management system, including GameObjects, methods and events that can be hooked. A list of these elements and how to use them is provided in the [**Documentation**](#documentation) section.


A description of the use of the template with a tutorial can be found [here](
https://github.com/project-SIMPLE/simple.toolchain/wiki/02-Tutorial-%E2%80%90-From-GAMA-model-to-Virtual-Universe-%E2%80%90-case-of-a-traffic-model).

## Installation

> [!WARNING]
> The project is being developped using **Unity Editor 6000.3.0f1**. It should work with newer versions also, as it doesn't use any version-specific features (for now), it is strongly recommended to use exactly the same Editor version. It is recommended to install the “Visual Studio Code” and “Android Build Support” modules with Unity.

### Prerequisites

To work properly, we assume that you already have a compatible GAMA model. It is also highly recommended that you install [**Gama Server Middleware**](https://github.com/project-SIMPLE/GamaServerMiddleware) as well.

> [!TIP]
> **For Windows users**, make sure that the folder Assets/Plugins contains a .dll file called websocket-sharp. If not, download it from [this repo](https://github.com/sta/websocket-sharp). And place it in Assets/Plugins in your Unity project.

### What is included

#### Scenes
The project contains different scenes:
 - Code Examples: Set of scenes illustrating some of the features of the SIMPLE tool. Works with GAMA models in the "Code Examples" folder of the GAMA plugin.
    - Limit Player Movement: Show how to limit the player movement in Unity from GAMA.
    - Receive DEM data: Show how to send a grid/matrix from GAMA to Unity and modify it on the fly.
    - Receive Dynamic Data: Show how to send dynamic geometries/agents from GAMA to Unity.
    - Receive Static Data: Show how to send static geometries/agents from GAMA to Unity.
    - Receive Water Data: Show how to send water data to Unity and modify it on the fly.
    - Send Receive Message: Show how to send and receive a message from Unity to GAMA.
    - User Interactions: Show how to define interaction in Unity from GAMA.
 - Menu: A set of scenes with menus that can be manipulated via VR.
    - Startup Menu: Allows to load two Scenes - IP Menu and Main Scene. It allows as well to define if the middleware will be used or not. Using the middleware requires to run another software (the middleware), but allows to connect several players et to follow the connection status of the players.
    - IP Menu: Allows to change the IP used to connect to the computer running the middleware/GAMA
    - End of Game Menu : Displays information to the player and allows to restart the game.
 - Demo: present a complete experience of game with interactions with GAMA.
    - Single Player Game: a simple game connected with GAMA where the player can move, select cars and motorbikes to remove then, grab a tree, select a building bloc to define it as a hotspot (to attract cars and motorbikes).
    - Multi-Player Game: a multi-player game where each player has to collect treasures. The player that collect the highest number of treasures is the winner. The synchronization between the players is done through GAMA.

In addtion, two scene templates are provided to create new games:
 - Main Scene - FPS player: define a basic scene with a FPS-type (First-Person) player
 - Main Scene - Sky View player: define a basic scene with a Sky View-type player

#### Resources
Composed of two types of elements:
 - Materials: includes different materials, especially for water and terrain.
 - Prefabs: includes different prefabs:
     - GAMA Link: Prefabs dedicated to the connection with GAMA, in particular "Connection Manager" to manage the connection between Unity and GAMA, and "Game Manager" to manage the game and the messages sent to/from GAMA.
     - Player: Prefabs dedicated to the management of the VR player. It includes two types of players - FPS player, where the player walks on a ground and can teleport and Sky view player, where the player flies in the sky (no gravity) and can move horizontally and vertically.
     - Utils: contains a Debug overlay that will display all the elements display in the console (using the Debug.Log() method).
     - Visual Prefabs: contains a set of 3D assets that can be used for different purposes.



### Quick Start

1. Download the simple.template.project ([here](https://github.com/project-SIMPLE/simple.toolchain/archive/refs/heads/Unity-6.zip))
2. Import it as a Unity project. **Make sure to use the right Editor version (Unity Editor Unity 6.3)**.
   <img width="1056" alt="Capture d’écran 2026-01-09 à 10 57 40" src="https://github.com/user-attachments/assets/f47bdea1-fa3e-435b-8093-89766091d8f0" />
4. In the Menu "File" select "Build Profiles"
<img width="1027"  alt="Capture d’écran 2026-01-09 à 10 59 05" src="https://github.com/user-attachments/assets/080fc718-2115-4a89-9d00-577b28d9cbf1" />

6. Select "Android" in "Platform", then click on "Switch Platform". You can after build and deploy the application on the headset by clicking on "Build and Run".

<img width="793" alt="Capture d’écran 2026-01-09 à 11 09 11" src="https://github.com/user-attachments/assets/d49ee2dd-fb29-4169-8847-73acda1c92a0" />

7. To run the application in conjunction with GAMA, make sure you have installed [GAMA 2025.06](https://gama-platform.org/download) and the [Unity Plugin for GAMA](https://github.com/project-SIMPLE/simple.toolchain/tree/Unity-6/GAMA%20Plugin). Information on installing the plugin is available [here](https://github.com/project-SIMPLE/simple.toolchain/tree/Unity-6/GAMA%20Plugin#installation). The plugin provides a set of model (added in Plugin models/LinkToUnity) that works with the Unity project.


## Building and deploying AEDES to a Quest headset

> This section covers the **AEDES game** in this repository, not the upstream SIMPLE
> template. Everything below runs from a Mac with no Windows machine required.

Two committed scripts cover the whole loop. There is no separate "make an APK" step:
Unity's player build *is* the APK, written signed and ready to sideload.

```bash
Tools/build.sh  2            # build module 2 -> build/Android/AEDES-Module2.apk
Tools/deploy.sh 2            # install it on the attached headset and launch it

Tools/build-and-deploy.sh 2  # both in one command
```

### Modules

Each module is built and deployed separately for now.

| Key | Module | Scene |
|---|---|---|
| `1` | Play as the mosquito | `Module_1_MainScene` |
| `2` | Household vector control | `Module_2_MainScene` |
| `tut2` | Module 2 tutorial | `Tutorial_M2` |

Scenes are located by filename, so moving or renaming their folders will not break the
scripts. Build Settings are ignored: each build contains only that module's scene.

> **Only one module can be installed at a time.** They all share the application id
> `com.unity.template.vr`, so deploying module 2 replaces module 1 on the headset.

### Requirements

- **Unity 6000.3.8f1** with the **Android Build Support** module (IL2CPP + SDK/NDK).
  The version is read from `ProjectSettings/ProjectVersion.txt`; the scripts refuse to run
  with a different one, because opening the project in another version rewrites asset files.
- **Rosetta 2** on Apple silicon: `softwareupdate --install-rosetta --agree-to-license`.
  A macOS major upgrade removes it, after which Unity exits silently with no log.
- `adb` is found automatically inside the editor's bundled Android SDK; it does not need
  to be on your `PATH`.

### Preparing the headset (once)

1. Create a **developer team** at [developers.meta.com](https://developers.meta.com) and
   verify the account (SMS two-factor, or a payment method).
2. Turn on developer mode in the **Meta Horizon phone app**: headset icon → your headset →
   *Headset settings* → *Developer mode*. There is **no in-headset toggle** for this.
3. Connect a **data-capable** USB-C cable, put the headset on, and accept
   *Allow USB debugging*, ticking **Always allow from this computer**.

The headset must be **awake** to launch an app. Horizon OS also refuses to start one while
a system dialog is open, so `Tools/deploy.sh` retries until it succeeds or times out
(`--wait <seconds>`, default 180).

The installed app appears in the headset library under **Unknown Sources**, named
**SIMPLE Template** — the template branding was never changed, so do not look for "AEDES".

### Clean builds

`Tools/build.sh <module> --clean` forces IL2CPP to recompile from scratch. This takes
several minutes rather than the usual two or three, and is occasionally necessary: a build
that crashes part-way can leave the IL2CPP cache holding a stub `libil2cpp.so`, after which
every incremental build happily reuses it and still reports success. The resulting APK
installs and starts the engine, then dies, because it contains no compiled C# at all.

You should not normally need the flag. `Tools/build.sh` checks `libil2cpp.so` inside the
finished APK and re-runs itself with `--clean` automatically if it finds a stub, and
`Tools/deploy.sh` refuses to install one.

### Troubleshooting

| Symptom | Cause |
|---|---|
| `no headset detected over adb` | Developer mode off, or a charge-only cable. An empty device list means the ADB interface is absent entirely, not that authorization is pending. |
| `headset is connected but unauthorized` | The *Allow USB debugging* prompt has not been accepted. Put the headset on. |
| Build fails, log mentions `cannot be extracted by the YAML Parser` | A `.meta` file has git conflict markers committed into it. Find them with `grep -rlE '^<{4,}' Assets --include='*.meta'` and keep the GUID the live scenes reference — never just delete the markers at random, as the wrong GUID silently breaks every reference to that asset. |
| App installs but never starts | Headset asleep, or a system dialog is open in it. |
| Changes missing from the build | Unity compiles scripts in roughly the first 90 seconds. Anything saved after that misses the build with no warning. Do not edit while a build runs. |

Build logs are written to `Logs/build/module-<key>.log`.


## Documentation

This section focuses only on the C# scripts which are useful for a Unity developer. The scripts not mentioned here are at least commented.
**Important note:** As all the scripts whose name finishes by "Manager" are instantiated when Unity is launched in the "Managers" GameObject, they are all developed using the Singleton Pattern. Hence trying to instantiate in some external scripts could break the default mechanisms. To call a method from one of these classes, one should rather use the following code snippet :

```csharp
NameOfClassManager.Instance.SomeMethod();
```

### WebSocketConnector

Base abstract class to establish a web-socket connection with GAMA. All the methods of this class are private or protected. Hence they are only accessible through a child class (ConnectionManager here).
Theorically, in most cases, **one mustn't try to access the methods of this class**, as they are alreay used/overriden by ConnectionManager.

**Abstract Methods:**
- `HandleConnectionOpen` : triggered when a web-socket connection is established.
- `HandleReceivedMessage` : triggered when a message is received from the server.
- `HandleConnectionClosed` : triggered when the connection is closed, either by the server or by Unity itself.

### ConnectionManager

This class extends WebSocketConnector and implements the methods mentioned above. The corresponding script is already in a GameObject called "Connection Manager", which has to be integrated in the Main Scene.
It is in charge of creating an ID for the player once the connection with GAMA is established. Moreover, it provides the Unity developer with a state machine implemented as an `enum` to handle each stage of the connection process. The specific role of each state is explained in the script source code. Some useful events allow the developer to to handle connection transitions and informations.

**Events:**
- `OnConnectionStateChange<ConnectionState newState>` : Triggered when a transition from one connection state from another occurs.
- `OnConnectiontStateReceived<JObject payload>` : Triggered when Unity receives a Json message from the server, which "type" field holds "json_state". For further informations about the payload detail, please refer to GamaServerMiddleware documentation
- `OnConnectionAttempted<boolean connectionSuccess>` : Triggered when a Json object with type "json_state" is received from the server, after Unity attempted to connect to it using `TryConnectionToServer` method. The boolean `connectionSuccess` contains true if the connection was successfully established, false otherwise.
- `OnServerMessageReceived` : Triggered when Unity receives a Json message from the server, which "type" field holds "json_simulation". For further informations about the payload detail, please refer to GamaServerMiddleware documentation.

**Methods:**
- `UpdateConnectionState(ConnectionState newState)` : Changes the current connection state to `newState`. Calling this method should be avoided whenever possible, as it could break the default connection process, leading to some undefined state.
- `TryConnectionToServer` : Attemps a connection to the middleware or to GAMA
- `IsConnectionState(ConnectionState currentState)` : Checks current state.
- `SendExecutableExpression(string expression)` : Allows to send an expression to GAMA through the middleware or directlty to GAMA. The expression is compiled and executed in the experiment context. :warning: Beware of the arguments expected by GAMA and special characters required by GAMA (such as `;`, `"`, ...) as the expression is executed as it is sent by Unity.
- `SendExecutableAsk(string action, Dictionary<string,string> arguments)` : Allows you to ask one of the  agents of the simulation to trigger an action (defined by its name), the second argument represents the values of the action arguments given by a dictionary (key: name of the argument, value: value of the argument). :warning: unlike SendExecutableExpression, the expression is not compiled, which is less time-consuming for GAMA, but only allows you to send simple values for the action's argument values and not complex expressions.
- `GetConnectionId` : Returns the ID created by Unity when the connection was established. This one is based on the IP of the headset.

### SimulationManager

This is the core script of this package. It allows to manage the actions triggered by the messages received by GAMA.

**Events**:

- `OnGameStateChanged<GameState newGameState>` : Triggered when a transition from one GameState to another occurs.
- `OnGameRestarted` : Triggered when the function `RestartGame` is called.
- `OnGeometriesInitialized<GAMAGeometry geometries>` : Triggered when the initial geometries sent by GAMA are converted into polygons in the Unity scene. By default, `OnGameStateChanged` is triggered just after this event, to switch from the LOADING_DATA state to the GAME state. Hooking to this event allows to seperate the logic between the game state transition and the loading of geometries.
:warning: This event is called when incoming geometric data is successfully managed and NOT when it is received.

**Methods**:

- `void UpdateGameState(GameState newState)` : Changes the current game state to `newState`. This method must be used with caution, as it could break the default game logic, leading to errors in the execution of crucial steps such as initialization or connection steps.
- `GameState GetCurrentState` : Returns the current game state
- `bool IsGameState(GameState state)` : Compares the current game state with the one specified as a parameter.
