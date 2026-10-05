# Changelog

## [Dépôt] Git + installation — 2026-10-04

### Ajouté
- Projet publié sur GitHub (sources seulement : `Library/`, `Build/`, `Logs/`, `UserSettings/` sont ignorés).
- `install.bat` : trouve Unity 6000.0.32f1, vérifie que l'éditeur et le Studio sont fermés, compile le Player Template
  et PuzzleStudio.exe, puis propose un raccourci sur le Bureau. S'il ne trouve pas Unity, il ouvre Unity Hub sur la
  bonne version.
- `tools/find_unity.bat` : recherche de l'éditeur (variable `UNITY_EXE`, dossiers par défaut de Unity Hub, dossier perso
  du Hub), utilisée par `build.bat` (plus de chemin en dur).
- `.gitattributes` : fins de ligne normalisées (`.bat` en CRLF), fichiers binaires marqués.
- Les lanceurs `.bat` disent quoi faire si le projet n'est pas encore compilé.

### Corrigé
- Les compilations ne réécrivent plus les scènes à chaque fois (seules les scènes manquantes sont créées),
  donc plus de modifications parasites dans Git.

## [Jalon 5] Les autres modes de puzzle — 2026-10-04

### Ajouté
- **Strips (bandes)** : l'image est coupée en N bandes verticales ou horizontales (réglable, 2 à 24, courbe de
  difficulté). On glisse une bande à sa place, **les autres se décalent en direct** pendant le glisser (réordonnancement
  de liste) ; clic-clic marche aussi. Par = nombre minimal d'insertions (N − plus longue sous-suite croissante).
- **Sliding (taquin)** : une case vide (la tuile du coin bas-droit, qui réapparaît à la victoire) ; cliquer une tuile
  alignée avec le trou fait glisser toute la rangée ; **flèches du clavier** ; un glisser vers le trou = un clic.
  Mélange par marche aléatoire de coups légaux → **toujours soluble** (vérifié par la parité et par une recherche
  exhaustive sur petite grille). Grille plus petite que les autres modes (×0,75, de 3 à 6 par côté).
  Indice = meilleur coup selon l'heuristique Manhattan + conflits linéaires ; par ≈ 1,7 × borne minimale.
- **Rotate (rotation)** : tuiles à leur place mais tournées de 90/180/270° ; **clic gauche = sens horaire,
  clic droit = sens inverse** ; cases carrées (recadrage automatique) ; tuiles verrouillées une fois droites ;
  la tuile tourne toujours dans le sens du clic. Par = nombre minimal de clics.
- Chaque mode : mélange avec seed jamais résolu, indice, annuler, nombre de coups de référence pour les étoiles.
- **Bulle d'aide** « comment jouer » propre à chaque mode au début d'un niveau (EN/FR), qui s'efface seule.
- Une rangée de taquin compte autant de coups que de tuiles déplacées ; un seul son « snap » par image même si
  plusieurs bandes se remettent en place d'un coup.
- Studio : description des 4 modes, réglages du mode Strips (direction, nombre de bandes début/fin) ; l'aperçu
  « Victory » marche avec tous les modes (`ForceSolve`).
- SamplePacks : CozyPastel et DarkNeon utilisent maintenant les 4 modes.
- Tests : +18 EditMode (insertion et aperçu des bandes, par et résolution par indices, taquin soluble + BFS,
  rangée/flèches/annuler, rotation : par, verrouillage, résolution en « par » clics, grilles par mode).

## [Jalon 4] Finitions visuelles — 2026-10-04

### Ajouté
- **Fonds procéduraux** (`Background.shader` + `BackgroundFill`) : couleur, dégradé (2 couleurs + angle),
  **dégradé animé** (dérive lente + halos lumineux), **motifs** points / rayures / grille / vagues (opacité et taille
  réglables), **vignette**. Fonctionne sous l'image de fond et avec les couleurs par niveau.
- **Ombres des pièces** : ombre douce sous chaque pièce (`_Softness` dans `Piece.shader`), qui s'agrandit et s'éloigne
  quand la pièce est soulevée ; disparaît avec les bordures à la victoire.
