# Changelog

## [Mode Memory] Nouveau mode de jeu : les paires — 2026-10-05

### Ajouté
- **Mode Memory** : toutes les cases sont des cartes **face cachée**. Une carte retournée montre **le morceau de l'image
  de sa place** et son **symbole** ; deux cartes au même symbole restent visibles et **leurs deux morceaux de l'image
  sont faits**. Deux cartes différentes se retournent toutes seules après ~1 s (ou au clic suivant). Toute l'image
  découverte = victoire.
- **3 styles de symboles** : **chiffres** (1…50), **lettres** (A…Z) ou **couleur de contour** (12 couleurs bien
  distinctes, avec un liseré sombre pour rester visibles sur toute image). Option daltonien du joueur : les numéros
  s'ajoutent aux couleurs.
- Animation de **retournement** (la carte pivote), cartes retournées soulevées, pause d'un instant sur une paire trouvée
  puis les symboles disparaissent avec un éclat : seule l'image reste.
- **Dos des cartes** dessiné par le shader (losanges, cadre, motif central) dans la couleur principale du thème
  ou celle de l'image (*Colors from pictures*).
- Grille : suit la difficulté (taille N ≈ N × N cartes, max 10 × 10), réduite si le style n'a pas assez de symboles ;
  grille impaire → une **carte libre** au centre.
- Score : 1 coup = 1 paire de cartes retournée ; par ≈ 1,6 coup par paire. Indice : surligne une paire (ou la carte
  qui va avec celle déjà retournée) et y déplace le curseur à la manette. Jouable souris, clavier (Entrée) et manette (A).
