# Système de succès via Timeline

Ce document explique comment déclencher des succès dans **Symphonie** à l'aide des signaux d'une Timeline.

1. **Préparer les succès**
   - Créez un `AchievementSO` pour chaque succès dans `Assets/Scripts/Classes`.
   - Renseignez l'identifiant, le nom et la description du succès en tenant compte de l'histoire (voir `Docs/HistoireSymphonie.md`).

2. **Placer le gestionnaire**
   - Ajoutez l'objet `AchievementManager` dans votre scène principale et renseignez la liste `achievements` avec tous les succès disponibles.
   - Lorsqu'un succès est débloqué, il quitte automatiquement cette liste pour être ajouté à `unlockedAchievements`, permettant de suivre facilement les progrès depuis l'inspecteur.

   - L'objet est persistant entre les scènes pour conserver l'état des succès.

3. **Déclencher depuis une Timeline**
   - Ajoutez un composant `AchievementSignalReceiver` sur un objet présent sur la Timeline.
   - Dans la Timeline, insérez un `Signal Emitter` et associez le succès à débloquer à l'événement `TriggerAchievement`.

Grâce à cette approche, il est facile de lier l'obtention d'un succès à un moment narratif précis, qu'il s'agisse d'une cinématique ou d'une action de gameplay scénarisée.
