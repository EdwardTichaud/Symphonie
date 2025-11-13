# Effet de veines pour le shader "Ash_Bark"

## Contexte narratif
Pour rester cohérent avec l'histoire décrite dans *Histoire de Symphonie*, l'écorce des frênes
présents dans les zones sanctifiées laisse désormais transparaître l'énergie vitale qui circule
à l'intérieur de l'arbre. Cet effet rappelle que ces arbres jouent un rôle clé dans la
propagation de l'harmonie sur Melodine et qu'ils sont intimement liés aux flux magiques qui
alimentent les instruments vivants.

## Résumé visuel
- Animation continue simulant un écoulement sanguin dans les veines de l'écorce.
- Pulsation lumineuse synchronisée avec un battement régulier pour suggérer une énergie
  organique.
- Couleur des veines et intensité émissive entièrement configurables afin d'adapter l'effet aux
  ambiances de scènes débutant ou avancées.

## Propriétés ajoutées
| Propriété | Type | Description |
|-----------|------|-------------|
| **Vein Mask** | Texture2D | Masque en niveaux de gris qui définit l'emplacement des veines. |
| **Vein UV Scale** | Vector2 | Permet d'ajuster l'échelle du masque pour s'adapter aux variations de densité de l'écorce. |
| **Vein Flow Direction** | Vector2 | Direction de déplacement des veines, par défaut vers l'axe V pour suivre la fibre de l'arbre. |
| **Vein Flow Speed** | Float | Vitesse du flux sanguin simulé ; valeurs négatives inversent le sens de déplacement. |
| **Vein Pulse Speed** | Float | Fréquence de la pulsation lumineuse. |
| **Vein Emission Strength** | Float | Intensité maximale de l'émission lorsque la pulsation est à son apogée. |
| **Vein Base Intensity** | Float | Intensité minimale maintenue entre deux pulsations pour conserver une lueur résiduelle. |
| **Vein Color** | Color | Couleur de l'émission des veines. |

## Schéma logique des nouveaux nœuds
1. **UV dynamique** :
   - Un nœud *UV* est scindé puis recombiné pour appliquer un facteur d'échelle.
   - Le produit du temps global et de *Vein Flow Speed* est multiplié par *Vein Flow Direction*
     puis additionné au résultat afin de paner le masque.
2. **Échantillonnage du masque** :
   - Le masque de veines est échantillonné avec l'UV animé pour obtenir un coefficient de
     présence par pixel.
3. **Pulsation** :
   - Le temps est multiplié par *Vein Pulse Speed* puis passé dans un nœud *Sine*.
   - Une constante `1.0` et une constante `0.5` normalisent la sortie entre 0 et 1.
   - Le résultat est amplifié par *Vein Emission Strength* puis rehaussé par *Vein Base Intensity*.
4. **Emission finale** :
   - L'intensité pulsée est multipliée par le masque (canal R) et par *Vein Color* avant d'être
     envoyée dans le slot `Emission` du bloc *SurfaceDescription*.

## Conseils d'utilisation
- Préparer un masque en niveaux de gris où les veines sont claires et l'écorce est sombre.
- Ajuster *Vein Flow Direction* pour suivre le sens des fibres du modèle 3D.
- Pour un rendu plus dramatique dans les scènes avancées, augmenter légèrement *Vein Base
  Intensity* et *Vein Emission Strength* afin de rappeler la tension narrative croissante.
- Laisser *Vein Pulse Speed* proche de 1 pour les zones calmes destinées aux débutants, puis
  accélérer la pulsation dans les situations de tension musicale.

## Impact sur le gameplay
Ce retour visuel permet d'informer les joueuses et joueurs sur la santé magique de l'arbre.
Combiné aux MusicalMoves orientés soin ou purification, l'effet de veines devient un indicateur
immédiat de réussite ou d'échec des combinaisons mises en place.
