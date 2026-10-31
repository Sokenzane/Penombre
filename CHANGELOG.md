# Journal des modifications

## Sommaire

- [Non publié](#non-publié)
- [0.5.1](#051)
- [0.5.0](#050)
- [0.4.0](#040)
- [0.3.0](#030)
- [0.2.0](#020)
- [0.1.0](#010)

## Non publié

- L'icône adaptative suit l'heure même quand le mode automatique est coupé (elle restait sur « nuit »)

## 0.5.1

Contient toutes les nouveautés de la 0.5.0 (nouvelle fenêtre de réglages, icônes, aperçu en direct…), détaillées dans le [journal des modifications](https://github.com/Sokenzane/Penombre/blob/main/CHANGELOG.md#050).

- Corrigé : la 0.5.0 se fermait dès le lancement (un texte de l'interface était déclaré deux fois). Elle est remplacée par celle-ci
- La compilation sur GitHub vérifie désormais que chaque texte de l'interface n'est déclaré qu'une fois

## 0.5.0

- Nouveau **look** pour la fenêtre de réglages, dans l'esprit de l'interface de Dofus 3 : rubriques à gauche, panneaux d'ardoise en relief, curseurs verts à poignée en gemme, boutons lumineux, police Nunito intégrée
- Nouvel **aperçu en direct** dans la rubrique Ambiance : une partie du jeu sans et avec le filtre
- Ambiances présentées en **vignettes**, chacune vue à travers son propre filtre
- Nouvelles **icônes de la zone de notification** : un œuf de lune qui change avec le ciel (par défaut), un œuf veilleur qui dort le jour, ou une lune bombée ; au choix dans Affichage
- Nouvelle icône et nouveau logo (l'œuf de lune)
- L'icône peut suivre l'heure (par défaut) ou garder une couleur au choix : case **Adapter l'icône à l'heure**, puis clic sur l'image voulue
- Les heures se saisissent dans un champ horaire sombre ; le menu de la zone de notification reprend les mêmes couleurs
- Relancer Pénombre alors qu'il tourne déjà ouvre sa fenêtre de réglages
- Nouveau bouton **Réinitialiser les réglages…** (onglet Général), avec confirmation
- Rubrique Comptes : changer l'ambiance, l'intensité ou le vignettage d'un personnage active aussitôt ses réglages propres (les contrôles restaient grisés tant que la case n'était pas cochée)
- Corrigé : une fenêtre très fine au-dessus de Dofus (la poignée d'onglet d'un outil qui regroupe les fenêtres, par exemple) coupait une bande dans le filtre
- Le self-test vérifie les nouvelles pages, les polices et les 36 images d'icônes, et que deux comptes aux mêmes réglages reçoivent la même teinte

## 0.4.0

- Nouveaux **réglages par compte** : un personnage peut avoir sa propre ambiance, intensité et vignettage au lieu de ceux par défaut ; les réglages sont gardés pour les personnages qui ne sont pas ouverts
- Nouvelle **transition douce** entre ambiances, intensités et vignettages
- Nouveau raccourci **coup d'œil** (`Ctrl+Alt+C` par défaut) : maintiens-le pour voir le jeu sans filtre
- Nouveaux **raccourcis personnalisables** (onglet Raccourcis), avec signalement de ceux qui sont indisponibles
- Nouvel horaire **vrai coucher et lever du soleil** : choisis une ville ou des coordonnées, calcul hors ligne
- Nouvelle **lune dans la zone de notification** qui reflète l'état : pleine, croissant, contour, grise
- Nouvelle option **seulement quand Dofus est au premier plan**
- Nouvelle **teinte du vignettage** et **grain pellicule** animé en option
- Nouveau **mode portable** : réglages et journal dans un dossier `Penombre-data` à côté de l'exe
- Nouvel **export / import** des réglages
- Les zones claires peuvent être **ancrées à leur bord le plus proche** et **suivre la hauteur de la fenêtre** (par défaut)
- Fenêtre de réglages réorganisée en onglets
- Corrigé : le diagnostic et le journal affichaient Windows 6.2 au lieu de la vraie version de Windows
- Corrigé : les onglets de réglages pouvaient être trop petits pour leur contenu et afficher des barres de défilement ; ils sont maintenant dimensionnés sur le plus grand
- Un filtre caché ou réduit par un autre programme est réaffiché automatiquement
- Corrigé : survoler un bouton de la barre des tâches (aperçus des fenêtres) ou Alt+Tab coupait une bande sur toute la largeur du filtre
- Corrigé : un filtre tout juste créé, réaffiché ou découpé autour d'une zone claire pouvait rester invisible un instant ; il est maintenant dessiné tout de suite
- Performances : les images du shader sont dessinées par une pompe de rendu quand l'appli est inactive, au lieu du minuteur ; elles ne retardent jamais les raccourcis ni le suivi des fenêtres. Le shader tourne à environ 30 images par seconde, le rythme de l'API Magnification de Windows ; les fenêtres Dofus qui ne sont pas au premier plan sont rafraîchies au quart de ce rythme
- Le coup d'œil s'arrête tout seul au bout d'une minute, au cas où une touche resterait bloquée
- Nouveau self-test (`tools\selftest.bat`, version de test uniquement) qui pilote l'appli sur les fenêtres Dofus ouvertes (un ou plusieurs comptes) et vérifie 116 points

## 0.3.0

- Nouveau **mode automatique** : le filtre suit l'heure réelle (crépuscule, nuit, aube, jour), avec des horaires réglables ; raccourci `Ctrl+Alt+A`
- Nouveau **fondu** à l'activation, à la coupure et au démarrage (durée réglable)
- Nouvelle **fenêtre de réglages**, appliqués en direct
- Nouvelle ambiance **Personnalisée** : luminosité, saturation, température, noirs relevés
- Nouveau **réglage par compte** : chaque personnage peut être filtré ou non
- Nouvelles **zones claires** : des parties de l'interface du jeu (sorts, chat…) restent sans filtre, avec un éditeur dessiné sur la fenêtre Dofus
- Nouvelle option **lancer au démarrage de Windows**
- Nouveau **journal d'erreurs** (`%APPDATA%\Penombre\penombre.log`) et menu « Ouvrir le journal »
- Performances : réagit aux événements de Windows au lieu de vérifier en boucle, shader à 20 images par seconde pour les fenêtres Dofus en arrière-plan, au repos quand aucun filtre n'est affiché

## 0.2.0

- Nouveau rendu **shader** : la zone de jeu est redessinée à travers une matrice de couleur (luminosité, saturation, teinte, noirs bleutés) ; le voile reste disponible en secours
- Nouveau **vignettage** (aucun, léger, moyen, fort) ; raccourci `Ctrl+Alt+V`
- Nouvelle ambiance : Nuit d'antan
- Corrigé : en multi-compte, passer d'une fenêtre Dofus à l'autre assombrissait le jeu de plus en plus. Chaque filtre est maintenant découpé à la partie visible de sa fenêtre, les filtres ne se superposent donc jamais
- Corrigé : gestion du premier plan simplifiée (filtres toujours au-dessus, mais découpés autour des fenêtres qui couvrent Dofus)
- Nouveau menu **Copier le diagnostic**
- Exécutable 64 bits

## 0.1.0

- Filtre teinté, traversé par les clics, ajusté à la zone de jeu de chaque fenêtre Dofus 3
- Quatre ambiances, intensité réglable, raccourcis globaux, icône et menu dans la zone de notification
- Réglages enregistrés, interface en français et en anglais
