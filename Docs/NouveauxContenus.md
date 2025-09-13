# Nouveaux MusicalMoves et Items

Ce document décrit les nouvelles actions musicales et objets utilisables ajoutés au projet **Symphonie**.

## Limites d'utilisation
Chaque MusicalMove ou Item peut maintenant définir un nombre maximal d'utilisations par tour et par combat. Les champs
`maxUsesPerTurn` et `maxUsesPerBattle` des ScriptableObjects permettent de configurer ces limites. Une valeur de `0` indique
une utilisation illimitée.

## Conditions d'altitude
Les MusicalMoves peuvent désormais spécifier une contrainte de hauteur :
- **Aériens** : réalisables uniquement si la cible ne touche pas le sol.
- **Terriens** : réalisables uniquement si la cible est au sol.
- **Aériens et terriens** : utilisables dans toutes les situations.

Cette classification aide les débutants à comprendre rapidement les restrictions tout en offrant aux joueurs avancés des
combinaisons plus techniques impliquant des changements d'altitude.

## Nouveau MoveType : Altération
Ce type regroupe les actions qui transforment le terrain ou l'état d'une cible
sans infliger directement un bonus ou un malus classique. Elles ouvrent des
opportunités tactiques que les novices peuvent appréhender facilement tout en
permettant aux vétérans de créer des enchaînements complexes.

## Timeline caméra continue
Un champ `fullTimeline` est disponible dans chaque **MusicalMove** et désormais dans chaque **Item**. Il définit un
mouvement de caméra couvrant l'ensemble de l'action (préparation, utilisation/exécution et repli) pour assurer un
suivi sans coupure. La rotation du lanceur est enregistrée dès la première frame de la phase de préparation puis
conservée jusqu'à la fin du move ou de l'objet, même si plusieurs timelines s'enchaînent, afin de garantir une
orientation cohérente de la caméra.

Si ce champ est laissé vide, le jeu place automatiquement la caméra de combat
de façon à voir simultanément le lanceur et sa cible, garantissant ainsi une
lisibilité optimale même sans configuration spécifique.

Les phases classiques restent gérées par le code du jeu afin de déclencher les téléportations nécessaires entre chaque
étape. Les timelines `preparingTimeline`, `performingTimeline` et `retreatTimeline` peuvent être lues en
**superposition** pour animer le lanceur ou ses effets pendant que la caméra suit la timeline principale.

Lorsqu'un **MusicalMove** ou un **Item** est utilisé, la `fullTimeline` démarre en premier puis les timelines de
préparation, d'exécution et de repli se succèdent automatiquement en parallèle. Le lanceur peut être téléporté entre la
fin de la préparation et le début de l'exécution ainsi qu'entre l'exécution et le repli afin d'assurer un enchaînement
fluide de l'action.

## Déplacement optionnel
Un booléen `requiresMovement` est présent dans chaque **MusicalMove** et **Item**.
- `true` : le lanceur se déplace ou se téléporte pour atteindre sa cible.
- `false` : l'action se déclenche sans déplacement, évitant toute téléportation.

Cette option rend le jeu plus accessible en autorisant des actions à distance simples tout en offrant aux joueurs avancés
des possibilités de combinaisons qui n'imposent plus un repositionnement systématique.

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

## MusicalMove : Pont Harmonique
- **Type** : Altération
- **Effet** : crée un pont éphémère sous une cible volante afin de la considérer comme au sol.
- **Coût** : 1 point d'harmonie et 1 point de fatigue.
- **Gain** : aucun.
- **Utilisation conseillée** : parfait pour les débutants qui peinent à atteindre les adversaires aériens. Les joueurs expérimentés pourront l'enchaîner avec des attaques terrestres pour réaliser des combinaisons originales.

## MusicalMove : Abîme Harmonique
- **Type** : Altération
- **Effet** : annule le sol sous la cible, la forçant à flotter et la rendant sensible aux stratégies aériennes.
- **Coût** : 1 point d'harmonie et 1 point de fatigue.
- **Gain** : aucun.
- **Utilisation conseillée** : utile pour isoler un ennemi trop bien protégé au sol. Les stratèges confirmés l'utiliseront pour préparer des enchaînements de MusicalMoves aériens.
