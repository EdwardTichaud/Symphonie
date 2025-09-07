# Nouveaux MusicalMoves et Items

Ce document décrit les nouvelles actions musicales et objets utilisables ajoutés au projet **Symphonie**.

## Limites d'utilisation
Chaque MusicalMove ou Item peut maintenant définir un nombre maximal d'utilisations par tour et par combat. Les champs
`maxUsesPerTurn` et `maxUsesPerBattle` des ScriptableObjects permettent de configurer ces limites. Une valeur de `0` indique
une utilisation illimitée.

## MusicalMove : Crescendo Fulgurant
- **Type** : Attaque
- **Effet** : inflige 30 points de dégâts à une cible unique.
- **Coût** : 2 points d'harmonie et 2 points de fatigue.
- **Gain** : aucun gain d'harmonie supplémentaire.
- **Utilisation conseillée** : idéal pour terminer un adversaire affaibli. Les joueurs débutants pourront l'utiliser simplement après avoir accumulé suffisamment d'harmonie, tandis que les joueurs avancés pourront l'enchaîner avec des débuffs pour maximiser les dégâts.

## Item : Élixir Revitalisant
- **Effet** : ranime une unité alliée avec 50% de ses points de vie.
- **Coût** : aucun, consommable classique.
- **Utilisation conseillée** :
  - Parfait pour les débutants en cas de défaite imprévue d'un allié.
  - Les joueurs expérimentés peuvent le combiner avec des MusicalMoves de protection pour sécuriser le retour au combat.


## MusicalMove : Arp\xE8ge R\xE9g\xE9n\xE9rant
- **Type** : Soutien
- **Effet** : soigne 20 PV \xE0 tous les alli\xE9s.
- **Co\xFBt** : 2 points d'harmonie et 2 points de fatigue.
- **Gain** : aucun.
- **Utilisation conseill\xE9e** : parfaite pour maintenir l'\xE9quipe en vie. Facile \xE0 ex\xE9cuter pour les d\xE9butants, mais permet aux joueurs exp\xE9riment\xE9s d'encha\xEEner de longs combats.

## MusicalMove : Staccato \xC9tourdissant
- **Type** : D\xE9buff
- **Effet** : endort une cible ennemie pendant un tour.
- **Co\xFBt** : 2 points d'harmonie et 2 points de fatigue.
- **Gain** : aucun.
- **Utilisation conseill\xE9e** : id\xE9al pour interrompre un adversaire puissant le temps de pr\xE9parer une combinaison.

## Item : Cl\xE9 du Courage
- **Effet** : augmente la Force d'un alli\xE9 de 10 pendant deux tours.
- **Co\xFBt** : aucun.
- **Utilisation conseill\xE9e** : utile d\xE8s les premiers combats pour booster un personnage offensif. Les strat\xE8ges aguerris l'exploiteront pour maximiser les d\xE9g\xE2ts d'un encha\xEEnement de MusicalMoves.

## Item : Bombe Sonique
- **Effet** : inflige 15 points de d\xE9g\xE2ts \xE0 tous les ennemis.
- **Co\xFBt** : aucun.
- **Utilisation conseill\xE9e** : permet d'affaiblir tout un groupe d'ennemis avant d'utiliser des attaques cibl\xE9es plus puissantes.
