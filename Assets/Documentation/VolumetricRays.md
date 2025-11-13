# Configuration des rayons volumétriques (HDRP)

Ce document reprend la **recette "2 minutes"** utilisée pour faire apparaître des rayons de lumière visibles dans la forêt.
Il peut servir de mémo rapide lors de la mise en place de nouvelles scènes.

## Pré‑requis
- Projet configuré avec le **High Definition Render Pipeline**.
- HDRP Asset : `Volumetrics` activé en qualité **Medium** ou **High**.

## Caméra
- Dans les *Frame Settings Overrides* :
  - `Volumetrics` **ON**
  - `Shadows` **ON**
  - `Atmospheric Scattering` **ON**

## Lumière directionnelle (soleil)
- Intensité : **60 000 – 100 000 Lux** (80 000 par défaut).
- `Volumetrics` activé, **Multiplier** entre **1.3** et **2.0**.
- `Shadow Dimmer` : **1** avec une résolution d'ombre ≥ **2048**.
- Oriente le soleil bas (10–20°) et regarde vers lui avec la caméra pour profiter du *forward scattering*.

## Volume global > Fog
- `State` : **Enabled**
- `Fog Attenuation Distance` : **70–120** (90 recommandé).
- `Volumetric Fog` : **ON**
- `Albedo` : très sombre (`#111` – `#222`).
- `Anisotropy` : **0.85 – 0.9**
- `Directional Lights Only` : **ON**
- `Denoising` : **Gaussian**

## Volume local de brume (clé)
- Ajouter un **GameObject > Volume > Local Volumetric Fog** entre le soleil et le sol.
- `Size` : environ **(10, 20, 10)** mètres.
- `Volumetric Fog Distance` : **10 – 15**
- `Anisotropy` : **0.9 – 0.95**
- `Blend Distance` : **2 – 4**
- Possibilité d'utiliser un *mask* 3D rayé pour des faisceaux plus nets.

## Feuillage et ombres
- Les matériaux de feuilles doivent **caster des ombres**.
- Si un shader est `Transparent`, cocher `Transparent Receives Shadows` et activer `Alpha Clipping`.
- Idéalement, utiliser un shader `Opaque + Alpha Clip` pour une découpe nette des rayons.

## Exposure
- Volume > `Exposure` : `Automatic` ou `Fixed` entre `0` et `+1 EV`.
- Si l'image semble voilée, réduire l'`Exposure` ou augmenter le `Fog Attenuation Distance`.

## Valeurs de départ qui fonctionnent
- **Fog global** : `Attenuation 90`, `Anisotropy 0.85`
- **Soleil** : `80 000 Lux`, `Volumetric Multiplier 1.5`, `Shadows 2048`
- **2x Local Volumetric Fog** : `Size (10,20,10)`, `Distance 12`, `Anisotropy 0.9`, `Blend 3`
- **Arbres** : `Alpha Clipping ON`, `Cast Shadows ON`, `Transparent Receives Shadows ON`

Ce guide s'appuie sur l'histoire et l'ambiance de *Symphonie* : une forêt dense où la lumière matinale perce la canopée. Ajustez les valeurs selon les besoins des niveaux tout en conservant une ambiance accueillante pour les débutants et riche en possibilités pour les joueurs expérimentés.
