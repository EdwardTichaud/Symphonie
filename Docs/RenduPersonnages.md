# Rendu des personnages CC5 dans Unity

Ce document récapitule les ajustements effectués pour rapprocher le rendu Unity de celui observé dans Character Creator 5 (CC5).

## Origine de l'écart visuel

* **Profils de diffusion manquants** : les matériaux de peau n'étaient pas associés à un profil de diffusion HDRP. Sans subsurface scattering cohérent, la peau devenait plus mate et cireuse que dans CC5.
* **Seuils de découpe des cheveux trop élevés** : plusieurs matériaux capillaires utilisaient une valeur d'`AlphaClip` à 0.666, éliminant une grande partie des mèches fines et produisant un aspect « crénelé » très visible sous l'éclairage neigeux de la scène Unity.
* **Spéculaire atténué sur les cheveux** : les paramètres de réflexion secondaire étaient trop faibles par rapport aux préréglages HQ fournis par l'Auto Setup, ce qui délavant les reflets et donnait l'impression d'une chevelure sans profondeur.

## Actions réalisées

1. **Création d'un profil de diffusion dédié** (`CharacterSkinDiffusionProfile.asset`) afin d'uniformiser le comportement du subsurface scattering sur toutes les matières corporelles.
2. **Référence explicite du profil de diffusion** dans les matériaux de peau (tête, corps, bras, jambes) pour garantir la même réponse lumineuse qu'en sortie CC5.
3. **Réalignement des paramètres capillaires** (AlphaClip, AlphaToMask, forces spéculaires, ombres) sur les valeurs recommandées des templates HQ fournis par Reallusion, pour restituer les mèches fines et les reflets doux attendus.

## Effets attendus

* Des volumes cutanés plus doux et translucides, notamment sur le visage et les mains.
* Une chevelure plus dense, sans effet de « peigne blanc » dans les zones éclairées de face.
* Une meilleure cohérence entre le rendu studio de CC5 et l'intégration HDRP Unity.

## Points de vigilance

* Le profil de diffusion doit être référencé dans les volumes HDRP utilisés par vos scènes (profil de volume principal ou volumes locaux) pour être disponible au rendu.
* Si d'autres personnages CC5 sont importés, répétez l'association du profil de diffusion et appliquez les mêmes réglages capillaires afin de conserver une cohérence visuelle.
