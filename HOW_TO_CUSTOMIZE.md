# Comment personnaliser Puzzle Studio

> Document complété à chaque jalon. Les sections marquées ⏳ arrivent avec les jalons suivants.

## Modifier un jeu sans toucher au code : `game.json`

Tout le contenu d'un jeu vient de son Game Pack (`SamplePacks/<Nom>/game.json` + `levels/`).
Le schéma complet et commenté est dans [ARCHITECTURE.md § 4](ARCHITECTURE.md).
Pour tester un pack modifié : `play_sample.bat <Nom>` ou `Build\Template\Game.exe -pack "<dossier du pack>"`.

Exemples :
- changer la difficulté : `gameplay.difficultyCurve` (`Fixed` / `Progressive` / `Custom`), `minGrid`, `maxGrid`, `fixedGrid` ;
- forcer une grille sur un niveau : `"grid": { "cols": 6, "rows": 4 }` dans le niveau ;
- couleurs : `theme.colors.*` (hex `#RRGGBB`) ;
- pièces : `theme.pieces.cornerRadius`, `gap`, `borderWidth`, `borderColor` (en pixels de référence 1080p) ;
- polices : `theme.font.heading` / `body` = `Default:Rounded`, `Default:Clean`, `Default:Playful`, `Default:Elegant` ;
- boutons/panneaux : `theme.uiStyle.buttonShape` (`Pill`/`Rounded`/`Square`), `panelStyle` (`Soft`/`Flat`/`Outlined`/`Glass`).

## Ajouter un nouveau mode de puzzle

1. Crée `Assets/_Project/Core/Modes/MonMode.cs` :
   ```csharp
   [Preserve]
   [PuzzleMode("MonMode", Shape = GridShape.Grid, MinGrid = 2)]
   public sealed class MonMode : PuzzleModeBase
   {
       public override string Id => "MonMode";
       public override bool CanUndo => ...;
       protected override void ClearHistory() { ... }
       public override void Shuffle(int seed) { /* ShuffleService + ApplyArrangement(...) */ }
       public override MoveResult HandleInput(in PuzzleInput input) { /* modifier l'état puis Commit(...) */ }
       public override Hint GetHint() { ... }
       public override int GetParMoves() { ... }
       public override bool Undo() { ... }
   }
   ```
   Options de l'attribut : `Shape` (Grid ou Strips), `SquareCells` (cases carrées, pour tourner des pièces),
   `MinGrid` / `MaxGrid` (bornes par côté), `GridFactor` (grille plus petite/grande que la courbe de difficulté).
   Surcharge aussi `DragStyle` (`Swap`, `Insert`, `Slide` ou `None`) pour choisir comment le plateau réagit au glisser.
   Exemples complets : `SwapTilesMode`, `StripsMode`, `SlidingMode`, `RotateMode` dans `Assets/_Project/Core/Modes/`.
2. C'est tout : le registre (`PuzzleModeRegistry`) le trouve par réflexion. Il devient utilisable avec
   `"defaultMode": "MonMode"` ou `"mode": "MonMode"` sur un niveau.
3. Ajoute la phrase d'aide `"mode.MonMode.help"` dans `Resources/Localization/en.json` et `fr.json`.
4. Ajoute des tests dans `Assets/Tests/EditMode/` sur le modèle de `ModesTests.cs`.

Règles : la logique d'un mode ne connaît pas l'affichage. `BoardView` relit `Pieces` (case, rotation, verrouillage)
après chaque `OnMove`. Utilise `SupportsLocking`, `SwapCells`, `ApplyArrangement`, `Commit` de `PuzzleModeBase`.

## Ajouter une langue

1. Copie `Assets/_Project/Resources/Localization/en.json` en `fr.json` (même dossier) et traduis les valeurs.
2. Ou, pour un seul jeu : ajoute `locale/fr.json` dans le Game Pack (il surcharge les textes intégrés) et mets
   `"defaultLanguage": "fr"` dans `game.json`.

## Déplacer les éléments de l'écran (Layout)

- Dans le Studio, onglet **Layout** : choisis l'écran (Gameplay ou Main menu), puis glisse les blocs dans l'aperçu.
  Poignée bleue = taille. Le jeu est en pause pendant l'édition.
- Les positions sont enregistrées dans `game.json`, section `layout` :
  `"layout": { "gameplay": { "title": { "x": 0.03, "y": 0.03, "scale": 1, "visible": true } }, "menu": { … }, "boardArea": { "x": 0.17, "y": 0.14, "w": 0.66, "h": 0.72 } }`
  (`x`, `y` = position du point d'ancrage du bloc, de 0 à 1 sur la largeur/hauteur de l'écran).
- **Ajouter un bloc déplaçable** : dans l'écran (C#), entoure-le avec `LayoutService.Tag(monElement, "monId")`,
  puis déclare-le dans `LayoutService.Slots` (`Assets/_Project/Game/UI/LayoutService.cs`) avec son ancrage et sa
  position par défaut. Il apparaît automatiquement dans l'onglet Layout.

## Couleurs selon les images

- Theme → *From pictures* : `"autoColors": "Off" | "Background" | "Full"` dans `game.json` (`theme`).
- Le calcul est dans `Assets/_Project/Core/Util/ThemeDerivation.cs` (clarté du fond, saturation, contraste minimum) :
  c'est là qu'il faut ajuster si tu veux des fonds plus ou moins colorés.

## Effets visuels (fonds, ombres, particules)

