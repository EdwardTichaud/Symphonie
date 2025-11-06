# Nouveaux MusicalMoves et Items

Ce document décrit les nouvelles actions musicales et objets utilisables ajoutés au projet **Symphonie**.

## Limites d'utilisation
Chaque MusicalMove ou Item peut maintenant définir un nombre maximal d'utilisations par tour et par combat. Les champs
`maxUsesPerTurn` et `maxUsesPerBattle` des ScriptableObjects permettent de configurer ces limites. Une valeur de `0` indique
une utilisation illimitée.

## Typage des Harmoniques
Chaque **MusicalMove** précise désormais explicitement deux informations :
- `consumedHarmonicType` : la couleur d'harmonique dépensée lors de l'utilisation.
- `generatedHarmonicType` : la couleur d'harmonique rendue au lanceur lorsque le move génère un gain.

Même lorsqu'un coût ou un gain vaut `0`, ces champs aident les joueuses et joueurs à planifier leurs combos et les designers à assurer la cohérence avec l'histoire (voir *Histoire de Symphonie*). Les nouveaux moves doivent donc documenter leurs coûts et gains en précisant clairement ces types.

Les **CharacterData** disposent en complément d'un champ `awakeHarmonicThreshold` (ancien `resonancePoint`) et d'un `baseHarmonicCharge`. Le premier indique la quantité à atteindre pour s'éveiller, tandis que le second fixe la réserve initiale en début de combat. Cette valeur de base pourra être modifiée dynamiquement par d'autres systèmes (objets, événements de narration, etc.).

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

## Caméras par phase
Chaque **MusicalMove** et chaque **Item** référence désormais un rôle de caméra
cinématique plutôt qu'un nom explicite :
`preparingCameraRole`, `performingCameraRole` et `retreatCameraRole`.
Ces rôles correspondent aux plans définis par le nouveau rig
(`MainMenuIdle`, `OverShoulderCasterToTarget`, `ClosePushCaster`,
`TargetReaction`, `WideEstablish`, `ProjectileFlyby`, `Victory`).
Sélectionner `None` conserve la caméra actuellement active afin de garantir une
transition fluide.

### Valeurs par défaut recommandées
Pour assurer une expérience cohérente, tout **nouveau MusicalMove** doit
initialiser ces trois champs avec les valeurs définies dans
**MusicalMove_Rhapsodie** (WideEstablish → OverShoulder → TargetReaction).
De même, les **nouveaux Items** doivent se baser sur les valeurs utilisées par
**Item_LonguePortee** (OverShoulder → OverShoulder → TargetReaction). Les
concepteurs peuvent ensuite adapter ces paramètres selon les besoins spécifiques du
contenu ajouté.

Ce nouveau système remplace l'ancien champ `cameraName` unique ainsi que
la notion de `fullTimeline` qui contrôlait auparavant la caméra.

## Déplacement optionnel
Un booléen `requiresMovement` est présent dans chaque **MusicalMove** et **Item**.
- `true` : le lanceur se déplace ou se téléporte pour atteindre sa cible.
- `false` : l'action se déclenche sans déplacement, évitant toute téléportation.

Cette option rend le jeu plus accessible en autorisant des actions à distance simples tout en offrant aux joueurs avancés
des possibilités de combinaisons qui n'imposent plus un repositionnement systématique.

## Mécanique : Marque de loyauté infiltrée
- **Principe général** : lorsqu'un **MusicalMove** qui appose une marque de loyauté (comme *Pour qui sonne le glas*) est lancé
  sur un ennemi, la cible reçoit désormais le statut **Infiltré**. L'entité croit agir pour le lanceur sans comprendre qu'elle
  sabote sa propre équipe.
- **Redirection de soutien** : tant que la marque est active, tout soin ou bouclier mono-cible provenant des alliés du marqué
  se verrouille automatiquement sur lui. Les joueurs débutants y voient une solution simple pour annuler les soins adverses,
  tandis que les stratèges peuvent isoler un boss des renforts qu'il attendait.
