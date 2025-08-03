# Hiérarchie des caméras

Ce document décrit la nouvelle organisation des quatre caméras utilisées dans le jeu. Chaque caméra possède un objet *Origin* servant de pivot et un objet *Cam* contenant le composant `Camera`. Cette structure homogène facilite la lecture et la maintenance.

## Caméra principale
- **MainCam_Origin**
  - Parent commun pour la caméra finale qui affiche à l'écran les différents `RenderTexture`.
- **MainCam_Cam** (`tag` : `MainCamera`)
  - Caméra affichée à l'écran.
  - Elle sélectionne dynamiquement la `RenderTexture` à présenter selon le contexte (monde, combat, versus).

## Caméra du monde
- **WorldCam_Origin**
  - Pivot de la caméra explorant l'overworld.
- **WorldCam_Cam** (`tag` : `WorldCamera`)
  - Rend vers `RT_WorldView`.
  - Contrôlée par `CameraController` pour suivre le joueur.

## Caméra de combat
- **BattleCamera_Origin**
  - Point de base pour les déplacements de la caméra de combat.
- **BattleCamera_Cam** (`tag` : `BattleCamera`)
  - Rend vers `RT_BattleView`.
  - Activée lors des confrontations sur le champ de bataille.

## Caméra Versus
- **VersusCamera_Origin**
  - Pivot de la caméra utilisée pour l'animation 2D de l'écran *Versus*.
- **VersusCamera_Cam** (`tag` : `VersusCamera`)
  - Rend vers `RT_VersusScreenView`.
  - Généralement désactivée en dehors des écrans de transition.

## Résumé des `RenderTexture`
| Caméra          | `RenderTexture`            | Usage principal                                    |
|-----------------|---------------------------|----------------------------------------------------|
| WorldCam_Cam    | `RT_WorldView`             | Exploration du monde                               |
| BattleCamera_Cam| `RT_BattleView`            | Scènes de combat                                   |
| VersusCamera_Cam| `RT_VersusScreenView`      | Animation de l'écran Versus                        |
| MainCam_Cam     | Affichage dynamique        | Affiche la texture pertinente à l'écran            |

> **Note :** la présence d'un `Origin` pour chaque caméra offre une base commune pour appliquer des animations ou des transitions sans perturber le composant `Camera`.