- **Particules** (`ParticleFactory`, textures générées par code, aucun asset) : étincelles ou petites étoiles quand une
  pièce se pose au bon endroit ; à la victoire **confettis** (pluie + deux canons, papier qui « tourne »),
  **étoiles** (deux salves) ou **bulles**. Quantité réglable ; très réduites si « Réduire les animations ».
- **Victoire** : après la disparition des bordures, petit **zoom** de l'image complète, puis particules ; le voile
  derrière le panneau est plus léger pour laisser voir la fête.
- **Studio → Theme** : types de fond Color / Gradient / Animated gradient / Picture / Level picture (blurred),
  2e couleur, angle, vitesse, motif, opacité/taille du motif, vignette ; ombre des pièces (on/off + force) ;
  section **Effects** (victoire, pièce bien placée, quantité).
- Test PlayMode : les particules de victoire jouent bien.
- Les captures automatiques du Studio n'écrivent plus dans la liste des projets récents.

## [Studio] Couleurs selon l'image, fonds, décorations, onglet Layout — 2026-10-04

### Ajouté
- **Couleurs selon l'image** (Theme → Colors → *From pictures*) : *Off*, *Background only* (le fond prend la couleur
  dominante de l'image du niveau) ou *Whole theme* (fond, panneaux, boutons, surbrillance, étoiles). Le caractère
  clair/sombre du thème est gardé et le texte reste lisible (WCAG AA, testé sur des centaines de palettes).
  Extraction des couleurs : `PaletteExtractor` (k-means déterministe) + `ThemeDerivation`.
- **Generate palette from images** : crée les couleurs de base du thème à partir de toutes les images du jeu.
- **Fond** : couleur, image (au choix) ou **image du niveau floutée** (dans les menus : celle du niveau « Continue »),
  avec opacité réglable au-dessus de la couleur de fond (`BackgroundView`).
- **Décorations gauche / droite** : deux images optionnelles et indépendantes (jeu et menu principal) ; quand il y en a,
  la zone du puzzle se resserre automatiquement pour leur laisser la place.
- **Onglet Layout** : déplacer à la souris les blocs de l'écran de jeu (titre du niveau, coups/temps/pause, boutons,
  zone du puzzle, décorations) et du menu principal (titre, boutons, cartes-photos, décorations) ; poignée de
  redimensionnement ; **grille magnétique** (taille réglable, affichable), **aimantation sur les autres blocs** et sur
  le centre/bords de l'écran avec lignes guides roses ; boutons d'alignement (gauche/centre/droite, haut/milieu/bas),
  réglages précis X/Y/taille, masquer un bloc, **Reset** d'un bloc ou de tout l'écran. Les positions sont
  proportionnelles à l'écran (fonctionnent en 16:9, 16:10, 21:9, 4:3). Données : section `layout` de `game.json`.
- Le jeu applique la mise en page (`LayoutService`) ; la zone du puzzle suit `layout.boardArea`.
- Tests : +14 EditMode (palette, lisibilité des couleurs dérivées, caractère clair/sombre, JSON du layout,
  application du layout, zone du puzzle).

## [Jalon 3] Le jeu complet — 2026-10-04

### Ajouté
- **Tous les écrans**, construits en code (UI Toolkit) avec transitions : Splash (développeur puis titre, passable),
  **Menu principal** (titre animé, cartes-photos des niveaux, Play/Continue, Levels, Settings, Credits, Quit),
  **Sélection des niveaux** (grille de vignettes floutées tant que non terminées, nettes ensuite, étoiles, meilleur temps,
  cadenas, compteur « x / n completed » et étoiles totales, secousse + son sur un niveau verrouillé),
  **Pause** (Resume, Restart, Settings, Levels, Main menu), **Options**, **Crédits**, **Écran de fin** (tous les niveaux finis),
  boîte de **confirmation**.
