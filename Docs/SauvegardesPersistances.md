# Sauvegardes : succès, codex, sceaux

Ce document décrit les éléments additionnels persistés dans les fichiers de sauvegarde.

## Contenu persisté

- Succès débloqués (IDs des `AchievementSO`).
- Attaques musicales connues (IDs des `MusicalMoveSO`).
- Sceaux débloqués et équipés (IDs des `SceauSO`).

## Résolution des IDs

- Les succès utilisent le champ `id` si renseigné, sinon le nom d’asset.
- Les MusicalMoves et Sceaux utilisent le nom d’asset (`.name`) comme ID stable.

## Registres de référence

Pour restaurer les assets à partir des IDs, deux registres sont utilisés :

- `MusicalCodexManager.registeredMoves`
- `InventoryManager.registeredSeals`

Ces listes doivent contenir l’ensemble des assets disponibles pour garantir une
restauration complète après chargement.

## Version de sauvegarde

La structure de sauvegarde embarque un `saveVersion`. Les anciennes sauvegardes
ne contiennent pas ces collections : dans ce cas, l’état courant en scène est
conservé afin d’éviter une remise à zéro involontaire.
