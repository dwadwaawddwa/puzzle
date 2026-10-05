# Puzzle Studio — Architecture (Jalon 1, proposition)

> Statut : **en attente de validation**. Rien n'est encore codé pour cette nouvelle version.
> Projet : ce dépôt (Unity 6000.0.32f1, Built-in RP, Mono).

---

## 0. Ce qu'on fait de l'ancien code

L'ancien générateur (`Assets/Scripts`, `Assets/Editor`, `Assets/_Generated`, `Assets/Images`, `Build/`) est
**déplacé dans `_Legacy/` à la racine du projet** (hors `Assets/` → Unity ne le compile plus, mais rien n'est perdu).
On repart d'un `Assets/` propre. Les seules choses réutilisées sont les idées qui marchaient
(mélange soluble du taquin, découpe d'image, workflow batchmode).

---

## 1. Choix techniques

| Sujet | Choix | Pourquoi |
|---|---|---|
| Pipeline de rendu | **Built-in** (pas d'URP) | Plus léger, shader maison simple, 60 FPS Steam Deck faciles. |
| Backend de script | **Mono** par défaut, IL2CPP en option | Le module IL2CPP n'est pas installé sur ta machine (il faut l'ajouter via Unity Hub + workload C++ de VS 2022). Le menu `Build` bascule tout seul si le module est présent. |
| UI | **UI Toolkit** (UXML/USS + C#) pour jeu et Studio | Demandé. |
| Thème runtime | `ThemeService` qui applique les couleurs/rayons/polices **en C# sur les classes USS** (`.pz-primary`, `.pz-surface`...) | ⚠️ Unity ne permet pas de modifier les *variables* USS (`--color-primary`) au runtime. Les USS gardent la mise en page, le C# pousse les valeurs du thème. Même résultat visuel, instantané. |
| Plateau | Monde 2D, caméra orthographique, **1 mesh par pièce avec UV sur une seule texture** + `PieceShader` (coins arrondis SDF, bordure, ombre, lueur, fondu des bordures à la victoire) | Demandé ; 1 matériau partagé + `MaterialPropertyBlock` → batching, zéro alloc. |
| JSON | `com.unity.nuget.newtonsoft-json` | Champs `null`, enums en texte, migration facile. |
| Entrées | Input System 1.11 (déjà présent) ; détection auto du dernier appareil (souris/clavier/Xbox/PS) | Icônes de boutons qui changent. |
| Aperçu live du Studio | Le runtime `Game` rend dans une **RenderTexture** (caméra plateau + `PanelSettings.targetTexture` pour l'UI) affichée au centre du Studio ; les clics sont retransmis | Même code que le jeu exporté → ce que tu vois = ce que le joueur voit. Résolutions simulées : 1920×1080, 1280×800, 2560×1080, 1024×768. |
| Icône de l'exe | **P/Invoke Win32 `BeginUpdateResource/UpdateResource/EndUpdateResource`** écrit en C# dans le Studio (+ PNG→ICO multi-tailles 16…256 maison) | Pas de binaire externe à télécharger. `rcedit` reste un plan B documenté. |
| Boîtes de dialogue fichiers | **P/Invoke Windows (`IFileOpenDialog`)** maison | Évite StandaloneFileBrowser (dll à télécharger), même rendu natif. |
| Glisser-déposer depuis l'Explorateur | Hook Win32 `DragAcceptFiles` + sous-classement de la fenêtre (code maison, Windows uniquement) | Unity ne le gère pas nativement en build. |
| Titre de fenêtre / dossier de save | Template compilé avec un nom générique ; au lancement le jeu renomme sa fenêtre (`SetWindowText`) et sauvegarde dans `persistentDataPath/<Titre>/` | Le `productName` est figé dans le template compilé ; c'est la méthode fiable sans recompiler. |
| Splash « Made with Unity » | Désactivé (autorisé en Unity 6 Personal) | Notre propre splash (logo dev + titre). |
| Sons/musiques par défaut | **Générés par code** (synthèse → WAV/OGG dans `Assets/_Project/Audio/`) : SFX clic/snap/victoire, 2-3 nappes d'ambiance douces | 100 % libres de droits sans téléchargement. Qualité correcte mais simple — tu pourras importer tes musiques dans le Studio. |
| Polices | Polices **OFL** (Nunito, Inter, Fredoka, Playfair…) depuis Google Fonts | Nécessite un téléchargement → **je te demanderai l'accord** au jalon 3. |
| Tests | `com.unity.test-framework`, EditMode + PlayMode, lancés en batchmode (`-runTests`) | Chaque jalon : compile + tests verts. |
| Steam | Steamworks.NET 2025.164.1 (git package) ; code compilé seulement si le paquet est là (`PUZZLE_STEAMWORKS`, versionDefines de PuzzleGame.asmdef) | Jalon 8 ✔. Sans App ID / sans Steam → `NullSteamService`. |

### Ce qui est impossible / déconseillé (franchement)
- **Compiler un jeu sans Unity** : impossible → d'où le Player Template (comme tu l'as prévu). ✔
- **Variables USS au runtime** : voir ci-dessus, contourné.
- **Tester un vrai Steam Deck** : je ne peux que simuler 1280×800 + manette ; à valider par toi sur l'appareil.
- **Captures d'écran du jeu** : ma capture voit le rendu GPU en noir → je vérifie via logs + tests automatiques, et tu valides visuellement.
- **Icône dans la barre des tâches pendant le jeu** : vient de l'exe (patché ✔). L'icône de la fenêtre vient aussi de l'exe ✔.

---

## 2. Arborescence

```
PuzzleGenerator/
  Assets/
    _Project/
      Core/            PuzzleCore.asmdef      (aucune dépendance UI, refs: Newtonsoft)
        Data/          GamePackData, GameInfo, GameplayConfig, ThemeConfig, BackgroundConfig, PieceStyle,
                       FontConfig, UIStyle, ParticleConfig, AudioConfig, LevelConfig, CropRect, StarRules,
                       ExportConfig, SteamConfig, AchievementDef, enums (PuzzleModeId, DifficultyCurve, UnlockRule…)
        Pack/          GamePackLoader, GamePackWriter, PackValidator (+ ValidationReport), PackVersionMigrator,
                       PackPaths, ColorUtil (hex, WCAG contrast)
        Puzzle/        IPuzzleMode, PuzzleModeRegistry, PuzzleState, PuzzleMove, BoardLayout,
                       ShuffleService (seed), SolvabilityChecker, ParCalculator, GridResolver (courbe de difficulté)
        Modes/         SwapTilesMode, StripsMode, SlidingMode, RotateMode   (logique pure, sans Unity UI)
        Save/          SaveSystem (JSON + .bak + récupération), PlayerProgress, LevelRecord, SettingsData
        Localization/  LocalizationService, (en.json par défaut en Resources)
        Audio/         IAudioService, AudioService (crossfade, pool SFX, variation de pitch)
        Steam/         ISteamService, NullSteamService, SteamService (#if STEAM_ENABLED), AchievementGenerator
        Util/          ImageSlicer, TextureLoader (PNG/JPG runtime + downscale), Tween (maison, sans alloc),
                       SeededRandom, Easing, Win32/ (WindowTitle, FileDialogs, DragDrop, IconPatcher)
      Game/            PuzzleGame.asmdef      (refs: Core, InputSystem)
        Bootstrap/     GameBootstrap (args `-pack`, StreamingAssets/GamePack, init services), ServiceHub
        Screens/       ScreenRouter + SplashScreen, MainMenuScreen, LevelSelectScreen, GameplayScreen,
                       VictoryScreen, SettingsScreen, CreditsScreen, PauseScreen, EndScreen  (UXML + C#)
        Gameplay/      BoardView, PieceView, InputController (souris/clavier/manette/tactile, curseur grille),
                       HintSystem, PreviewOverlay, GameSession (coups, chrono, undo, étoiles)
        FX/            ParticleFactory, Confetti, PieceSnapFX, ScreenShake, BackgroundAnimator (shaders de fond)
        UI/            ThemeService, DeviceIconService, composants (PzButton, PzPanel, PzToggle, PzSlider…), USS
      Studio/          PuzzleStudio.asmdef    (refs: Core, Game)
        StudioApp/     StudioBootstrap, ProjectManager (new/open/recents), StudioProject (.puzzleproj),
                       AutoSave, CommandStack (undo/redo), StudioLog, Shortcuts
        Panels/        ProjectPanel, LevelsPanel, GameplayPanel, ThemePanel, AudioPanel, TextsPanel,
                       SteamPanel, ExportPanel
        Preview/       LivePreview (RenderTexture + relais d'entrée), PreviewScreenSelector, ResolutionSelector
        Export/        GameExporter (pipeline + progression), ExeRenamer, IconInjector, PngToIco,
                       PackOptimizer (redimensionne images), StoreAssetsGenerator, ZipExporter
        Widgets/       ColorPicker (HSV + hex + palette + pipette), GradientEditor, ImageCropper,
                       ReorderableThumbList, FileBrowser (wrap Win32)
        Themes/        ThemePresets (8 presets), PaletteExtractor (k-means), ThemeRandomizer (harmonies + WCAG)
      Shaders/         Piece.shader, Background.shader (gradient/animé/motifs/flou), Blur.shader
      Art/ Audio/ Fonts/
      Resources/       DefaultTheme, en.json, sons/polices par défaut référencés par "default:xxx"
      Scenes/          Boot.unity, Game.unity, Studio.unity   (générées par script éditeur, jamais montées à la main)
    Editor/
      BuildTools/      BuildMenu (Build > Player Template / Studio / All), BuildPipeline batchmode,
                       SceneGenerator, SampleImageGenerator, DefaultAudioGenerator
    Tests/
      EditMode/        Shuffle, Solvability (parité taquin), victoire 4 modes, par, JSON roundtrip,
                       migration v0→v1, validator, contraste WCAG, undo
      PlayMode/        charger SamplePack → jouer un niveau scripté → victoire → save relue
  SamplePacks/         CozyPastel/, DarkNeon/, MinimalWhite/   (images générées : dégradés, motifs, formes)
  Tools/               (rcedit plan B si besoin)
  build.bat            build.bat template | studio | all | test
  README.md  HOW_TO_CUSTOMIZE.md  CHANGELOG.md  ARCHITECTURE.md
  _Legacy/             ancien générateur (non compilé)
```

Sortie des builds :
```
Build/
  Template/   Game.exe, Game_Data/, UnityPlayer.dll, MonoBleedingEdge/ …
  PuzzleStudio/
    PuzzleStudio.exe, PuzzleStudio_Data/ …
    Template/   (copie de Build/Template)
```

---

## 3. Classes principales

```csharp
// Core/Puzzle
public interface IPuzzleMode {
    PuzzleModeId Id { get; }
    void Setup(BoardLayout board, ModeSettings settings);   // grille, nb de bandes, orientation…
    void Shuffle(int seed);                                 // reproductible, jamais résolu, >= minMisplacedRatio
    MoveResult HandleInput(PuzzleInput input);              // Select / Drag / Drop / Rotate(cw|ccw) / Direction
    bool IsSolved();
    Hint GetHint();                                         // pièce mal placée + destination
    int GetParMoves();
    bool Undo();
    IReadOnlyList<PieceState> Pieces { get; }               // position courante, rotation, verrouillée…
    event Action<PuzzleMove> OnMove;
    event Action<int> OnPieceCorrect;
    event Action OnSolved;
}
// Enregistrement : [PuzzleMode("Sliding")] sur la classe → PuzzleModeRegistry la trouve par réflexion.
```

- **Logique (Core)** ≠ **affichage (Game)** : `BoardView` écoute `OnMove` et anime les `PieceView` ; les modes ne connaissent pas Unity UI → tout est testable en EditMode.
- **ServiceHub** : registre simple (pas de framework DI) : `Pack`, `Save`, `Audio`, `Loc`, `Theme`, `Steam`, `Input`.
- **ScreenRouter** : pile d'écrans UI Toolkit avec transitions (fondu/glissé selon `animationSpeed`, désactivées si « réduire les animations »).
- **GameRuntime** : objet racine instanciable N fois → le Studio en crée un pour l'aperçu, avec un `GamePackData` **en mémoire** (pas besoin d'écrire sur disque à chaque modif).

---

## 4. Schéma complet de `game.json` (v1)

```jsonc
{
  "packVersion": 1,
  "game": {
    "title": "Cozy Puzzles",              // obligatoire
    "subtitle": "Relaxing picture puzzles",
    "developer": "My Studio",
    "version": "1.0.0",
    "steamAppId": 0,                      // 0 = pas de Steam
    "defaultLanguage": "en",
    "logo": null,                         // "theme/logo.png" ou null → titre texte stylisé
    "devLogo": null,                      // logo du studio pour le splash
    "credits": [ { "role": "Game design", "names": ["Me"] } ]
  },
  "gameplay": {
    "defaultMode": "SwapTiles",           // SwapTiles | Strips | Sliding | Rotate
    "difficultyCurve": "Progressive",     // Fixed | Progressive | Custom
    "minGrid": 3, "maxGrid": 7,           // Progressive : interpolé du 1er au dernier niveau
    "fixedGrid": 4,                       // Fixed
    "stripsCount": { "min": 4, "max": 12 },
    "stripsOrientation": "Vertical",      // Vertical | Horizontal
    "rotateSteps": [90, 180, 270],
    "lockCorrectPieces": true,            // SwapTiles : verrouille les tuiles bien placées
    "minMisplacedRatio": 0.8,
    "unlockRule": "Sequential",           // Sequential | AllUnlocked | ByStars
    "starsToUnlockPerLevel": 2,           // ByStars
    "showTimer": true, "showMoves": true,
    "allowPreview": true, "allowHints": true, "allowUndo": true,
    "maxHintsPerLevel": 3,
    "starRules": { "threeStarsMoveFactor": 1.2, "twoStarsMoveFactor": 2.0, "hintCostsStar": true }
  },
  "theme": {
    "preset": "CozyPastel",               // informatif : les valeurs ci-dessous font foi
    "colors": {
      "background": "#F6EFE7", "surface": "#FFFFFF", "primary": "#E07A5F", "secondary": "#81B29A",
      "text": "#3D405B", "textMuted": "#8D8FA6", "accent": "#F2CC8F",
      "success": "#6BBF59", "highlight": "#FFD166", "highlightColorblind": "#3A86FF"
    },
    "background": {
      "type": "AnimatedGradient",         // Solid | Gradient | AnimatedGradient | Pattern | Image | BlurredLevel
      "image": null,
      "gradient": ["#F6EFE7", "#EADBC8"], "gradientAngle": 135, "animationSpeed": 0.2,
      "pattern": "Dots",                  // None | Dots | Stripes | Grid | Waves
      "patternOpacity": 0.06, "patternScale": 1.0,
      "blurLevelImage": false, "vignette": 0.15
    },
    "pieces": {
      "cornerRadius": 8, "gap": 4, "borderWidth": 2, "borderColor": "#FFFFFF",
      "shadow": true, "shadowStrength": 0.25, "shadowOffset": 4,
      "correctGlow": true, "glowColor": null,           // null → colors.success
      "hoverScale": 1.04, "liftScale": 1.08
    },
    "font": { "heading": "Default:Rounded", "body": "Default:Clean" },   // ou "theme/custom_font.ttf"
    "uiStyle": {
      "buttonShape": "Pill",              // Pill | Rounded | Square
      "panelStyle": "Soft",               // Soft | Flat | Outlined | Glass
      "cornerRadius": 16, "animationSpeed": 1.0, "titleAnimation": "Float"   // None | Float | Pulse
    },
    "particles": { "victory": "Confetti", "snap": "Sparkle", "intensity": 1.0 }  // Confetti|Stars|Bubbles|None
  },
  "audio": {
    "musicMenu": "default:calm_01", "musicGame": "default:calm_02",   // ou "audio/music_menu.ogg"
    "musicVolume": 0.6, "sfxVolume": 0.8, "crossfadeSeconds": 1.5,
    "sfx": { "click": "default:click", "pick": "default:pick", "drop": "default:drop",
             "snap": "default:snap", "victory": "default:victory", "star": "default:star",
             "locked": "default:locked" },
    "pitchVariation": 0.08
  },
  "steam": {
    "cloudEnabled": false,
    "richPresence": true,
    "achievements": [ { "id": "ACH_FIRST_PUZZLE", "name": "First Puzzle",
                        "description": "Complete your first puzzle.", "rule": "LevelsCompleted", "value": 1,
                        "hidden": false } ]   // vide → générés automatiquement
  },
  "levels": [
    { "id": "lvl_01", "image": "levels/01.png", "name": "Sunset",
      "mode": null,                       // null = gameplay.defaultMode
      "grid": null,                       // null = courbe ; sinon { "cols": 4, "rows": 3 } ou { "strips": 8 }
      "crop": { "x": 0, "y": 0, "w": 1, "h": 1 },   // normalisé 0..1
      "seed": null }                      // null = dérivée de l'id
  ]
}
```

**`locale/en.json`** : `{ "menu.play": "Play", "menu.continue": "Continue", ... }` — n'importe quelle clé
surcharge les textes par défaut ; `locale/fr.json` etc. ajoute une langue.

**Projet Studio** : `MonJeu.puzzleproj/` = `project.json` (réglages d'export : nom exe, icône, dossier de sortie, zip)
+ `pack/` (le Game Pack source, au format ci-dessus) + `.autosave/`.

**Migration** : `PackVersionMigrator` applique `v0→v1→v2…` sur le JSON brut (JObject) avant désérialisation ;
chaque nouveau champ a une valeur par défaut dans la classe C#.

**Validator** (erreurs ⛔ / avertissements ⚠️) : titre vide, aucun niveau, image manquante/illisible, image < 512 px,
ratio > 3:1, doublons (hash), id en double, grille impossible, contraste texte/fond < 4.5:1 (WCAG AA),
primary/surface < 3:1, police/son introuvable, appId non numérique, nom d'exe invalide.

---

## 5. Export (rappel du pipeline)

1. Copie `PuzzleStudio/Template/` → `<sortie>/<Nom>/`
2. `Game.exe` → `<Nom>.exe`, `Game_Data` → `<Nom>_Data` (Unity trouve automatiquement `<NomExe>_Data`)
3. Icône : PNG → ICO (16,24,32,48,64,128,256) → injectée dans `<Nom>.exe` (Win32 UpdateResource)
4. Pack optimisé → `<Nom>_Data/StreamingAssets/GamePack/` (images redimensionnées max 2048 px, JPG/PNG)
5. `achievements_steamworks.txt` + option `.zip`
6. Ouvre l'Explorateur sur le dossier

Le jeu accepte aussi `-pack "C:\chemin\GamePack"` pour tester sans exporter.

---

## 6. Jalons

1. Architecture ← **on est ici**
2. Core + SwapTiles + SamplePack + tests
3. Jeu complet (écrans, save, options, loc EN, thème, audio)
4. Polish / juice (shader, fonds animés, victoire)
5. Strips, Sliding, Rotate
6. Studio v1 (projets, Levels, aperçu live, Theme, Export complet)
7. Studio v2 (presets, palette, randomize, Audio, Texts, recadrage, undo/redo)
8. Steam (Steamworks.NET, succès, Rich Presence, assets boutique)
9. Manette / Steam Deck / accessibilité
10. Passe qualité finale
