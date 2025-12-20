# Validation des ScriptableObjects

Des validations sont exécutées dans l'éditeur pour éviter les configurations
incohérentes qui provoquent des erreurs en jeu.

## MusicalMoveSO

Contrôles principaux :
- `moveName`/`moveIcon` renseignés.
- Coûts et limites non négatifs.
- `targetTypes` non vide et contenant `defaultTargetType`.
- Si préparation multi-tour : `preparationTurnCount > 0` et `preparingTimeline` renseignée.
- Distances/temps de déplacement non négatifs.

## ItemData

Contrôles principaux :
- `itemID`, `itemName`, `itemIcon` renseignés.
- Coûts/limites non négatifs.
- Durées de buff/débuff non négatives.
- `targetTypes` non vide et contenant `defaultTargetType`.
- `requiresMovement` cohérent avec `castDistance`.
- `beatPattern` sans valeur <= 0.

Ces validations émettent des avertissements dans la console Unity et corrigent
automatiquement certains champs lorsqu'ils sont négatifs.