- **Accumulation de trahison** : chaque fois que l'ennemi infiltré inflige des dégâts, 30% de la valeur est siphonnée dans la
  marque sous forme de *fragments de dévotion*. Lorsque la marque est retirée (par expiration ou par un nouvel usage), ces
  fragments explosent en dégâts harmoniques sur la cible infiltrée et l'ennemi le plus proche.
- **Combinaisons conseillées** :
  - Les MusicalMoves de type **Protection** peuvent absorber l'explosion finale pour maintenir Lucian debout tout en laissant la
    détonation punir les ennemis regroupés.
  - Les **Altérations** modifiant le positionnement (ex. *Pont Harmonique*) permettent de rapprocher deux cibles ennemies pour
    maximiser la déflagration.
- **Cohérence narrative** : cette mécanique illustre la stratégie de Lucian consistant à retourner la propagande d'Azazel contre
  lui : au lieu de protéger ses alliés, l'ennemi infiltré renforce l'attaque harmonique des protagonistes, rappelant que la
  loyauté imposée par le Rêve peut être détournée par une improvisation bien menée (voir *Histoire de Symphonie*).

## MusicalMove : Crescendo Fulgurant
- **Type** : Attaque
- **Effet** : inflige 30 points de dégâts à une cible unique.
- **Coût** : 2 points d'harmonie et 2 points de fatigue.
- **Gain** : aucun gain d'harmonie supplémentaire.
- **Harmoniques** : consomme des harmoniques de **Lumière**, n'en génère pas.
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
- **Harmoniques** : puise dans l'harmonique **Souffle** et ne restitue rien.
- **Utilisation conseillée** : parfaite pour maintenir l'équipe en vie. Facile à exécuter pour les débutants, mais permet aux joueurs expérimentés d'enchaîner de longs combats.

## MusicalMove : Staccato Étourdissant
- **Type** : Débuff
- **Effet** : endort une cible ennemie pendant un tour.
- **Coût** : 2 points d'harmonie et 2 points de fatigue.
- **Gain** : aucun.
- **Harmoniques** : consomme des harmoniques de **Lumière**, sans restitution.
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
- **Harmoniques** : dépense une harmonique **Lumière** pour plaquer la cible au sol.
- **Utilisation conseillée** : parfait pour les débutants qui peinent à atteindre les adversaires aériens. Les joueurs expérimentés pourront l'enchaîner avec des attaques terrestres pour réaliser des combinaisons originales.

## MusicalMove : Sforzando
- **Type** : Attaque téléportée.
- **Effet** : Lucian se matérialise face à une cible unique et libère une onde percussive qui inflige 20 points de dégâts (plus la puissance courante) tout en générant 1 point d'harmonie supplémentaire.
- **Coût** : 1 point d'harmonie et 1 point de fatigue.
- **Gain** : +1 harmonie grâce à la puissance du choc musical.
- **Harmoniques** : utilise et régénère la même harmonique **Lumière**.
- **Utilisation conseillée** : idéal pour surprendre un adversaire isolé. L'attaque place Lucian au contact de sa cible ; les débutants peuvent l'utiliser comme finisher direct, tandis que les joueurs expérimentés profiteront du maintien en mêlée pour enchaîner immédiatement un combo ou préparer une interception.

## MusicalMove : Abîme Harmonique
- **Type** : Altération
- **Effet** : annule le sol sous la cible, la forçant à flotter et la rendant sensible aux stratégies aériennes.
- **Coût** : 1 point d'harmonie et 1 point de fatigue.
- **Gain** : aucun.
- **Harmoniques** : nécessite une harmonique de **Brume** et n'en rapporte pas.
- **Utilisation conseillée** : utile pour isoler un ennemi trop bien protégé au sol. Les stratèges confirmés l'utiliseront pour préparer des enchaînements de MusicalMoves aériens.
