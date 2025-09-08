# Optimisations de performance

Ce document résume les actions entreprises pour réduire le nombre de draw calls et améliorer les FPS.

## GPU Instancing
- Tous les matériaux du dossier `Assets/Materials` ont l'option **Enable GPU Instancing** activée.
- Le script d'éditeur `OptimizationTools` permet d'activer l'instancing pour de nouveaux matériaux via le menu `Symphonie/Optimisation`.

## Static Batching
- Les objets de décor peuvent être marqués comme statiques grâce à `OptimizationTools`.
- Unity pourra ainsi combiner les meshes et diminuer le coût du rendu opaque.

## Réduction des draw calls
- Regrouper les petits meshes et créer des atlases de textures reste une priorité.
- Privilégier l'utilisation de matériaux partagés et l'instancing pour les objets identiques.

## Level of Detail (LOD)
- Ajouter des `LODGroup` sur les gros modèles permet de réduire le nombre de polygones rendus au loin.

## Test de preuve
- Pour vérifier le gain, dupliquer la scène et retirer 80 % des objets opaques. Les FPS doivent augmenter sensiblement.
