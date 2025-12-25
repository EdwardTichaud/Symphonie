# Typage des harmoniques

Ce document détaille la manière dont chaque MusicalMove consomme et génère des harmoniques pour respecter la progression narrative et les combos recherchés.

Chaque **MusicalMove** précise explicitement deux informations :
- `consumedHarmonicType` : la couleur d'harmonique dépensée lors de l'utilisation.
- `generatedHarmonicType` : la couleur d'harmonique rendue au lanceur lorsque le move génère un gain.

Exception notable : l'Harmonique **Lumière** agit comme un joker. Elle peut remplacer n'importe quel type lors du paiement d'un coût d'harmonique. Dans le lore, elle représente l'espoir, demeure extrêmement rare, et constitue l'harmonique principal de Lucian.

En combat, la timeline n'affiche que le total d'harmoniques. Pour consulter le détail par type, utilisez l'input **Menu** de l'action map *Battle* : cela ouvre un récapitulatif par personnage et applique une légère pénalité de score, afin de valoriser les joueurs qui mémorisent leurs flux.

Même lorsqu'un coût ou un gain vaut `0`, ces champs aident les joueuses et joueurs à planifier leurs combos et les designers à assurer la cohérence avec l'histoire (voir `Docs/HistoireSymphonie.md`). Les nouveaux moves doivent donc documenter leurs coûts et gains en précisant clairement ces types.

Les **CharacterData** disposent en complément d'un champ `awakeHarmonicThreshold` (ancien `resonancePoint`) et d'un `baseHarmonicCharge`. Le premier indique la quantité à atteindre pour s'éveiller, tandis que le second fixe la réserve initiale en début de combat. Cette valeur de base pourra être modifiée dynamiquement par d'autres systèmes (objets, événements de narration, etc.).

Cette précision rend l'apprentissage intuitif pour les débutants tout en révélant aux stratèges aguerris des fenêtres de combos alignées avec la légende de Symphonie.