- **Studio** : mode « Memory » dans les listes, section **Memory mode** (onglet Gameplay) pour choisir les symboles,
  choix par niveau (« Card symbols » dans l'onglet Levels).
- Exemples : un niveau Memory dans chaque SamplePack (chiffres dans Cozy Puzzles, couleurs dans Neon Nights,
  lettres dans Quiet Shapes).
- Tests : 23 tests EditMode (distribution des paires, carte libre, paire trouvée / ratée, 3e clic, victoire unique,
  indice, par, styles, grille, JSON) et 1 test PlayMode (partie complète à la manette virtuelle, cartes qui se
  retournent toutes seules). Total : 201 EditMode + 5 PlayMode.

### Technique
- `PuzzleModeBase.IsDone(piece)` (surchargeable) remplace le test « bien placée » pour `OnPieceCorrect` et la victoire.
- `ICardMode` (cartes face cachée), `CardSymbols` (libellés, couleurs, ajustement de la grille).
- Un `PuzzleMove` peut compter 0 coup (la première carte d'une paire).
- Actions de capture `flip` et `mismatch` ; `partial` trouve un quart des paires en mode Memory.

## [Jalon 10] Passe qualité + couleurs selon les images — 2026-10-05

### Amélioré
- **Couleurs selon les images** refaites :
  - extraction des couleurs dans **OKLab** (perceptuel), les nuances identiques sont fusionnées ;
  - **fond = couleur dominante**, **fin du dégradé = 2e couleur**, **halo / lueurs du fond animé = 3e couleur** ;
  - **boutons, boutons secondaires, étoiles, sélection = couleurs moins dominantes et plus vives** (l'orange reste
    orange, plus de boutons « marron ») ; image grise → boutons du thème ;
  - même clarté de fond quelle que soit la teinte ; texte lisible garanti (testé sur 600 palettes, 4 thèmes) ;
  - fonctionne sur **tous les types de fond**, y compris animé, motif, image et image floutée, en clair et en sombre.
- **Fond** : dégradé adouci aux extrémités, **3e couleur** (halo sur les dégradés, lueurs qui se déplacent sur le fond
  animé), **tramage** contre les bandes de couleur sur grand écran.
- « Generate palette from images » (Studio) remplit aussi les 3 couleurs du dégradé.
- Libellés des boutons : encre foncée si ni le blanc ni le texte du thème ne sont lisibles (boutons clairs en thème sombre).

### Corrigé
- Image de niveau manquante ou illisible : image de remplacement colorée (pièces toutes différentes, niveau jouable)
  au lieu de pièces grises identiques.
- En mode capture automatique (`-captureQuit`), un pack illisible ferme le jeu au lieu de laisser une fenêtre d'erreur.
- Style `:first-child` non supporté par UI Toolkit (avertissement à chaque lancement) remplacé.

### Vérifié
- Journaux du jeu et du Studio sans avertissement ni erreur sur une session complète (tous les écrans, tous les onglets).
- Pack « piège » : image absente, corrompue, minuscule (64 px), énorme (6000 × 4000), très large (3000 × 300) : aucun plantage.
- **Performances** (1920 × 1080, grille 12 × 12, fond animé, confettis) : 0,8 ms par image en moyenne, 0 ramasse-miettes
  sur 400 images (pas d'à-coups). Nouvelle action de mesure `perf`.
- Régénération des SamplePacks : images identiques (seuls les nouveaux champs apparaissent dans game.json).
- Tests : 178 EditMode + 4 PlayMode.

### Ajouté
- `RELEASE_CHECKLIST.md` : la liste des étapes pour sortir un jeu sur Steam.
- Le Studio s'appelle maintenant « Puzzle Studio 1.0 ».

## [Jalon 7] Studio v2 — 2026-10-05

### Ajouté
- **Annuler / Rétablir** (Ctrl+Z, Ctrl+Y ou Ctrl+Maj+Z, boutons Undo / Redo) sur tout le projet ; une frappe ou un
  curseur glissé compte pour une seule étape.
- **Sauvegarde automatique** toutes les 60 s dans `.autosave\` (jamais par-dessus le projet) ; après un plantage, le
  Studio propose de récupérer les changements (et on peut encore annuler la récupération).
- **Niveaux** : réordonner en glissant les points à gauche de chaque niveau ; **outil de recadrage** (déplacer le cadre,
  tirer un coin, ratios Libre / Original / 1:1 / 4:3 / 16:9 / 3:4, Reset), visible tout de suite dans l'aperçu.
- **Onglet Audio** : musique des menus et des niveaux (3 intégrées, aucune, ou ton fichier OGG/WAV/MP3), écoute dans le
  Studio, volumes, fondu enchaîné, variation de hauteur, et chacun des 10 sons (écouter, remplacer, revenir à l'intégré).
- **Onglet Texts** : crédits (ajouter, ordonner, supprimer) ; **tous les textes du jeu** modifiables dans chaque langue
  avec recherche ; **ajout de langues** (allemand, espagnol, italien… 13 au choix) avec compteur de traduction. Stocké dans
  `game.json` (`texts`) ; une langue ajoutée apparaît dans les réglages du jeu.
- **Theme** : **logo du jeu** (remplace le titre) et **logo du studio** (splash) ; **polices perso** `.ttf` / `.otf`
  pour les titres et le texte, chargées par le jeu depuis le pack (police intégrée si le fichier est illisible).
- Fichiers importés nommés selon leur contenu (`background_3fa2c1d0.png`) : jamais écrasés, donc toujours annulables ;
  les fichiers devenus inutiles partent dans `.trash\` à l'ouverture suivante.
- Tests : +13 EditMode (historique, instantanés, sauvegarde auto, fichiers, ordre des niveaux, recadrage, textes et
  langues, polices depuis un fichier).

### Modifié
- Textes : un changement en anglais ne remplace plus un texte français intégré (ordre des couches corrigé).
- Supprimer un niveau ne supprime plus son image tout de suite (elle part dans `.trash\` à la réouverture).
- La barre « Coming next » du Studio a disparu : tout ce qui était prévu est là.

## [Jalon 9] Manette, Steam Deck et accessibilité — 2026-10-04

### Ajouté
- **Jeu complet à la manette et au clavier** : curseur sur le plateau (croix / stick / flèches / ZQSD), A / Entrée pour
  prendre-poser-tourner, RB / Q pour tourner dans l'autre sens, X / H indice, Y / Espace aperçu, LB / Ctrl+Z annuler,
  View / R recommencer, B / Échap annuler ou pause, Menu pause. Répétition quand on maintient une direction.
  Au taquin, la croix pousse les tuiles dans le trou. Un indice déplace le curseur sur la pièce à bouger.
- **Menus à la manette** : EventSystem + Input System (croix, stick, A, B), contour autour de l'élément choisi,
  barre « A Choisir · B Retour », les listes défilent avec le focus. Dans les réglages, Haut / Bas passent d'une ligne
  à l'autre (y compris depuis les curseurs de volume) et sautent les lignes grisées.
- **Repères de boutons** : A / X / Y / LB / View sur les boutons du jeu avec une manette, H / R / Espace… au clavier ;
  l'affichage suit l'appareil utilisé en dernier. Aide du niveau adaptée à la manette ou au clavier.
- **Vibrations** (pièce bien placée, victoire, coup impossible), désactivables.
- **Écran Controls** (Settings) : toutes les commandes souris / clavier / manette.
- **Accessibilité** : masquer le chrono, police simple et lisible, vibrations (en plus de la taille de l'interface,
  du mode daltonien et de « réduire les animations »).
- **Steam Deck** : interface à 115 % au premier lancement sur un Deck ; vérifié en 1280 × 800 ; section Steam Deck dans
  le README Steamworks.
- **Confirmation** avant de quitter le jeu ; les confirmations sélectionnent « Annuler » par défaut.
- Studio : bouton **Gamepad** dans la barre d'aperçu (voir le jeu comme un joueur à la manette).
- Notifications de succès groupées (« 5 SUCCÈS DÉBLOQUÉS » au lieu de 5 bandeaux).
- Tests : +5 EditMode (curseur, répétition, Steam Deck, anciens réglages) et +2 PlayMode joués avec une
  **manette virtuelle** (un niveau d'échange résolu entièrement à la manette ; taquin, annuler, indice, pause).
- Outil de capture : actions `pad:a`, `pad:right`… (manette virtuelle), `controls`.

### Corrigé
- Le A / Entrée qui lance un niveau (Jouer, Niveau suivant, Reprendre) n'agit plus aussi sur le plateau.

## [Jalon 8] Steam — 2026-10-04

### Ajouté
- **Steamworks.NET 2025.164.1** (paquet Git, MIT). Le jeu s'initialise avec Steam quand un App ID est réglé :
  succès, **Rich Presence** (« Résout le puzzle 3 sur 12 », EN/FR), **pause quand l'overlay s'ouvre**,
  langue de Steam pour un nouveau joueur, **relance via Steam** si l'exe est lancé hors Steam
  (jamais pour Play Test). Sans App ID ou sans Steam : tout marche pareil (`NullSteamService`).
- **Succès** : 10 succès générés selon le nombre de niveaux (premier puzzle, quart, moitié, trois quarts, tous,
  coups parfaits, sans indice, sans aperçu, moins d'une minute, 3 étoiles partout) ou liste personnalisée.
  Calculés depuis la sauvegarde (rien n'est perdu hors ligne, renvoyés à Steam au lancement suivant).
  Sans overlay Steam, un **bandeau « Achievement unlocked »** s'affiche ; nouvel écran **Achievements** dans le menu
  (progression « 2 / 3 », succès cachés). Noms traduits EN/FR tant qu'ils ne sont pas modifiés.
- Studio : nouvel onglet **Steam** — App ID, Depot ID, relance via Steam, Rich Presence, Steam Cloud
  (avec les réglages Auto-Cloud exacts), succès *Automatic / Custom / Off* avec éditeur (API name, nom, description,
  règle, valeur, caché), **images de la boutique** générées (10 formats Steamworks : capsules, bibliothèque, logo
  transparent, icône communauté) et **5 captures d'écran 1920 × 1080** prises par le jeu lui-même.
- Export : dossier **`<Nom>_Steamworks`** avec icônes de succès 256 × 256 (normale + grisée), liste des succès,
  fichiers Rich Presence par langue, images boutique, captures, script **SteamPipe** (`app_build_<AppID>.vdf` +
  `upload.bat`) et `README_STEAMWORKS.txt` (quoi saisir où). Option `steam_appid.txt` pour tester hors du client Steam.
- Jeu : arguments `-tempSave`, `-demoProgress`, `-mute`, `-noSteam`, actions de capture combinées (`play2+partial`).
- Validateur : App ID / Depot ID, API names des succès (format, doublons), valeurs impossibles.
- Icônes vectorielles Trophée, Étoile, Horloge.
- Tests : +23 EditMode (règles des succès, génération, sauvegarde, service + faux Steam, fichiers VDF, guide,
  validateur, tailles des images boutique).

### Modifié
- L'App ID se règle maintenant dans l'onglet Steam (l'onglet Project y renvoie).
- « Best: 1 move » au singulier.

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
