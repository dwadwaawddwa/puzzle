# Puzzle Studio

Usine à jeux de puzzle d'images pour Steam, construite avec **Unity 6 (6000.0.32f1)**.

Un même projet Unity produit deux programmes :

| Programme | Rôle | État |
|---|---|---|
| **Player Template** (`Build/Template/Game.exe`) | Le jeu générique. Il ne contient aucun contenu et se construit tout seul à partir d'un **Game Pack** (JSON + images). | ✅ 1.0 : 4 modes, menus, succès, Steam, manette / Steam Deck, accessibilité |
| **PuzzleStudio.exe** (`Build/PuzzleStudio/`) | L'outil de création : projets, niveaux, thème, audio, textes, Steam, aperçu en direct jouable, Play Test, export du jeu final | ✅ 1.0 |

Les 10 jalons du plan sont terminés. Avant de publier un jeu : [RELEASE_CHECKLIST.md](RELEASE_CHECKLIST.md).

L'architecture complète est décrite dans [ARCHITECTURE.md](ARCHITECTURE.md), l'historique dans [CHANGELOG.md](CHANGELOG.md).

---

## Installation (après un clone)

Le dépôt ne contient que les sources : les programmes (`Build/`) se compilent sur ta machine.

**Prérequis** (Windows 10/11) :
1. [Unity Hub](https://unity.com/download), connecté à un compte Unity (licence Personal gratuite).
2. L'éditeur **Unity 6000.0.32f1** : dans Unity Hub > *Installs* > *Install Editor* > *Archive*,
   ou ouvre directement `unityhub://6000.0.32f1/b2e806cf271c`. Aucun module en plus n'est nécessaire
   (le support Windows Mono est inclus).
3. Git, ou GitHub Desktop.

**Installer** :
```
git clone https://github.com/dwadwaawddwa/puzzle.git
cd puzzle
install.bat
```
`install.bat` trouve Unity tout seul et compile le Player Template, puis PuzzleStudio.exe dans `Build\`.
La première fois, Unity importe tout le projet (5 à 15 min). Ensuite, il propose un raccourci sur le Bureau.
- Unity Hub doit être **ouvert** pendant la compilation (licence), et le projet **fermé** dans l'éditeur Unity.
- Si Unity est installé ailleurs : `set UNITY_EXE=D:\...\6000.0.32f1\Editor\Unity.exe`, puis `install.bat`.
- En cas d'échec, les journaux sont dans `Logs\` (`build-template.log`, `build-studio.log`).

Pour recompiler après une modification : `build.bat` (voir plus bas).

---

## Démarrage rapide

### Créer un jeu avec le Studio
1. Double-clique **`Lancer PuzzleStudio.bat`** (ou `Build\PuzzleStudio\PuzzleStudio.exe`).
2. **New project** (ou « Start from a sample »). Les projets vont dans `Documents\PuzzleStudio Projects\`.
3. Onglet **Levels** : *+ Add images…* / *+ Add folder…*, ou **glisse-dépose** des images ou un dossier depuis l'Explorateur.
4. Onglets **Project** (titre…), **Levels** (ordre par glisser, recadrage), **Gameplay** (difficulté…), **Theme** (presets, couleurs — fixes ou selon chaque image —, fond, décorations, logos, polices dont les tiennes, pièces), **Layout** (déplacer/redimensionner les éléments à la souris),
   **Audio** (musiques et sons, écoute, import), **Texts** (crédits, tous les textes, traductions), **Steam** (App ID, succès, images de la boutique, captures d'écran) :
   l'aperçu au centre est le vrai jeu, jouable, mis à jour en direct.
5. **Play Test** (F5) : lance le jeu dans sa propre fenêtre.
6. **Export Game** : crée `Documents\PuzzleStudio Exports\<NomDuJeu>\<NomDuJeu>.exe` + `<NomDuJeu>_Data`
   (icône incluse), plus un `.ico`, un `.zip` optionnel et le dossier `<NomDuJeu>_Steamworks` (tout pour Steamworks).
   C'est ce dossier qu'on envoie sur Steam : voir [HOW_TO_CUSTOMIZE.md](HOW_TO_CUSTOMIZE.md#publier-sur-steam).

Raccourcis : Ctrl+S enregistrer, Ctrl+Z annuler, Ctrl+Y rétablir, Ctrl+N nouveau, Ctrl+O ouvrir, F5 Play Test.
Sauvegarde automatique toutes les minutes dans `.autosave\` du projet (proposée à la réouverture après un plantage).

### Jouer à un pack d'exemple (sans Unity)
```
play_sample.bat                 (CozyPastel par défaut)
play_sample.bat DarkNeon
play_sample.bat MinimalWhite
```

### Dans l'éditeur Unity
1. Ouvre le projet dans Unity Hub.
2. Ouvre `Assets/_Project/Scenes/Game.unity` et clique sur **Play**.
   En éditeur, le jeu charge automatiquement `SamplePacks/CozyPastel`.

### Commandes du jeu
| Action | Souris / tactile | Clavier | Manette (Steam Deck) |
|---|---|---|---|
| Déplacer le curseur | — | **flèches** / ZQSD (WASD) | **croix** / stick gauche |
| Prendre / poser (Swap, Strips), tourner (Rotate) | clic, ou glisser | **Entrée** | **A** |
| Tourner dans l'autre sens (Rotate) | clic droit | **Q** | **RB** |
| Taquin (Sliding) | clic sur une tuile alignée avec le trou | **flèches** | **croix** / stick |
| Aperçu de l'image | maintenir **Preview** | maintenir **Espace** | maintenir **Y** |
| Indice | **Hint** | **H** | **X** |
| Annuler | **Undo** | **Ctrl+Z** ou **Retour arrière** | **LB** |
| Recommencer | **Restart** | **R** | **View** |
| Désélectionner / Pause | — | **Échap** | **B** / **Menu** |

Les menus se pilotent entièrement à la manette ou au clavier (contour autour de l'élément choisi,
barre « A Choisir · B Retour »). Les boutons affichent leur touche (A, X, LB… ou H, R…) selon l'appareil utilisé
en dernier ; un mouvement de souris remet l'affichage souris. L'écran **Settings → Controls** liste tout.

---

## Builds en ligne de commande

Ferme l'éditeur Unity avant de lancer un build (le projet est verrouillé sinon).

```
build.bat samples    → régénère les SamplePacks (images procédurales)
build.bat test       → tests EditMode + PlayMode (résultats dans Logs\*.xml)
build.bat template   → Build\Template\Game.exe
build.bat studio     → Build\PuzzleStudio\PuzzleStudio.exe (+ copie du Template et des SamplePacks)
build.bat setup      → régénère scènes, PanelSettings, matériau
build.bat all        → samples + test + template + studio
```

Équivalents dans l'éditeur : menu **Build > Player Template**, **Build > Studio**, **Build > All**, **Build > Generate Sample Packs**, **Build > Setup > Regenerate Project Assets**.

Arguments de dev du Studio : `PuzzleStudio.exe -openProject "<dossier .puzzleproj>" -capture a.png;b.png -captureSteps levels;theme -captureQuit`
(étapes : un onglet, `picker`, `victory`, `selectN`, `preset:DarkNeon`, `storeart`, `screenshots`, `scroll:800`, `pad`, `crop`, `close`,
`cropset:x,y,w,h`, `move:1:3`, `undo`, `redo`, `settext:fr:menu.play:Jouer`, `importfont:<chemin>`, `exportrun`).

### Arguments du jeu
| Argument | Effet |
|---|---|
| `-pack "C:\chemin\GamePack"` | charge un Game Pack depuis le disque (sans export) |
| `-level 3` | démarre directement au niveau 3 |
| `-screen menu` | démarre sur un écran (`splash`, `menu`, `levels`, `settings`, `credits`, `end`) |
| `-capture a.png;b.png -debugAction none;solve -captureQuit` | outil de dev : captures d'écran automatiques (actions : `none`, `select`, `hint`, `partial`, `solve`, `menu`, `levels`, `settings`, `credits`, `end`, `pause`, `pausesettings`, `achievements`, `play2`…, combinables : `play2+partial`) |
| `-tempSave` / `-demoProgress` | sauvegarde jetable (et remplie à ~40 %) : utilisé pour les captures de la boutique |
| `-mute` / `-noSteam` | sans son / sans initialiser Steam |
| action `perf` | mesure 400 images sans limite de FPS : temps moyen / 95e centile / pire, ramasse-miettes (`[Perf]` dans Player.log) |
| actions `pad:a`, `pad:right`, `pad:start`… | appuie sur un bouton d'une manette virtuelle (tests de la manette sans manette) |

Ordre de recherche du pack : `-pack`, puis `<Jeu>_Data/StreamingAssets/GamePack/`, puis (éditeur seulement) `SamplePacks/CozyPastel`.

---

## Arborescence

```
Assets/_Project/Core     logique pure (données, pack, modes, sauvegarde, localisation, succès, fichiers Steamworks) — PuzzleCore.asmdef
Assets/_Project/Game     runtime du jeu (plateau, entrées, écrans UI Toolkit, Steam via Steamworks.NET) — PuzzleGame.asmdef
Assets/_Project/Studio   outil de création — PuzzleStudio.asmdef : StudioApp (fenêtre, projets), Panels (onglets),
                         Preview (aperçu live), Export (exporteur, icône, images Steam, captures), Widgets, Themes (8 presets + aléatoire)
Assets/_Project/Shaders  Piece.shader (coins arrondis SDF, bordure, surbrillance)
Assets/_Project/Resources  polices OFL, en.json, USS, PanelSettings, matériau
Assets/Editor/BuildTools   setup du projet, génération des SamplePacks, builds
Assets/Tests             EditMode (178 tests) + PlayMode (4 tests : parcours complet + niveaux joués à la manette virtuelle)
SamplePacks/             CozyPastel, DarkNeon, MinimalWhite
_Legacy/                 ancien générateur (non compilé, conservé pour référence)
```

Un projet Studio = un dossier `<Nom>.puzzleproj\` : `project.json` (réglages d'export) + `pack\` (le Game Pack : `game.json` + `levels\`,
`theme\`, `audio\`) + `steam\` (images boutique, captures) + `.autosave\` / `.trash\` (gérés par le Studio).
Réglages du Studio (projets récents) : `%USERPROFILE%\AppData\LocalLow\PuzzleStudio\PuzzleStudio\studio.json`.

Sauvegardes du joueur : `%USERPROFILE%\AppData\LocalLow\PuzzleStudio\PuzzleGame\<Titre du jeu>\`
(`progress.json` + `.bak`, `settings.json`). Log du jeu : `...\LocalLow\PuzzleStudio\PuzzleGame\Player.log`.

---

## Manual steps

Étapes que le code ne peut pas faire à ta place :

1. **Licence Unity** : Unity Hub (version Microsoft Store) doit être **ouvert et connecté** à ton compte
   pour que les builds en ligne de commande obtiennent la licence. Si un build échoue avec
   « No valid Unity Editor license found », ouvre Unity Hub et reconnecte-toi.
2. **IL2CPP (optionnel)** : pour builder en IL2CPP au lieu de Mono, ajoute le module
   *Windows Build Support (IL2CPP)* dans Unity Hub (et la charge de travail « Développement Desktop en C++ »
   de Visual Studio 2022). Le menu Build détecte le module tout seul.

## Crédits des ressources
- Polices : Nunito, Inter, Fredoka, Playfair Display — SIL Open Font License (`Assets/_Project/Fonts/OFL-*.txt`).
- Steam : [Steamworks.NET](https://github.com/rlabrecque/Steamworks.NET) 2025.164.1 (licence MIT), paquet Git
  téléchargé par Unity (`Packages/manifest.json`).
- Images des SamplePacks : générées par code (`SamplePackGenerator.cs`), libres de droits.
