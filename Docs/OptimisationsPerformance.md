# Optimisations de performance

Ce document r\u00e9sume les actions entreprises pour r\u00e9duire le nombre de draw calls et am\u00e9liorer les FPS.

## GPU Instancing
- Tous les mat\u00e9riaux du dossier `Assets/Materials` ont l'option **Enable GPU Instancing** activ\u00e9e.
- Le script d'\u00e9diteur `OptimizationTools` permet d'activer l'instancing pour de nouveaux mat\u00e9riaux via le menu `Symphonie/Optimisation`.

## Static Batching
- Les objets de d\u00e9cor peuvent \u00eatre marqu\u00e9s comme statiques gr\u00e2ce \u00e0 `OptimizationTools`.
- Unity pourra ainsi combiner les meshes et diminuer le co\u00fbt du rendu opaque.

## R\u00e9duction des draw calls
- Regrouper les petits meshes et cr\u00e9er des atlases de textures reste une priorit\u00e9.
- Privil\u00e9gier l'utilisation de mat\u00e9riaux partag\u00e9s et l'instancing pour les objets identiques.

## Level of Detail (LOD)
- Ajouter des `LODGroup` sur les gros mod\u00e8les permet de r\u00e9duire le nombre de polygones rendus au loin.

## Test de preuve
- Pour v\u00e9rifier le gain, dupliquer la sc\u00e8ne et retirer 80 % des objets opaques. Les FPS doivent augmenter sensiblement.
