# Rôles de caméras pour les MusicalMoves et Items

Ce document récapitule les rôles de caméras cinématiques à associer aux MusicalMoves et aux Items afin de conserver une mise en scène harmonieuse.

Chaque **MusicalMove** et chaque **Item** référence désormais un rôle de caméra cinématique plutôt qu'un nom explicite :
`preparingCameraRole`, `performingCameraRole` et `retreatCameraRole`.
Ces rôles correspondent aux plans définis par le nouveau rig (`MainMenuIdle`, `OverShoulderCasterToTarget`, `ClosePushCaster`, `TargetReaction`, `WideEstablish`, `ProjectileFlyby`, `Victory`).

Grâce à cette nomenclature, les joueuses et joueurs débutants bénéficient d'une lecture claire des transitions tandis que les vétérans peuvent orchestrer des combos spectaculaires parfaitement cadrés.
