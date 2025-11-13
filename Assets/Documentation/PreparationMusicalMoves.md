# Préparation des MusicalMoves sur plusieurs tours

Ce guide décrit la nouvelle mécanique de charge appliquée aux MusicalMoves pour offrir
aux joueurs débutants une lecture claire tout en ouvrant des possibilités tactiques
pour les vétérans, conformément aux enjeux narratifs rappelés dans `HistoireSymphonie.md`.

## Activer une phase de charge
- `requiresPreparationBeforeExecution` (MusicalMoveSO) : cochez cette option pour
  indiquer que le move ne se résout qu'après une ou plusieurs phases de préparation.
  Le move est déclaré au tour sélectionné, mais sa résolution ne survient qu'à la fin
  de la charge.
- `preparationTurnCount` : nombre de tours complets nécessaires avant l'exécution.
  Une valeur de `0` garde un comportement immédiat même si la case précédente est
  activée, utile pour de futurs équilibrages progressifs.

## Gérer les risques d'interruption
- `preparationFailureConditions` : liste d'entrées décrivant ce qui annule la charge.
  Chaque entrée possède :
  - `conditionType` : type d'événement (dégâts sur une attaque, débuff, événement
    scénarisé...).
  - `thresholdValue` : seuil numérique utilisé par les dégâts ou tout autre test.
  - `debuffType` : précisez un `DebuffStatType` lorsque seule une altération donnée
    interrompt la préparation.
  - `designerNote` : champ libre pour relier le réglage au récit ou à des combos
    avancés.

### Filets de sécurité appliqués automatiquement

Lorsqu'un `MusicalMove` active `requiresPreparationBeforeExecution`, deux conditions
sont insérées par défaut pour préserver l'équilibrage et la lisibilité :

1. **Dégâts massifs** : un `PreparationFailureConditionType.DamageFromSingleAttack`
   avec un `thresholdValue` minimal de **20 000** garantit que les attaques les plus
   puissantes peuvent toujours interrompre une préparation prolongée.
2. **MusicalMove interruptif** : un `PreparationFailureConditionType.InterruptingMusicalMove`
   assure qu'un move adverse explicitement conçu pour casser les préparations mettra
   fin à la charge, créant des opportunités tactiques et des moments narratifs forts.

Ces garde-fous peuvent être complétés par d'autres conditions selon les besoins
scénaristiques ou de gameplay, mais ils ne sont retirés manuellement qu'en pleine
conscience de leurs impacts sur l'expérience des joueurs.

Combinez ces champs pour orchestrer des stratégies où les héros doivent protéger un
lanceur pendant plusieurs tours, tout en respectant les arcs narratifs de Symphonie.
