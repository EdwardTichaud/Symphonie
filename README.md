# Symphonie

Ce dépôt contient le projet Unity *Symphonie*.

## Système de cinématiques

Un système simple et modulable a été ajouté pour créer des cinématiques plus facilement.
Consultez les scripts dans `Assets/Scripts/Classes` et `Assets/Scripts/MonoBehavioursUsed` :

- **CinematicSequenceSO** : objet scriptable décrivant une suite d'étapes (timelines, dialogues, attentes ou événements).
- **CinematicPlayer** : composant chargeant une `CinematicSequenceSO` et l'exécutant pas à pas.
- **CinematicTrigger** : déclencheur optionnel qui joue automatiquement la cinématique lorsqu'un joueur entre dans son collider.

Chaque étape est de type `PlayTimeline`, `Wait`, `Dialogue` ou `Event`. Cette approche permet d'enchaîner simplement plusieurs actions sans créer une Timeline unique.
