# Checklist de sortie d'un jeu

À suivre pour chaque jeu créé avec Puzzle Studio, du projet au bouton « Publier » de Steam.

## 1. Contenu (Studio)
- [ ] **Levels** : toutes les images importées, dans le bon ordre (glisser les points), recadrées si besoin, noms relus.
- [ ] Aucune erreur dans **Export → Validate** (images manquantes, titre vide, contraste…).
- [ ] **Gameplay** : courbe de difficulté testée du premier au dernier niveau (Play Test, F5).
- [ ] **Theme** : couleurs lisibles (le Studio l'indique), logo et polices dont tu as le droit d'usage.
- [ ] **Audio** : musiques et sons dont tu as le droit d'usage commercial ; volumes écoutés.
- [ ] **Texts** : crédits complets ; chaque langue proposée relue (compteur « traduits » à 100 %).

## 2. Test du jeu exporté
- [ ] **Export Game**, puis lancer `<NomDuJeu>.exe` depuis le dossier exporté (pas depuis le Studio).
- [ ] Jouer un niveau de chaque mode à la **souris**, au **clavier** et à la **manette** (curseur, A, B, X, Y, LB, Menu).
- [ ] Réglages : plein écran / fenêtré, résolutions, volumes, langue, taille de l'interface, police lisible.
- [ ] Finir le jeu une fois : écran de fin, succès débloqués (menu **Achievements**).
- [ ] Quitter et relancer : la progression et les réglages sont conservés.
- [ ] Si possible : un essai sur **Steam Deck** (ou en 1280 × 800 dans le Studio, bouton **Gamepad** activé).

## 3. Steamworks (voir aussi `README_STEAMWORKS.txt` dans le dossier `_Steamworks`)
- [ ] App ID et Depot ID saisis dans l'onglet **Steam** du Studio, puis nouvel export.
- [ ] Succès créés avec les mêmes **API Names**, icônes envoyées, puis **Publish**.
- [ ] Fichiers **Rich Presence** envoyés (un par langue).
- [ ] **Steam Cloud** configuré si coché (Auto-Cloud : chemin indiqué dans le README).
- [ ] Images de la page boutique et de la bibliothèque (dossier `store\`), 5 captures ou plus (`screenshots\`).
- [ ] Build envoyée avec `steampipe\upload.bat`, puis mise en ligne sur la branche **default**.
- [ ] Test final en installant le jeu **depuis Steam** (overlay Maj+Tab, succès, présence chez un ami).
- [ ] Questionnaire **Steam Deck** rempli (voir HOW_TO_CUSTOMIZE.md, section Manette).

## 4. Après la sortie
- [ ] Garder le dossier du projet `.puzzleproj` (sauvegarde ailleurs aussi) : c'est lui qu'on rouvre pour une mise à jour.
- [ ] Pour une mise à jour : changer la **Version** (onglet Project), exporter, relancer `upload.bat`.
