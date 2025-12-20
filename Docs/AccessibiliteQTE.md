# Accessibilité des QTE

Ce document décrit les réglages disponibles dans `RhythmQTEManager` pour rendre
les QTE plus lisibles et adaptables aux joueurs.

## Réglages principaux

- `qteWindowScale` : multiplie la durée de la fenêtre (1 = valeur par défaut).
- `qteValidationPadding` : marge en pixels ajoutée à la zone de validation de la barre.
- `qtePerfectThreshold` : seuil relatif (0..1) pour qualifier un timing de parfait.
- `defenseWindowScale` : élargit les fenêtres de parade/esquive en défense.
- `showQteFeedback` : affiche un feedback textuel (Parfait/Bien/Trop tôt/Trop tard/Raté).

## Conseils

- Augmenter `qteWindowScale` pour les joueurs débutants.
- Ajouter un petit `qteValidationPadding` pour les QTE rapides.
- Laisser `qtePerfectThreshold` bas (0.3~0.4) pour conserver la valeur du timing parfait.
