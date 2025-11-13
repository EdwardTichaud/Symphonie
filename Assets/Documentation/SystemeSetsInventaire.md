# Système de sets d'inventaire et de répertoire musical

Afin de respecter l'intention de Symphonie, chaque personnage jouable peut désormais
configurer des ensembles de compétences et d'objets favoris. Ces sets permettent
de mettre en avant les outils les plus utilisés tout en conservant l'accès complet
à tout le répertoire d'un héros.

## Principes généraux

- **Accessibilité** : les débutants retrouvent immédiatement leurs actions
  essentielles, affichées en tête de liste dans les menus de combat.
- **Profondeur** : les joueurs expérimentés peuvent préparer plusieurs combinaisons
  de MusicalMoves et d'Items pour adapter leur stratégie aux événements de
  l'histoire décrite dans `HistoireSymphonie.md`.
- **Souplesse** : lorsqu'aucun set n'est actif, l'ordre original est conservé et
  toutes les actions restent disponibles sans restriction.

## Configuration dans l'inspecteur Unity

Chaque `CharacterData` expose deux nouvelles listes :

1. **Sets personnalisés d'attaques musicales** (`musicalMoveSets`)
   - `setName` : nom éditorial pour identifier rapidement le set.
   - `prioritizedMoves` : liste ordonnée de `MusicalMoveSO` mis en avant.
   - `defaultMusicalMoveSetIndex` permet de choisir le set actif par défaut
     (mettre `-1` pour conserver l'ordre original).
2. **Sets personnalisés d'items** (`itemSets`)
   - `setName` : identifiant du regroupement d'objets.
   - `prioritizedItems` : liste ordonnée des `ItemData` favoris.
   - `defaultItemSetIndex` suit la même logique que pour les attaques.

Les champs `currentMusicalMoveSetIndex` et `currentItemSetIndex` sont gérés
automatiquement au lancement d'un combat.

## Utilisation en jeu

- Le menu de compétences appelle automatiquement `OrderMovesForCurrentSet` pour
  afficher les MusicalMoves favoris en premier.
- L'ouverture de l'inventaire utilise `OrderItemsForCurrentSet` afin de proposer
  les objets clefs avant le reste de la réserve.
- Les listes complètes restent intactes : si un set ne contient pas une entrée,
  elle sera tout de même disponible après les favoris.

## Conseils de conception

- Créez un set « Apprentissage » avec trois actions simples pour les premiers
  combats d'un personnage, puis un set « Maîtrise » reprenant les combinaisons
  avancées reliées aux arcs narratifs de la symphonie.
- N'oubliez pas de maintenir la documentation des MusicalMoves et Items lorsque
  vous en créez de nouveaux : leurs descriptions doivent rester cohérentes avec
  l'évolution de l'histoire.
- Lors d'un changement majeur d'équipement (par exemple après une scène clé du
  `Docs/HistoireSymphonie.md`), basculez vers un set adapté via
  `CharacterUnit.ActivateMusicalMoveSet` ou `ActivateItemSet`.

Ce système vise à fluidifier les combats tout en préservant la richesse
chorégraphique propre à Symphonie.