- **Progression** : règles de déblocage Sequential / AllUnlocked / ByStars, « Continue » reprend le bon niveau,
  « Niveau suivant » / « Terminer » après la victoire (`ProgressRules`, testé).
- **Victoire** : étoiles qui apparaissent une par une (pop + son montant), meilleur score, « New record! ».
- **HUD** avec icônes vectorielles (pause, recommencer, annuler, indice, aperçu…) ; pause auto quand la fenêtre
  perd le focus ; Échap / bouton B = retour ou pause, Start = pause.
- **Options** : volumes (général, musique, effets), mode d'affichage, résolution, V-Sync, limite d'images/s,
  langue, taille de l'interface (90–130 %), surbrillance daltonien, réduire les animations, réinitialiser la progression.
- **Audio** : `AudioService` (musique en fondu enchaîné, 10 voix d'effets, variation de hauteur, fichiers du pack
  ogg/wav/mp3). Sons et **3 musiques d'ambiance générés par synthèse** (libres de droits, boucles parfaites) :
  click, pick, drop, snap (hauteur qui monte en série), star, victory, hint, undo, locked, swoosh.
- **Localisation** : tous les textes via des clés ; anglais + **français** intégrés ; langues supplémentaires via
  `locale/xx.json` dans le pack.
- **Studio** : l'aperçu peut afficher chaque écran (Gameplay, Victory, Main menu, Level select, Settings, Credits,
  End screen, Splash) + bouton Sound on/off.
- Outil de capture : `-screen menu|levels|settings|credits|end`, actions `menu`, `levels`, `pause`, `playN`…
- Tests : 94 EditMode (+ règles de progression, complétude du français) et 2 PlayMode (niveau résolu + sauvegarde ;
  parcours menu → niveaux → jeu → pause → victoire → niveau suivant → menu).

## [Studio v1] Générateur PuzzleStudio.exe (jalon 6 avancé à ta demande) — 2026-10-04

### Ajouté
- **PuzzleStudio.exe** (UI Toolkit, thème sombre, interface en anglais) : barre du haut (New / Open / Recent / Save /
  Play Test / Export Game), onglets **Project, Levels, Gameplay, Theme, Export**, aperçu au centre, inspecteur à droite,
  barre d'état. Écran d'accueil avec projets récents et démarrage depuis un SamplePack.
- **Projets** `<Nom>.puzzleproj/` (project.json + pack/), création depuis un pack existant, projets récents,
  confirmation avant de quitter/changer de projet sans sauvegarder, raccourcis Ctrl+S / Ctrl+N / Ctrl+O / F5.
- **Levels** : import d'images ou d'un dossier (boîtes de dialogue Windows natives), glisser-déposer depuis
  l'Explorateur, tri naturel, vignettes, renommer, monter/descendre, dupliquer, supprimer, mode et grille par niveau,
  avertissements du validateur par niveau.
- **Theme** : 8 presets (Cozy Pastel, Dark Neon, Minimal White, Retro Wood, Ocean Calm, Forest, Candy Pop, Night Sky),
  Randomize theme (couleurs harmonieuses, contraste WCAG garanti), sélecteur de couleur HSV + hex + palette,
  style des pièces, polices, forme des boutons, style des panneaux, vitesse d'animation ; lisibilité vérifiée en direct.
- **Gameplay** : mode, courbe de difficulté, grilles, verrouillage, aides (timer, coups, aperçu, indices, annuler), étoiles.
- **Aperçu en direct jouable** : le vrai runtime du jeu rendu dans le Studio (RenderTexture + UI du jeu hébergée),
  choix du niveau et de la résolution (16:9, Steam Deck 16:10, 21:9, 4:3), Reshuffle, Show victory.
- **Play Test** : lance le Player Template sur le pack du projet dans sa propre fenêtre.
- **Export Game** : validation, copie du Template, renommage exe/_Data, icône multi-tailles injectée dans l'exe
  (API Win32, sans rcedit) + fichier .ico, images optimisées (max 2048 px), zip optionnel, ouverture du dossier.
