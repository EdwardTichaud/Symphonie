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


## MusicalMove : Arpège Régénérant
- **Type** : Soutien
- **Effet** : soigne 20 PV à tous les alliés.
- **Coût** : 2 points d'harmonie et 2 points de fatigue.
- **Gain** : aucun.
- **Utilisation conseillée** : parfaite pour maintenir l'équipe en vie. Facile à exécuter pour les débutants, mais permet aux joueurs expérimentés d'enchaîner de longs combats.

## MusicalMove : Staccato Étourdissant
- **Type** : Débuff
- **Effet** : endort une cible ennemie pendant un tour.
- **Coût** : 2 points d'harmonie et 2 points de fatigue.
- **Gain** : aucun.
- **Utilisation conseillée** : idéal pour interrompre un adversaire puissant le temps de préparer une combinaison.

## Item : Clé du Courage
- **Effet** : augmente la Force d'un allié de 10 pendant deux tours.
- **Coût** : aucun.
- **Utilisation conseillée** : utile dès les premiers combats pour booster un personnage offensif. Les stratèges aguerris l'exploiteront pour maximiser les dégâts d'un enchaînement de MusicalMoves.

## Item : Bombe Sonique
- **Effet** : inflige 15 points de dégâts à tous les ennemis.
- **Coût** : aucun.
- **Utilisation conseillée** : permet d'affaiblir tout un groupe d'ennemis avant d'utiliser des attaques ciblées plus puissantes.