- **Fond** : `Assets/_Project/Shaders/Background.shader` (dégradé, animation, motifs `_Pattern` 1 à 4, vignette).
  Ajouter un motif = un nouveau `else if (_Pattern > 4.5 …)` dans le shader + une valeur dans l'enum
  `BackgroundPattern` (`Core/Data/Enums.cs`) ; il apparaît automatiquement dans le Studio.
- **Particules** : `Assets/_Project/Game/FX/ParticleFactory.cs` — `PlaySnap` (pièce bien placée) et `PlayVictory`
  (`VictoryConfetti`, `VictoryStars`, `VictoryBubbles`). Les quantités, vitesses et durées sont en haut de chaque méthode ;
  les formes (rectangle, étoile, bulle, éclat) sont dessinées dans `Texture(...)`.
- **Ombres** : force/décalage dans `theme.pieces` (`shadow`, `shadowStrength`, `shadowOffset`) ; le rendu est dans
  `PieceView.UpdateShadow()`.

## Ajouter un preset de thème

Dans `Assets/_Project/Studio/Themes/ThemePresets.cs` :
1. ajoute l'identifiant dans `Names` (et un joli nom dans `DisplayName`) ;
2. ajoute un `case "MonPreset":` dans `Create()` en t'inspirant des autres (`Colors(...)`, `Bg(...)`, `Pieces(...)`,
   `Ui(...)`, `Fonts(...)`) ;
3. lance `build.bat test` : le test `Presets_AreAllReadable` vérifie que le texte reste lisible (contraste WCAG ≥ 4.5),
   puis `build.bat studio`.

## Publier sur Steam (étapes générales)

1. Dans le Studio, onglet **Export** → **Export Game**. Le dossier `Documents\PuzzleStudio Exports\<NomDuJeu>\` est le jeu complet.
2. Sur partner.steamgames.com : crée l'application (frais Steam Direct), note l'**App ID** et le **Depot ID** Windows.
3. Télécharge le **Steamworks SDK** ; dans `sdk/tools/ContentBuilder/` copie le contenu du dossier exporté dans `content/`
   et adapte les scripts `app_build_<AppID>.vdf` / `depot_build_<DepotID>.vdf`.
4. Lance `builder/steamcmd.exe +login <compte> +run_app_build ..\scripts\app_build_<AppID>.vdf +quit`.
5. Dans Steamworks, *Installation → General* : exécutable = `<NomDuJeu>.exe` ; icône client = le `.ico` exporté à côté du dossier.
6. Publie la build sur la branche par défaut, puis prépare la page boutique.

Ne mets **pas** de `steam_appid.txt` dans le dossier exporté (il sert seulement en développement).
L'intégration Steamworks (succès, Rich Presence, overlay) arrive au jalon 8.

## Ajouter un écran ou un bouton dans le jeu

- Les écrans sont dans `Assets/_Project/Game/Screens/` (`MenuScreens.cs`, `SettingsScreen.cs`, `GameplayScreen.cs`).
  Chaque écran hérite de `GameScreen` et construit son interface en C# avec les helpers de `Pz`
  (`Pz.MakeIconButton(Icon.Gear, loc.T("ma.cle"), Pz.Primary, action)`, `Pz.MakeLabel`, `Pz.MakeBox`).
- **Ajouter un bouton au menu principal** : dans `MainMenuScreen.OnShow()`, ajoute
  `Add(Pz.MakeIconButton(Icon.Star, Flow.Loc.T("menu.monBouton"), Pz.Ghost, MaFonction));`
  puis ajoute la clé `"menu.monBouton"` dans `Resources/Localization/en.json` **et** `fr.json`
  (le test `Localization_FrenchHasEveryEnglishKey` vérifie qu'aucune traduction ne manque).
- **Ajouter un écran** : crée une classe `MonEcran : GameScreen` (voir `CreditsScreen` comme modèle), instancie-la dans
  `GameFlow.BuildScreens()`, ajoute une méthode `ShowMonEcran()` dans `GameFlow` qui appelle `_router.ShowPage(...)`
  (page) ou `_router.PushOverlay(...)` (par-dessus), et ajoute ses styles dans `Resources/UI/Game.uss`.
- Les couleurs et polices sont appliquées automatiquement par `Flow.Theme.Apply(Root)` grâce aux classes
  `pz-text`, `pz-surface`, `pz-primary`, `pz-ghost`, `pz-heading`…
- Pour la voir dans le Studio, ajoute l'écran à `StartScreen` (GameFlow.cs) et à `LivePreview.Screens`.

## Changer les sons et musiques par défaut

- **Pour un seul jeu** : dans le Game Pack, mets tes fichiers dans `audio/` et indique-les dans `game.json` :
  `"audio": { "musicMenu": "audio/menu.ogg", "musicGame": "audio/jeu.ogg", "sfx": { "snap": "audio/pop.wav" } }`
  (formats : ogg, wav, mp3). Les clés de sons sont : click, pick, drop, snap, star, victory, hint, undo, locked, swoosh.
- **Pour tous les jeux** : remplace les fichiers de `Assets/_Project/Resources/Audio/` (même nom), ou modifie la
  synthèse dans `Assets/Editor/BuildTools/DefaultAudioGenerator.cs` puis menu **Build > Generate Default Audio**
  (les musiques `calm_01..03` sont décrites par une suite d'accords, un tempo et une gamme).

## ⏳ À venir
- Ajouter un réglage dans le Studio (jalons 6–7)
- Ajouter un succès Steam, builder et uploader sur Steam (jalon 8)
