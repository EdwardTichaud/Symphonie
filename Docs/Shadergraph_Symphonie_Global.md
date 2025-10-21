# Shadergraph Symphonie Global

## Emplacement et usage
Le shadergraph global `Shadergraph_Symphonie_Global` est situé dans `Assets/Shaders`. Il doit être le point de départ de tous les matériaux utilisés par les objets du jeu pour assurer une cohérence artistique et faciliter l’activation de l’effet de dissolution.

## Paramètres principaux
- **Dissolve Amount** : contrôle la progression de la dissolution. `0` conserve le matériau totalement opaque alors que `1` le rend invisible.
- **Dissolve Width** : définit l’épaisseur de la bordure de dissolution afin de lisser la transition.
- Les propriétés historiques du shader Bark HDRP (albédo, normales, détails de mousse, etc.) restent disponibles pour ajuster l’aspect de chaque matériau dérivé.

## Flux de travail recommandé
1. Créer un nouveau matériau via *Create → Material* puis sélectionner le shader `Shader Graphs/Shadergraph_Symphonie_Global`.
2. Ajuster les propriétés standards (couleur de base, normales, rugosité…) puis régler `Dissolve Amount` et `Dissolve Width` suivant le comportement désiré.
3. Pour des effets dynamiques, animer `Dissolve Amount` via script, timeline ou animation.
4. Utiliser des textures personnalisées pour le canal de bruit si un motif particulier de dissolution est souhaité.

## Notes techniques
- Le bruit de dissolution utilise l’échantillon `Vein Flow Sample` issu du graphique original Bark HDRP, garantissant une compatibilité totale avec les assets existants.
- Le shader est configuré en mode Transparent (Alpha) afin de permettre la disparition progressive des objets.
- Les matériaux existants ont été redirigés pour utiliser automatiquement ce shadergraph, mais un passage dans l’éditeur est recommandé pour ajuster les valeurs selon chaque asset.

## Validation
- Ouvrir le shadergraph dans Unity pour vérifier la preview.
- Tester un matériau dérivé dans une scène afin de confirmer la transition opaque → dissolution → invisible.

