<p align="center">
  <img src="assets/logo.png" width="96" alt="Logo de Pénombre">
</p>

<h1 align="center">Pénombre</h1>

<p align="center">
  Un <b>mode nuit</b> pour Dofus 3, comme celui de Dofus 1.29.<br>
  Un petit <code>.exe</code>, rien à installer. Windows 10 et 11 (64 bits).
</p>

<p align="center">
  <a href="https://github.com/Sokenzane/Penombre/releases/latest"><img src="https://img.shields.io/github/v/release/Sokenzane/Penombre?label=t%C3%A9l%C3%A9charger" alt="Dernière version"></a>
  <a href="https://github.com/Sokenzane/Penombre/actions/workflows/build.yml"><img src="https://github.com/Sokenzane/Penombre/actions/workflows/build.yml/badge.svg" alt="Compilation"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/licence-GPL--3.0-blue" alt="Licence : GPL-3.0"></a>
</p>

<p align="center">
  <a href="https://github.com/Sokenzane/Penombre/releases/latest/download/Penombre.exe"><b>Télécharger Penombre.exe</b></a>
</p>

## Sommaire

- [C'est quoi ?](#cest-quoi-)
- [Est-ce sans risque pour mon compte ?](#est-ce-sans-risque-pour-mon-compte-)
- [Téléchargement](#téléchargement)
- [Utilisation](#utilisation)
  - [Raccourcis](#raccourcis)
  - [Fenêtre de réglages](#fenêtre-de-réglages)
  - [Ambiances](#ambiances)
  - [Mode automatique](#mode-automatique)
  - [Zones claires](#zones-claires)
  - [Plusieurs comptes](#plusieurs-comptes)
  - [Icône de la zone de notification](#icône-de-la-zone-de-notification)
  - [Mode portable et sauvegarde](#mode-portable-et-sauvegarde)
- [Fonctionnement](#fonctionnement)
  - [Rendus](#rendus)
  - [Multi-compte](#multi-compte)
  - [Performances](#performances)
- [Fichiers](#fichiers)
- [Diagnostic et journal](#diagnostic-et-journal)
- [Limites connues](#limites-connues)
- [Compiler soi-même](#compiler-soi-même)
- [Contribuer](#contribuer)
- [Licence](#licence)

## C'est quoi ?

Dofus 1.29 pouvait passer le jeu en mode nuit ; Dofus 3 ne le peut pas. Pénombre pose sur la zone de jeu un filtre traversé par les clics qui l'assombrit, refroidit les couleurs et relève un peu les noirs, pour une ambiance nocturne :

- **Vrai travail des couleurs** (shader) : chaque pixel est recalculé à travers une matrice de couleur, au lieu d'être simplement recouvert d'une couche sombre.
- **Ambiances** : nuit bleutée, clair de lune, crépuscule, nuit noire, nuit d'antan, ou la tienne.
- **Suit l'heure réelle** si tu veux, avec des horaires fixes ou le **vrai coucher et lever du soleil** de ta ville, et des transitions douces.
- **Prêt pour le multi-compte** : un filtre par fenêtre Dofus, sans cumul d'assombrissement, et des **réglages par compte**.
- **Zones claires** : la barre de sorts ou le chat restent lisibles.
- **Coup d'œil** : maintiens un raccourci pour voir le jeu sans filtre.
- **Vignettage** (teinte au choix) et **grain pellicule** en option.

## Est-ce sans risque pour mon compte ?

Pénombre **ne touche jamais au client Dofus** : pas d'injection, pas de lecture mémoire, rien n'est envoyé au jeu ni aux serveurs. Le filtre est une fenêtre Windows à part, posée par-dessus le jeu, comme une teinte d'écran (f.lux, l'éclairage nocturne de Windows…). Il utilise seulement des fonctions publiques de Windows pour trouver les fenêtres du jeu et dessiner par-dessus.

Ankama n'a ni examiné ni approuvé Pénombre. Lis toi-même les conditions d'utilisation du jeu, et utilise-le à tes risques.

## Téléchargement

**[Télécharge Penombre.exe](https://github.com/Sokenzane/Penombre/releases/latest/download/Penombre.exe)** et lance-le. Pas d'installation, pas de droits administrateur. Les notes de version et les versions précédentes sont sur la page [Releases](https://github.com/Sokenzane/Penombre/releases), avec le SHA-256 de chaque exe (voir [SECURITY.md](SECURITY.md#vérifier-un-téléchargement)). Il faut .NET Framework 4, fourni avec tous les Windows 10 et 11.

> L'exe n'est pas signé : Windows SmartScreen peut afficher *« Windows a protégé votre ordinateur »*. Clique sur **Informations complémentaires**, puis **Exécuter quand même**. Tu peux aussi [le compiler toi-même](#compiler-soi-même) en un clic.

Dofus doit être en mode **fenêtré** ou **plein écran fenêtré** : en plein écran exclusif, aucune fenêtre ne peut s'afficher par-dessus le jeu.

## Utilisation

Lance `Penombre.exe` : un œuf de lune apparaît dans la zone de notification (près de l'horloge, parfois cachée derrière la flèche `^`). Clic gauche dessus pour activer ou couper le filtre, clic droit pour le menu. Relancer `Penombre.exe` alors qu'il tourne déjà ouvre la fenêtre de réglages.

### Raccourcis

| Raccourci par défaut | Action |
| --- | --- |
| `Ctrl+Alt+N` | Activer / couper le filtre (avec fondu) |
| `Ctrl+Alt+C` | **Coup d'œil** : maintiens-le pour voir le jeu sans filtre |
| `Ctrl+Alt+A` | Activer / couper le mode automatique |
| `Ctrl+Alt+P` | Ambiance suivante |
| `Ctrl+Alt+PgUp` / `PgDn` | Intensité +5 % / -5 % |
| `Ctrl+Alt+V` | Vignettage suivant (aucun, léger, moyen, fort) |

Chaque raccourci se change dans **Réglages… > Raccourcis** (clique dans une case, appuie sur la nouvelle combinaison ; `Retour arrière` le supprime). Un raccourci déjà pris par une autre appli est signalé *indisponible*. Quand le Dofus au premier plan a des [réglages propres](#plusieurs-comptes), les raccourcis d'ambiance, d'intensité et de vignettage modifient ceux-là.

### Fenêtre de réglages

Menu **Réglages…**. La fenêtre reprend l'esprit de l'interface de Dofus 3 : rubriques à gauche, curseurs et boutons verts. Tout s'applique en direct, avec une transition douce :

| Onglet | Contenu |
| --- | --- |
| Ambiance | aperçu en direct (une partie du jeu sans et avec le filtre), ambiances en vignettes, curseurs de l'ambiance personnalisée, intensité, vignettage et sa teinte, grain pellicule, rendu |
| Horaires | mode automatique (horaires fixes ou vrai soleil), durée des transitions (0 à 2 s) |
| Comptes | réglages par compte |
| Affichage | icône de la zone de notification, zones claires, « seulement quand Dofus est au premier plan » |
| Raccourcis | tous les raccourcis, personnalisables |
| Général | lancement au démarrage, mode portable, dossier des réglages, export / import, journal |

### Ambiances

L'intensité (5 à 100 %) dose l'effet entre l'image d'origine et l'ambiance complète.

| Ambiance | Luminosité | Saturation | Teinte |
| --- | --- | --- | --- |
| Nuit bleutée | 55 % | 55 % | bleu froid |
| Clair de lune | 70 % | 30 % | bleu pâle, noirs relevés |
| Crépuscule | 72 % | 80 % | violet chaud |
| Nuit noire | 38 % | 50 % | bleu léger |
| Nuit d'antan | 62 % | 90 % | bleu, façon 1.29 |
| Personnalisée | réglable | réglable | froid ↔ chaud, noirs relevés |

Toucher un curseur dans les réglages crée une ambiance **Personnalisée** à partir de l'ambiance en cours.

### Mode automatique

Le filtre suit l'heure de ton PC, avec deux façons de régler les horaires :

- **Horaires fixes** (ci-dessous) ;
- **Vrai coucher et lever du soleil** : choisis ta ville (ou saisis des coordonnées). La nuit tombe 45 min avant le coucher et est complète 45 min après ; l'aube fonctionne pareil autour du lever. Le calcul se fait sans connexion, et les horaires suivent les saisons.

| Moment | Par défaut | Filtre |
| --- | --- | --- |
| Début du crépuscule | 19:30 | monte progressivement |
| Nuit complète | 21:30 | plein effet |
| Début de l'aube | 06:30 | redescend progressivement |
| Jour complet | 08:30 | aucun (shader suspendu) |

### Zones claires

Pour garder lisibles la barre de sorts, le chat ou la timeline : **Réglages… > Définir les zones…**. Un calque s'affiche sur la fenêtre Dofus : glisse pour dessiner une zone, clic droit sur une zone pour la supprimer, `Suppr` pour tout effacer, `Entrée` pour valider, `Échap` pour annuler. Les zones valent pour tous les comptes. Par défaut, elles sont **ancrées au bord le plus proche** (une barre de sorts en bas reste en bas) et **grandissent avec la hauteur de la fenêtre**, comme l'interface de Dofus ; décoche ces deux options dans **Affichage** pour des zones proportionnelles à la fenêtre.

### Plusieurs comptes

Chaque personnage peut être filtré ou non, depuis le menu **Comptes** ou les réglages. Dans **Réglages… > Comptes**, un personnage peut aussi avoir **sa propre ambiance, intensité et vignettage** au lieu de ceux par défaut : il suffit de changer l'un des trois pour que ses réglages propres s'activent. Les personnages sont reconnus au titre de leur fenêtre, et leurs réglages sont gardés même quand ils ne sont pas ouverts.

Option **Seulement quand Dofus est au premier plan** (onglet Affichage) : le filtre ne s'affiche que sur la fenêtre Dofus sur laquelle tu joues.

### Icône de la zone de notification

L'icône montre l'état d'un coup d'œil : nuit, crépuscule ou aube, jour (mode auto, pas de filtre), filtre coupé. Trois styles au choix dans **Réglages… > Affichage** :

| Style | Nuit | Crépuscule | Jour | Coupé |
| --- | --- | --- | --- | --- |
| **Œuf de lune** (par défaut) | œuf saphir, lune dorée | œuf rose et ambre | œuf ambré, soleil | œuf de pierre fêlé |
| **Œuf veilleur** | yeux ouverts | yeux ouverts | endormi | gris, endormi |
| **Lune bombée** | pleine lune | croissant | contour | croissant gris |

### Mode portable et sauvegarde

- **Mode portable** (onglet Général) : réglages et journal passent dans un dossier `Penombre-data` à côté de l'exe. Copie le dossier avec l'exe sur un autre PC, tout suit, zones comprises. Décoche pour les remettre dans `%APPDATA%`.
- **Exporter… / Importer…** : enregistre tes réglages dans un fichier, ou charge un fichier enregistré sur un autre PC.

## Fonctionnement

Les fenêtres Dofus 3 sont détectées automatiquement (classe de fenêtre `UnityWndClass` dans un processus `Dofus`).

### Rendus

- **Shader** (par défaut) : l'API Magnification de Windows redessine la zone de jeu à travers une matrice de couleur 5×5. La luminosité est multipliée, les couleurs désaturées et teintées, les noirs relevés. C'est le même principe que le `ColorTransform` Flash de la nuit de Dofus 1.29.
- **Voile** : une simple couche colorée semi-transparente, en repli si le shader n'est pas disponible.
- **Vignettage** (optionnel, avec les deux rendus) : bords et coins assombris, centre dégagé, dessiné en alpha par pixel.

### Multi-compte

Chaque filtre est découpé (`SetWindowRgn`) à la partie **réellement visible** de sa fenêtre Dofus. Pénombre parcourt les fenêtres du haut vers le bas de la pile Windows : tout ce qui est au-dessus d'une fenêtre Dofus (autre appli, autre client) en masque la partie couverte. Deux filtres ne se superposent jamais, et l'assombrissement ne s'accumule pas quand tu passes d'un compte à l'autre.

Les fenêtres traversées par les clics (overlays GPU, Discord, Steam…), invisibles ou masquées (autres bureaux virtuels) ne comptent pas comme des obstacles.

### Performances

- Pénombre réagit aux événements Windows (fenêtre déplacée, affichée, masquée, mise au premier plan…) au lieu de tout revérifier en boucle. Une vérification de secours a lieu toutes les 500 ms.
- Shader : 60 images/s pour le Dofus au premier plan, environ 20 pour ceux en arrière-plan.
- Le shader est suspendu quand Dofus est réduit ou entièrement caché, quand le filtre est coupé, ou en plein jour (mode auto). Le programme passe alors au ralenti (10 vérifications par seconde).

## Fichiers

Dans `%APPDATA%\Penombre\` (ou `Penombre-data` à côté de l'exe en [mode portable](#mode-portable-et-sauvegarde)) :

- `settings.ini` : tous les réglages, y compris ceux par compte et les raccourcis. Si ton client ne s'appelle pas `Dofus.exe`, modifie la ligne `process=`. Supprime le fichier pour tout remettre à zéro.
- `penombre.log` : le journal (démarrage, fenêtres détectées, erreurs). Au-delà de 512 Ko, il est renommé en `penombre.log.old`.

L'interface suit la langue de Windows (français ou anglais) ; `--lang=fr` ou `--lang=en` force la langue. Le lancement au démarrage écrit une seule valeur sous `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` ; rien d'autre n'est écrit sur ton système.

## Diagnostic et journal

- **Copier le diagnostic** : copie l'état complet dans le presse-papiers (version de Windows, fenêtres Dofus, filtres, mode auto, zones, comptes, pile des fenêtres Windows). Il contient les titres de tes fenêtres, donc tes noms de personnages : masque-les avant de le publier si tu préfères.
- **Ouvrir le journal** : ouvre `penombre.log` dans le Bloc-notes.

## Limites connues

- Le plein écran exclusif n'est pas pris en charge (voir [Téléchargement](#téléchargement)).
- L'interface du jeu est assombrie aussi, sauf si tu définis des [zones claires](#zones-claires). Si elles se décalent après un redimensionnement, essaie les autres options de zones ou redessine-les.
- Le shader est une transformation de couleur par pixel (matrice), pas un shader GPU programmable : pas de flou, de bloom ni de lumière localisée.
- En rendu shader, l'image filtrée peut avoir une image de retard sur le jeu.
- Le grain pellicule utilise environ 25 Mo de mémoire par fenêtre Dofus, et met une fraction de seconde à se préparer à l'activation ou au redimensionnement.
- Les horaires du soleil supposent que le fuseau horaire du PC est celui de la ville choisie.

## Compiler soi-même

Le code est dans [`src/Penombre.cs`](src/Penombre.cs) et l'interface dans [`src/Interface.cs`](src/Interface.cs) (C# 5, WinForms, aucune dépendance).

1. Clone ou télécharge le dépôt.
2. Double-clique sur **`build.bat`** : il utilise le compilateur C# fourni avec Windows.
3. Lance `dist\Penombre.exe`.

Les icônes et le logo sont générés par `python tools/make_icon.py` (nécessite Pillow et Playwright).

## Contribuer

Rapports de bugs, idées et pull requests sont les bienvenus. Voir [CONTRIBUTING.md](CONTRIBUTING.md).

## Licence

Pénombre est un logiciel libre, publié sous la [licence publique générale GNU v3.0](LICENSE).
Tu peux l'utiliser, l'étudier, le partager et le modifier ; les versions modifiées que tu distribues doivent rester open source sous la même licence et créditer le projet d'origine.

Copyright © 2026 [Sokenzane](https://github.com/Sokenzane).

La police [Nunito](https://github.com/googlefonts/nunito) (© The Nunito Project Authors), intégrée à l'exe, est sous [licence SIL Open Font 1.1](assets/fonts/OFL.txt).

Pénombre est un projet de fan indépendant, ni affilié à Ankama ni approuvé par Ankama. Dofus est une marque d'Ankama. Aucun élément du jeu n'est inclus dans ce dépôt.
