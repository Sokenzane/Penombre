# Contribuer à Pénombre

Merci de ton intérêt ! Pénombre est un petit projet : deux fichiers sources, aucune dépendance. Rester aussi simple fait partie du but.

## Sommaire

- [Signaler un bug ou proposer une idée](#signaler-un-bug-ou-proposer-une-idée)
- [Compiler](#compiler)
- [Self-test](#self-test)
- [Règles de code](#règles-de-code)
- [Règles pour tout ce qui touche au jeu](#règles-pour-tout-ce-qui-touche-au-jeu)
- [Ajouter une langue](#ajouter-une-langue)
- [Pull requests](#pull-requests)
- [Publier une version (mainteneur)](#publier-une-version-mainteneur)

## Signaler un bug ou proposer une idée

Ouvre un [ticket](https://github.com/Sokenzane/Penombre/issues/new/choose). Pour un bug, colle le résultat de **Copier le diagnostic** (menu de la zone de notification) et, si utile, la fin du journal (**Ouvrir le journal**). Les deux contiennent les titres de tes fenêtres, donc tes noms de personnages : remplace-les par `XXX` si tu préfères.

## Compiler

Double-clique sur `build.bat` (ou lance `build.bat ci` pour éviter la pause finale). Il compile `src/Penombre.cs` et `src/Interface.cs` en `dist/Penombre.exe` (64 bits) avec le compilateur C# fourni avec .NET Framework 4 : rien à installer.

`--lang=fr|en` force la langue, `--data=<dossier>` range les réglages et le journal dans un autre dossier.

## Self-test

Avec Dofus 3 ouvert (un personnage connecté, ou deux pour le multi-compte, en fenêtré ou plein écran fenêtré), double-clique sur `tools\selftest.bat`. Il arrête Pénombre, compile `dist\Penombre-test.exe` (l'appli plus [`src/SelfTest.cs`](src/SelfTest.cs)), le lance sur un dossier de réglages à part (`dist\selftest-data`, tes réglages ne sont pas touchés), puis recompile et relance `dist\Penombre.exe`.

Le test pilote la vraie appli par-dessus le jeu et mesure l'assombrissement à l'écran : rendus, fondu, transitions d'ambiance, réglages par compte, zones claires, vignettage, grain, mode automatique, horaires du soleil, enregistrement des raccourcis, export / import, mode portable, lancement au démarrage (remis comme avant), fenêtre de réglages, multi-compte, performances. Mets Dofus au premier plan quand c'est demandé, puis suis les lignes `MANUAL` de `dist\selftest-data\selftest-progress.txt` (raccourcis, coup d'œil, une fenêtre par-dessus Dofus, seulement au premier plan). Ne touche pas au jeu pendant le reste du test : un compte inactif assombrit lui-même son écran et fausse les mesures.

Résultats : `dist\selftest-data\selftest-report.txt` (avec les captures `snap-*.png` et `settings-tab*.png`). Toutes les lignes doivent être `PASS`.

## Règles de code

- **C# 5 uniquement.** Le compilateur intégré à Windows (`csc.exe` v4) s'arrête à C# 5. Pas d'interpolation de chaînes (`$"…"`), de membres `=>`, de `?.`, de `nameof`, de `out var` ni d'initialiseurs de propriétés automatiques. `build.bat` te le dira si quelque chose passe.
- **Aucune dépendance.** L'appli est dans `src/Penombre.cs`, ses contrôles dessinés à la main et ses couleurs dans `src/Interface.cs`. WinForms, GDI+ et Win32 (P/Invoke) uniquement. Les ressources intégrées (icônes, police Nunito) sont listées dans `build.bat`.
- **Icônes.** Modifie `tools/make_icon.py` et lance-le (Pillow et Playwright) : il réécrit `assets/penombre.ico`, `assets/logo.png` et `assets/tray-icons.png`, sans métadonnées.
- **Pixels physiques.** Le processus gère le DPI par écran : toutes les coordonnées sont en pixels physiques.
- **Aucun texte d'interface en dur** : chaque texte est dans `T` (anglais et français).
- **Les réglages** passent par la classe `Settings` (`%APPDATA%\Penombre\settings.ini`) ; garde un petit fichier clé=valeur.
- **Les erreurs** vont dans le journal (`Log.Error`), jamais dans une boîte de message en boucle.
- Code et commentaires en anglais, indentation de 4 espaces, fins de ligne CRLF (gérées par `.editorconfig` et `.gitattributes`).

## Règles pour tout ce qui touche au jeu

Pénombre doit rester un simple calque par-dessus le jeu. Les pull requests qui font l'une de ces choses ne seront pas acceptées :

- injecter du code dans le client Dofus, ou lire ou écrire sa mémoire ;
- envoyer des entrées au jeu, automatiser une action de jeu, ou lire des données du jeu ;
- capturer, stocker ou envoyer des captures d'écran ou des données de joueurs ;
- ajouter des ressources du jeu (images, sons, polices) appartenant à Ankama.

## Ajouter une langue

1. Dans `src/Penombre.cs`, trouve `static class T`.
2. Ajoute une colonne à chaque entrée de `texts` et choisis-la dans `S()` pour le code de ta langue sur deux lettres (par exemple `"es"`).
3. Garde les marqueurs `{0}`.
4. Teste avec `dist\Penombre.exe --lang=es`, ouvre les réglages et l'éditeur de zones, et vérifie que rien n'est coupé.
5. Mentionne la langue dans le README.

## Pull requests

1. Fork, crée une branche, fais ta modification.
2. Vérifie que `build.bat` compile, lance le [self-test](#self-test) et teste sur Windows avec Dofus 3 (idéalement avec deux clients pour ce qui touche au multi-compte).
3. Ajoute une ligne dans `CHANGELOG.md`, dans une section **Non publié** en haut.
4. Ouvre la pull request et remplis la liste de vérification.

En contribuant, tu acceptes que ta contribution soit publiée sous [GPL-3.0](LICENSE), comme le reste du projet.

## Publier une version (mainteneur)

1. Mets à jour `Program.Version` et les deux lignes `AssemblyVersion` / `AssemblyFileVersion` dans `src/Penombre.cs`.
2. Renomme la section **Non publié** de `CHANGELOG.md` avec la nouvelle version (par exemple `## 0.6.0`) et ajoute-la au sommaire.
3. Commit, puis tag : `git tag v0.6.0` et `git push --tags`.
4. GitHub Actions vérifie que le tag correspond à `Program.Version`, compile `Penombre.exe` et publie la version avec les notes du CHANGELOG et le SHA-256 de l'exe. Le lien de téléchargement du README pointe toujours vers la dernière version.
