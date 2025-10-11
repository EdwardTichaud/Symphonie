# Guide de configuration du CharacterAnimationController

> Ce document décrit pas à pas la manière de préparer un Animator Unity pour le nouveau pipeline paramétrique. Il complète les outils intégrés dans l'inspecteur et permet de vérifier que les transitions respecteront la narration d'**Histoire de Symphonie** tout en restant accessibles aux débutants.

## Préparation du contrôleur

1. **Sélectionnez le personnage** dans la hiérarchie et assurez-vous que le composant `CharacterAnimationController` est bien présent avec un `Animator` associé.
2. **Attribuez un `RuntimeAnimatorController`** compatible (AnimatorController classique ou AnimatorOverride basé dessus).
3. Ouvrez **l'inspecteur** et déroulez la section `Assistant de configuration Animator` nouvellement ajoutée.

## Utilisation de l'assistant

- Cliquez sur **« Analyser l'Animator »** pour obtenir un rapport détaillé des paramètres présents/missing. Les messages d'avertissement apparaissent directement dans l'inspecteur.
- Appuyez sur **« Configurer automatiquement les paramètres »** pour ajouter les entrées manquantes (int, float ou trigger) en conservant l'ordre logique actuel du projet.
- Vous pouvez également utiliser le **menu contextuel** du composant (`clic droit > Animator/Configurer automatiquement les paramètres`) pour lancer la configuration sans ouvrir l'inspecteur.

## Paramètres attendus

| Layer | Nom par défaut | Type | Rôle |
|-------|----------------|------|------|
| Body  | `BodyState` | Int | Sélection de l'état principal (Idle, Walk, Run, etc.). |
| Body  | `BodyTransition` | Float | Durée de transition à appliquer. |
| Body  | `BodyNormalizedTime` | Float | Point de départ normalisé lors de l'entrée dans l'état. |
| Body  | `BodyInstant` | Trigger | Forcer une transition immédiate (utilisé pour les QTE par exemple). |
| Body  | `BodySpeed` | Float | Vitesse normalisée pour le blend-tree de locomotion. |
| Face  | `FaceState` | Int | Expression faciale courante. |
| Face  | `FaceTransition` | Float | Temps de transition entre expressions. |
| Face  | `FaceInstant` | Trigger | Transition instantanée côté visage. |
| Body  | `exitAction` | Trigger | Retour vers l'état neutre après une action de combat. |
| Body  | `isTurning` | Trigger | Autorise les pivots rapides pour synchroniser la direction.

> **Note :** Si vous personnalisez les noms des paramètres dans le composant, l'assistant tiendra compte de ces nouvelles valeurs et recréera les entrées correspondantes dans l'Animator.

## Bonnes pratiques

- **Transitions cohérentes :** reliez les paramètres ci-dessus aux transitions appropriées dans l'Animator afin que les états (Falling, Landing, etc.) restent parfaitement synchronisés.
- **Visuel + gameplay :** testez chaque transition en jeu pour vérifier qu'elle respecte les intentions scénaristiques décrites dans la documentation « Histoire de Symphonie ».
- **Accessibilité :** conservez des transitions fluides pour les débutants, tout en permettant aux joueurs expérimentés de maîtriser des combinaisons avancées de `MusicalMoves` et d'`Items`.

Grâce à ces étapes, le système d'animation reste entièrement piloté par paramètres, garantissant un enchaînement propre entre les états (`Falling_Loop` → `Landing`, etc.) sans appel direct à `Animator.Play()`.
