# Système de succès via PointOfInterest

Ce document décrit la procédure pour débloquer un succès lorsque le joueur interagit avec un **PointOfInterest**.

1. **Préparer les succès**
   - Créez un `AchievementSO` pour chaque succès dans `Assets/Achievements`.
   - Définissez un identifiant, un nom et une description en accord avec l'histoire (voir `Docs/HistoireSymphonie.md`).

2. **Placer le gestionnaire**
   - Assurez-vous qu'un `AchievementManager` est présent dans la scène et que la liste `achievements` contient tous les succès disponibles.

3. **Configurer le PointOfInterest**
   - Sur le composant `PointOfInterest`, renseignez le champ **Succès** avec l'`AchievementSO` à débloquer.
   - Lors de l'interaction, le succès sera automatiquement débloqué si un `AchievementManager` actif est trouvé.

Grâce à cette fonctionnalité, chaque point d'intérêt peut récompenser le joueur par un succès, renforçant ainsi la progression narrative et ludique.
