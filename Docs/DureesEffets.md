# Durées des effets temporaires

Ce document clarifie la façon dont **Symphonie** mesure les durées de buffs, débuffs
et statuts. L'objectif est d'assurer des réglages cohérents entre la data (SO) et
le runtime.

## Règle principale : des tours, pas des secondes

- Toutes les durées de buffs/débuffs sont exprimées en **tours**.
- Un effet est décrémenté **à la fin du tour** de l'unité qui le porte.
- Une durée inférieure ou égale à `0` signifie **durée infinie**.
- Toute durée fractionnaire est **arrondie à l'entier supérieur** pour éviter
  qu'un effet ne dure moins longtemps que prévu.

## Où cela est appliqué

- Les buffs/débuffs passent par `CharacterStatusEffectController`.
- Le tick par tour est déclenché dans `CharacterUnit.ProcessEndOfTurnStatuses`.
- `NewBattleManager.EndTurn` appelle cette routine pour l'unité qui termine son tour.

## Impacts pour le design

- Les champs `buffDuration`/`debuffDuration` des `MusicalMoveSO` et `ItemData`
  doivent être renseignés en nombre de tours.
- Les items d'extension d'effet (ex. "Extension d’Effets") ajoutent des tours,
  pas des secondes.