- Runtime : `GameViewport` (le jeu peut être hébergé hors plein écran), `GameRoot.HostConfig`.
- Build : `Build > Studio`, `Build > All`, `build.bat studio`, `Lancer PuzzleStudio.bat` ; le Studio embarque
  `Template/` et `Samples/`.
- Tests : +12 EditMode (projets, import, suppression, noms, presets lisibles, thèmes aléatoires lisibles, ICO,
  injection d'icône dans un vrai Game.exe).

### Pas encore (jalons suivants)
- Menu principal / sélection des niveaux / options / sons dans le jeu (jalon 3), fonds animés et particules (4),
  modes Strips/Sliding/Rotate (5), onglets Audio/Texts/Steam, recadrage, undo/redo, police perso (7), Steamworks (8).

## [Jalon 2] Core + mode SwapTiles — 2026-10-04

### Ajouté
- **Nouveau projet « Puzzle Studio »** dans le même projet Unity ; l'ancien générateur est archivé dans `_Legacy/` (non compilé).
- Assembly definitions : `PuzzleCore` (logique, sans UI), `PuzzleGame` (runtime), `PuzzleStudio.Editor`, tests EditMode/PlayMode.
- Packages : Newtonsoft JSON 3.2.1, Test Framework 1.4.5, module ScreenCapture.
- **Format Game Pack v1** complet (`game.json`) : jeu, gameplay, thème, audio, Steam, niveaux. Valeurs par défaut partout,
  `null` = valeur globale.
  - `GamePackLoader` / `GamePackWriter` (écriture atomique), `PackVersionMigrator` (v0 → v1),
    `PackValidator` (erreurs/avertissements lisibles : titre, images manquantes/petites/ratio extrême/doublons,
    ids dupliqués, modes inconnus, couleurs invalides, contraste WCAG, fichiers manquants).
- **Logique de puzzle** : `IPuzzleMode` + `PuzzleModeBase` + registre par attribut `[PuzzleMode]`
  (ajouter un mode = une classe), `ShuffleService` (seed reproductible, jamais résolu, % minimum mal placé),
  `SolvabilityChecker` (parité du taquin, prêt pour le jalon 5), `GridResolver` (courbe Fixed/Progressive/Custom,
  grilles adaptées au ratio de l'image, surcharges par niveau), `StarCalculator`.
- **Mode SwapTiles** : clic-clic ou glisser-déposer, tuiles bien placées verrouillées (option), par = nombre minimal
  d'échanges, indice, annulation.
- **Sauvegarde** : `JsonFileStore` (fichier temporaire + `.bak` + récupération si corruption), progression et réglages
  par jeu.
- **Localisation** : `LocalizationService` par couches (anglais intégré → pack), `en.json`.
- **Runtime jouable** : `GameRoot` construit caméra + UI + plateau par code ; `BoardView`/`PieceView` (1 quad par pièce,
  UV sur une seule texture, `Piece.shader` : coins arrondis SDF, bordure, contour de sélection), animations avec easing,
  survol, soulèvement au glisser, « pop » + lueur quand une pièce est bien placée, aperçu maintenu, indice pulsé,
  victoire (les bordures fondent → image complète) + panneau étoiles/coups/temps/record.
- **UI Toolkit** : HUD + panneau de victoire construits en C#, thème appliqué au runtime (`ThemeService` : couleurs,
  polices, forme des boutons, style des panneaux) ; 4 familles de polices OFL intégrées.
- Titre de la fenêtre = titre du jeu (Win32), pause du chrono quand la fenêtre perd le focus.
- **3 SamplePacks** générés par code (CozyPastel 6 niveaux, DarkNeon 4, MinimalWhite 4), images procédurales.
- Outils de build : menu `Build > …`, `build.bat`, `play_sample.bat`, argument `-pack`, outil de capture `-capture`.
- Tests : 76 EditMode (mélange, solvabilité, SwapTiles, étoiles, JSON, migration, validateur, images, grille, couleurs,
  sauvegarde, localisation) + 1 PlayMode (charger un pack → finir un niveau → progression sauvegardée).
