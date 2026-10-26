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
  &nbsp;·&nbsp; <a href="README.md">Read in English</a>
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
- **Suit l'heure réelle** si tu veux : crépuscule, nuit, aube et jour, avec des transitions douces.
- **Prêt pour le multi-compte** : un filtre par fenêtre Dofus, sans cumul d'assombrissement.
- **Zones claires** : la barre de sorts ou le chat restent lisibles.

## Est-ce sans risque pour mon compte ?

Pénombre **ne touche jamais au client Dofus** : pas d'injection, pas de lecture mémoire, rien n'est envoyé au jeu ni aux serveurs. Le filtre est une fenêtre Windows à part, posée par-dessus le jeu, comme une teinte d'écran (f.lux, l'éclairage nocturne de Windows…). Il utilise seulement des fonctions publiques de Windows pour trouver les fenêtres du jeu et dessiner par-dessus.

Ankama n'a ni examiné ni approuvé Pénombre. Lis toi-même les conditions d'utilisation du jeu, et utilise-le à tes risques.

## Téléchargement

**[Télécharge Penombre.exe](https://github.com/Sokenzane/Penombre/releases/latest/download/Penombre.exe)** et lance-le. Pas d'installation, pas de droits administrateur. Les notes de version et les versions précédentes sont sur la page [Releases](https://github.com/Sokenzane/Penombre/releases), avec le SHA-256 de chaque exe (voir [SECURITY.md](SECURITY.md#verifying-a-download)). Il faut .NET Framework 4, fourni avec tous les Windows 10 et 11.

> L'exe n'est pas signé : Windows SmartScreen peut afficher *« Windows a protégé votre ordinateur »*. Clique sur **Informations complémentaires**, puis **Exécuter quand même**. Tu peux aussi [le compiler toi-même](#compiler-soi-même) en un clic.

Dofus doit être en mode **fenêtré** ou **plein écran fenêtré** : en plein écran exclusif, aucune fenêtre ne peut s'afficher par-dessus le jeu.

## Utilisation

Lance `Penombre.exe` : une lune apparaît dans la zone de notification (près de l'horloge, parfois cachée derrière la flèche `^`). Clic gauche dessus pour activer ou couper le filtre, clic droit pour le menu.

### Raccourcis

| Raccourci | Action |
| --- | --- |
| `Ctrl+Alt+N` | Activer / couper le filtre (avec fondu) |
| `Ctrl+Alt+A` | Activer / couper le mode automatique |
| `Ctrl+Alt+P` | Ambiance suivante |
| `Ctrl+Alt+PgUp` / `PgDn` | Intensité +5 % / -5 % |
| `Ctrl+Alt+V` | Vignettage suivant (aucun, léger, moyen, fort) |

### Fenêtre de réglages

Menu **Réglages…**. Tout s'applique en direct :

- ambiance, curseurs de l'ambiance personnalisée, intensité, vignettage, rendu ;
- mode automatique et ses quatre horaires ;
- durée du fondu (0 à 2 s) ;
- zones claires ;
- comptes filtrés ;
- lancement au démarrage de Windows, accès au journal.

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

Le filtre suit l'heure de ton PC :

| Moment | Par défaut | Filtre |
| --- | --- | --- |
| Début du crépuscule | 19:30 | monte progressivement |
| Nuit complète | 21:30 | plein effet |
| Début de l'aube | 06:30 | redescend progressivement |
| Jour complet | 08:30 | aucun (shader suspendu) |

### Zones claires

Pour garder lisibles la barre de sorts, le chat ou la timeline : **Réglages… > Définir les zones…**. Un calque s'affiche sur la fenêtre Dofus : glisse pour dessiner une zone, clic droit sur une zone pour la supprimer, `Suppr` pour tout effacer, `Entrée` pour valider, `Échap` pour annuler. Les zones sont proportionnelles à la taille de la fenêtre et valent pour tous les comptes.

### Plusieurs comptes

Chaque personnage peut être filtré ou non, depuis le menu **Comptes** ou les réglages. Le personnage est reconnu au titre de sa fenêtre.

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

Dans `%APPDATA%\Penombre\` :

- `settings.ini` : tous les réglages. Si ton client ne s'appelle pas `Dofus.exe`, modifie la ligne `process=`. Supprime le fichier pour tout remettre à zéro.
- `penombre.log` : le journal (démarrage, fenêtres détectées, erreurs). Au-delà de 512 Ko, il est renommé en `penombre.log.old`.

L'interface suit la langue de Windows (français ou anglais) ; `--lang=fr` ou `--lang=en` force la langue. Le lancement au démarrage écrit une seule valeur sous `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` ; rien d'autre n'est écrit sur ton système.

## Diagnostic et journal

- **Copier le diagnostic** : copie l'état complet dans le presse-papiers (fenêtres Dofus, filtres, mode auto, zones, pile des fenêtres Windows). Il contient les titres de tes fenêtres, donc tes noms de personnages : masque-les avant de le publier si tu préfères.
- **Ouvrir le journal** : ouvre `penombre.log` dans le Bloc-notes.

## Limites connues

- Le plein écran exclusif n'est pas pris en charge (voir [Téléchargement](#téléchargement)).
- L'interface du jeu est assombrie aussi, sauf si tu définis des [zones claires](#zones-claires). Elles sont proportionnelles : si l'interface de Dofus ne grandit pas avec la fenêtre, redessine-les après un gros changement de taille.
- Le shader est une transformation de couleur par pixel (matrice), pas un shader GPU programmable : pas de flou, de bloom ni de lumière localisée.
- En rendu shader, l'image filtrée peut avoir une image de retard sur le jeu.
- Pas encore testé : écrans avec des mises à l'échelle différentes (100 % et 125 %, par exemple).

## Compiler soi-même

Tout est dans un seul fichier, [`src/Penombre.cs`](src/Penombre.cs) (C# 5, WinForms, aucune dépendance).

1. Clone ou télécharge le dépôt.
2. Double-clique sur **`build.bat`** : il utilise le compilateur C# fourni avec Windows.
3. Lance `dist\Penombre.exe`.

L'icône et le logo sont générés par `python tools/make_icon.py` (nécessite Pillow).

## Contribuer

Rapports de bugs, idées et pull requests sont les bienvenus. Voir [CONTRIBUTING.md](CONTRIBUTING.md) (en anglais).

## Licence

Pénombre est un logiciel libre, publié sous la [licence publique générale GNU v3.0](LICENSE).
Tu peux l'utiliser, l'étudier, le partager et le modifier ; les versions modifiées que tu distribues doivent rester open source sous la même licence et créditer le projet d'origine.

Copyright © 2026 [Sokenzane](https://github.com/Sokenzane).

Pénombre est un projet de fan indépendant, ni affilié à Ankama ni approuvé par Ankama. Dofus est une marque d'Ankama. Aucun élément du jeu n'est inclus dans ce dépôt.
