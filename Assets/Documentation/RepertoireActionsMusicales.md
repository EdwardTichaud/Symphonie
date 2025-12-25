# Répertoire des MusicalMoves jouables

Ce document synthétise les effets de base des MusicalMoves accessibles aux héros en s'appuyant sur le contexte narratif de `Docs/HistoireSymphonie.md` pour guider l'apprentissage et la maîtrise.

## Système de Sceaux modulaires pour l’accessibilité

> **Commentaire de conception** : ce système a été validé avec les chroniqueurs de `Docs/HistoireSymphonie.md` afin de respecter l’ascension progressive de la Fratrie décrite dans le récit.

- **Principe général** : les Sceaux sont des artefacts narratifs forgés par Munin pour moduler l’intensité des affrontements. On peut en équiper jusqu’à dix avant un combat pour ajuster la difficulté sans modifier le comportement des MusicalMoves ni des Items.
- **Équilibrage du score** : chaque Sceau appliqué ajuste cumulativement le score de fin de combat et l’XP obtenue. Les Sceaux de facilitation appliquent une décote (généralement –10 %), tandis que les Sceaux de défi octroient une prime (généralement +10 %) pour récompenser les virtuoses.
- **Compatibilité** : les Sceaux n’ajoutent aucune nouvelle ressource ; ils améliorent temporairement le personnage choisi, quel que soit son rôle, par des bonus passifs qui se déclenchent automatiquement dès que le combat commence.
- **Équipement** : les Sceaux ne sont pas pré-équipés. Ils se sélectionnent dans l’interface d’inventaire durant l’exploration, puis restent actifs jusqu’à ce que le joueur les retire manuellement.
- **Vérification** : avant chaque combat, le moteur vérifie les Sceaux actuellement équipés et applique leurs effets ainsi que leurs modificateurs de score.
- **Retrait manuel** : il est possible de retirer tous les Sceaux en un clic depuis l’onglet de préparation afin que la difficulté reste intacte pour les vétérans.

### Catalogue de Sceaux recommandés

Afin de faciliter la sélection, les Sceaux sont regroupés en deux familles : les modèles universels utilisables dans toutes les
rencontres et les variantes spécifiques optimisées pour des situations récurrentes (types d’ennemis, environnements ou altérations).

#### Sceaux universels

| Nom du Sceau | Effet facilité | Commentaire d’utilisation | Impact sur le score |
|--------------|----------------|----------------------------|---------------------|
| **Sceau de Cadence Stable** | Réduit tous les cooldowns de 1 tour (minimum 1). | Maintient un tempo constant pour enchaîner *Tunnel*, *Staccato Étourdissant* et les ultimes offensifs. | -10 % |
| **Sceau de Main Guidée** | +25 % dégâts sur les attaques basiques seulement. | Sert de socle à des builds centrés sur *Rhapsodie* ou *Serpenteau Mélodique* avant d’embrayer sur les combos lourds. | -10 % |
| **Sceau de Fatigue Allégée** | Réduit de 1 le coût en Fatigue des MusicalMoves (minimum 0) pour le premier tour uniquement. | Offre une ouverture souple aux stratégies qui demandent plusieurs buffs immédiats. | -10 % |
| **Sceau du Rempart Arcanique** | Octroie un bouclier de 6 000 PV et dissipe 1 altération au début du combat. | Sécurise l’entrée en scène face aux Séraphins focalisés sur les malédictions. | -10 % |
| **Sceau de l’Avant-Scène** | +150 Initiative et posture de garde automatique au premier tour. | Garantit la priorité au meneur choisi pour placer *Pour qui sonne le glas* avant la riposte ennemie. | -10 % |
| **Sceau du Cœur Accordé** | +30 % soins reçus et +10 % régénération d’Harmonie pendant 3 tours. | Amplifie *Arpège Régénérant* et consolide les duos axés sur le soutien mélodique. | -10 % |
| **Sceau de l’Écho Suspendu** | La première attaque de zone ennemie inflige -40 % dégâts et ne peut pas repousser. | Protège les lignes en préparation d’un *Crescendo Fulgurant*. | -10 % |
| **Sceau de Pulsation Constante** | +1 charge de Tempo et +300 Initiative au groupe au tour 1. | Aligne les timings pour lancer un assaut coordonné dès l’ouverture. | -10 % |
| **Sceau de l’Onde Persévérante** | À 50 % PV, déclenche une vague de soin de 12 000 PV sur le porteur (1 fois par combat). | Soutient les tanks lors des duels prolongés contre Azazel. | -10 % |
| **Sceau de l’Impulsion Franche** | Les trois premiers MusicalMoves lancés coûtent -1 Harmonie (minimum 0). | Accélère les ouvertures agressives basées sur *Sforzando* et *Crescendo Fulgurant*. | -15 % |
| **Sceau des Cordes Stoïques** | +40 % durée des buffs défensifs du porteur et +10 % résistances globales. | Prolonge *Accord Bienveillant* et les runes défensives avancées. | -10 % |
| **Sceau de la Rumeur Lucide** | Révèle l’intention de la cible principale pendant 2 tours et +15 % chances de critique contre elle. | Optimise les fenêtres d’exécution pour *Rupture Harmonieuse*. | -10 % |
| **Sceau des Fragments Calmes** | Convertit jusqu’à 2 fragments de mélodie en un voile de 4 000 PV chacun au début du combat. | Protège les stratèges qui préparent un *Rappel des Fragments* tardif. | -10 % |
| **Sceau du Cercle Harmonisé** | Crée au tour 1 une zone qui réduit de 15 % les dégâts reçus par les alliés adjacents (2 tours). | Idéal pour verrouiller les positions autour du soutien principal. | -10 % |
| **Sceau du Silence Stratège** | La première incantation ennemie est retardée d’un tour et inflige -20 % dégâts. | Ouvre un créneau pour interrompre les chorales de Séraphins. | -10 % |

#### Sceaux de défi (universels)

| Nom du Sceau | Effet de défi | Commentaire d’utilisation | Impact sur le score |
|--------------|---------------|----------------------------|---------------------|
| **Sceau de Lame Déchaînée** | +25 % dégâts subis par le porteur et +20 % dégâts infligés. | Transforme le porteur en glass-cannon assumé pour les combats éclairs. | +10 % |
| **Sceau de Silence Absolu** | Désactive les soins externes sur le porteur mais +40 % dégâts sur les MusicalMoves offensifs. | À réserver aux runs maîtrisés où l’équipe enchaîne les éliminations rapides. | +12 % |
| **Sceau d’Harmonie Frêle** | -30 % PV max pour tous les alliés mais +50 % régénération d’Harmonie. | Favorise les compositions axées sur les combos incessants. | +15 % |
| **Sceau de la Mesure Inflexible** | Les cooldowns ne peuvent plus être réduits mais chaque coup critique génère +1 Harmonie. | Encourage la précision rythmique tout en supprimant les sécurités. | +10 % |
| **Sceau de Virtuosité Soliste** | Le porteur ne peut pas recevoir de buffs alliés mais gagne +35 % Initiative et +15 % critique. | Test ultime de pilotage individuel lors des duels prolongés contre les menaces majeures. | +15 % |

#### Sceaux spécifiques

| Nom du Sceau | Effet facilité | Commentaire d’utilisation | Impact sur le score |
|--------------|----------------|----------------------------|---------------------|
| **Sceau de Murmure Protecteur** | +20 % PV max et +15 % résistances harmoniques. | Conçu pour les duels d’endurance face aux prédateurs empathiques ou aux gardiens drainants qui pressent l’équipe sur la durée. | -10 % |
| **Sceau de Résonance Apaisée** | Réduit de 50 % les dégâts de Résonance et Dissonance. | Neutralise les débordements harmoniques typiques des chorales séraphiques ou des avatars d’Azazel lors des phases critiques. | -10 % |
| **Sceau de Vigilance Nocturne** | +25 % résistance aux altérations mentales et vision des entités furtives pendant 2 tours. | Indispensable dans les zones saturées d’illusions ou face aux assassins oniriques évoqués dans `Docs/HistoireSymphonie.md`. | -10 % |
| **Sceau de la Harpe Solaire** | +20 % dégâts contre les créatures du Néant et lumière persistante annulant la Peur. | S’emploie dès qu’une expédition plonge dans les couloirs obscurs de la Convocation montante infestés de créatures du Néant. | -15 % |
| **Sceau de Gravité Mesurée** | Réduit de 60 % les effets de poussée/traction subis et -25 % dégâts de chute. | Essentiel dans toutes les arènes instables où les Séraphins ou les anomalies gravitationnelles bousculent les trajectoires. | -10 % |

> **Note d’équilibrage** : l’équipement simultané de plusieurs Sceaux cumule leurs modificateurs de score (ex. deux Sceaux à –10 % et un Sceau à –15 % = –35 %, deux Sceaux de défi à +10 % et +15 % = +25 %). Cela garantit que le mode « difficile » reste l’option naturelle pour les experts qui souhaitent optimiser leur progression, tout en offrant une voie héroïque aux virtuoses.

| Nom | Type | Coûts (Fatigue / Harmonie) | Effet de base | Combinaisons & Conseils |
|-----|------|----------------------------|---------------|-------------------------|
| **Rhapsodie** | Attaque | 0 / 0 | ~7 500 dégâts + puissance actuelle du lanceur. | Démarre les enchaînements en accumulant des fragments avant de déclencher un combo majeur. |
| **Éclair** | Attaque | 1 / 1 | ~11 000 dégâts instantanés. | À lier avec la *Clé du Courage* ou des runes de puissance pour éliminer les menaces rapides. |
| **Contrepoint Chaotique** | Attaque de zone | 1 / 3 | ~13 000 dégâts à chaque ennemi. | Prépare des balayages massifs avant un *Crescendo Fulgurant* pour achever les survivants. |
| **Tunnel** | Contrôle | 1 / 1 | Appose le glyphe Presto : la cible déclenche une attaque basique après chaque tour jusqu’au retour du lanceur. | Parfait pour accélérer un allié clef ou forcer un ennemi contrôlé à se retourner contre ses pairs avant la prochaine salve du lanceur. |
| **Fausse Note** | Altération | 1 / 1 | Réveille toutes les unités alliées. | Annule les effets soporifiques des Entités du Rêve ou de l’*Endormissement*. |
| **Accord Bienveillant** | Soutien | 1 / 1 | Soigne ~10 000 PV et +400 Défense (2 tours). | Ancre parfaite pour les runes défensives lorsque les assauts de l’Ange Pleureur se déchaînent. |
| **Rupture Harmonieuse** | Affaiblissement | 1 / 2 | ~9 500 dégâts et -400 Défense sur l’ennemi (2 tours). | Lancez-la avant *Crescendo Fulgurant* ou *Sforzando* pour maximiser les dégâts critiques. |
| **Pour qui sonne le glas** | Protection | 1 / 1 | Appose une marque de loyauté : la cible alliée ignore les dégâts subis tandis que le lanceur encaisse la moitié à sa place. | Déclenche un remerciement vocal du protégé ; combinez-la avec les postures défensives de Lucian pour temporiser les assauts majeurs. |
| **Marque** | Tactique | 1 / 1 | Appose une marque de lien sur l’ennemi. | Sert de point d’ancrage pour les combos de Kael ou les runes d’exécution. |
| **Hate** | Placement | 1 / 0 | Lucian se place devant la cible pour absorber les attaques. | À utiliser juste avant *Sforzando* pour rester au contact de l’adversaire. |
| **Pont Harmonique** | Altération | 1 / 1 | Ramène une cible volante au sol pour 2 tours. | Indispensable contre les entités en suspension avant d’enchaîner les attaques terrestres. |
| **Staccato Étourdissant** | Contrôle | 2 / 2 | Endort un ennemi pendant 1 tour. | À coupler avec des runes de contrôle pour verrouiller un boss pendant la préparation d’une offensive majeure. |
| **Sforzando** | Attaque téléportée | 1 / 1 | ~20 000 dégâts, génère +1 Harmonie. | Offre un repositionnement agressif idéal pour capitaliser sur la marque de loyauté. |
| **Crescendo Fulgurant** | Attaque | 2 / 2 | ~30 000 dégâts concentrés. | L’ultime exécution de Lucian, à déclencher après une *Rupture Harmonieuse*. |
| **Arpège Régénérant** | Soutien | 2 / 2 | ~20 000 PV à toute l’équipe. | Associé aux runes de soin, il permet de soutenir des combats prolongés contre les Séraphins. |
| **Abîme Harmonique** | Altération | 1 / 1 | Force la cible à flotter pendant 2 tours. | Prépare les stratégies aériennes de Luna et contrebalance les défenses terrestres. |
| **Battement d’Ouverture** | Soutien | 0 / 1 | Confère +200 Initiative à un allié et réduit sa Fatigue de 1. | Assure la priorité tactique à Kael ou Luna pour enclencher les séquences critiques du tour suivant. |
| **Serpenteau Mélodique** | Attaque | 1 / 0 | 6 000 dégâts et appose un fragment de mélodie cumulable (max 3). | Facile à manier : chaque fragment peut être converti plus tard en +5 % dégâts pour *Rhapsodie* ou *Éclair*. |
| **Cantate des Profondeurs** | Contrôle | 2 / 3 | Étourdit une cible immergée ou instable, sinon inflige 12 000 dégâts et -2 Harmonie. | Synergie tardive avec les altérations d’*Abîme Harmonique* et les Items aqueux décrits dans `Docs/RepertoireItemsUtilisables.md`. |
| **Polyrythmie Fractale** | Attaque de zone | 3 / 4 | 18 000 dégâts + 2 000 par effet sonore actif sur chaque ennemi. | Mécanique avancée : combinez les échos des Séraphins (voir `Docs/HistoireSymphonie.md`) avec les runes de résonance pour déchaîner une salve dévastatrice. |
| **Pulse Initiatique** | Soutien | 0 / 0 | +600 PV instantanés et +1 charge de Tempo protectrice (max 2) pour le lanceur. | Déploie un filet de sécurité avant une offensive sans consommer de ressources rares. |
| **Boucle de Veille** | Protection | 0 / 1 | Confère un voile absorbant 8 000 dégâts et annule la prochaine altération négative. | Couplée aux talismans de Munin, elle amortit les premiers chocs face aux Échos perdus. |
| **Glissando Apaisant** | Soutien | 1 / 0 | Rend 12 000 PV à une cible et dissipe la Peur. | S’utilise dès les premières confrontations contre l’Ange Pleureur pour maintenir Lucian debout malgré les chocs émotionnels. |
| **Aube du Métal** | Attaque | 1 / 0 | 9 000 dégâts et +10 % aux dégâts physiques du lanceur (2 tours). | Permet aux fans de riffs soutenus de ressentir immédiatement la pulsation héroïque ; associez-la à *Rhapsodie* pour un finish rapide. |
| **Pas de Brume** | Placement | 1 / 0 | Téléporte l’allié ciblé sur la case adjacente libre la plus proche. | Clarifie les trajectoires : repositionnez Munin-caméra sans risquer la ligne de vue des Séraphins. |
| **Oscillation Empathique** | Soutien | 1 / 1 | Copie le buff principal d’un allié sur Lucian pour 2 tours. | Introduit doucement la gestion de buffs : doublez *Accord Bienveillant* pour résister aux vagues d’Azazel. |
| **Cadre de Silence** | Contrôle | 1 / 1 | Interrompt la prochaine incantation ennemie et inflige -200 Initiative. | Très accessible : déclenchez-le quand un Séraphin prépare une bénédiction, puis enchaînez avec *Éclair*. |
| **Rumeur de la Branche** | Tactique | 1 / 1 | Révèle les points faibles d’un ennemi, offrant +15 % chances de coup critique à l’équipe (1 tour). | Outil de scouting précieux pour cartographier les branches du Rêve décrites dans `Docs/HistoireSymphonie.md`. |
| **Lame de Lune** | Attaque téléportée | 1 / 2 | 16 000 dégâts et applique Saignée Astrale (4 000 dégâts/tour pendant 2 tours). | À utiliser juste après *Hate* pour que Lucian bondisse et reste au contact, tout en alimentant les combos nocturnes de Luna. |
| **Beat Souterrain** | Attaque de zone | 2 / 1 | 11 000 dégâts aux ennemis souterrains ou instables, sinon 7 000 dégâts et -1 Harmonie. | Synergie intermédiaire avec les altérations de terrain décrites dans `Docs/ConditionsAltitude.md`. |
| **Cadence du Traqueur** | Attaque | 2 / 2 | 14 000 dégâts et convertit 1 buff positif de la cible en Fragment Obscur utilisable par l’équipe. | Permet d’apprivoiser l’héritage du Traqueur sans sombrer : stockez les fragments pour alimenter *Crescendo Fulgurant*. |
| **Réplique de Munin** | Soutien | 2 / 2 | Stocke la prochaine action d’un allié, puis la rejoue automatiquement au tour suivant sans coût. | Débloque des plans avancés : dupliquez un soin critique ou un *Sforzando* pour surprendre Azazel. |
| **Anacrouse Décisive** | Altération | 2 / 2 | Remplit la jauge ATB de la cible à 100 % pour qu’elle joue juste après le tour du lanceur. | Faire jouer un ennemi plus tôt peut briser la chronologie adverse : par exemple faire agir un boss avant ses sbires l’empêche d’être buff avant d’attaquer. |
| **Syncope d’Azazel** | Affaiblissement | 2 / 3 | 13 000 dégâts, -300 Défense et génère une dissonance qui empêche la régénération (2 tours). | Arme de mid-game contre les clones d’Azazel : combinez-la avec les Items de rupture listés dans `Docs/RepertoireItemsUtilisables.md`. |
| **Conque Résiliente** | Protection | 2 / 2 | Confère un renvoi de 50 % des dégâts subis au corps à corps pendant 1 tour. | Idéal pour encaisser les charges des Séraphins aveuglés, surtout après *Pour qui sonne le glas*. |
| **Rappel des Fragments** | Soutien | 2 / 2 | Consomme tous les Fragments de mélodie pour rendre 4 000 PV et +1 Harmonie par fragment. | Excellent point de bascule entre early et mid-game : convertissez les stacks de *Serpenteau Mélodique* en burst de ressources. |
| **Vibration de la Source** | Attaque de zone | 3 / 3 | 15 000 dégâts et applique Résonance (cible subit +5 % dégâts harmoniques cumulable 3 fois). | Prépare les assauts finaux avant la Convocation ; alternez avec *Polyrythmie Fractale* pour saturer les défenses. |
| **Flux de Légitimité** | Soutien | 3 / 3 | +2 Harmonie à l’équipe et purification des malédictions. | À réserver pour les moments où la Convocation faiblit : maintient la cohésion contre les vagues d’Azazel. |
| **Contrechant du Refuge** | Protection | 3 / 4 | Crée une zone réduisant de 30 % les dégâts subis par les alliés et infligeant 6 000 dégâts à l’entrée des ennemis (2 tours). | Combo de late game : verrouillez un sanctuaire pendant que Munin compile les archives oniriques. |
| **Frappe en Polychronie** | Attaque | 3 / 4 | 20 000 dégâts et si la cible est marquée, déclenche toutes les marques pour +4 000 dégâts chacune. | Mécanique avancée : synchronisez les marques de Kael et l’*Oscillation Empathique* pour une explosion finale. |
| **Finale de la Fratrie** | Ultime | 4 / 5 | 28 000 dégâts à une cible et 10 000 soins aux alliés, puis +1 Harmonie durable. | Climax narratif : à employer lorsque Munin soutient Lucian contre Azazel, offrant une conclusion émotive et stratégique. |

En maîtrisant cette liste, chaque chef d’orchestre dispose d'un guide clair tandis que les virtuoses peuvent planifier des suites d'accords fidèles à la légende de Symphonie.


### Implémentation Unity des nouvelles actions

| Nom | Asset Unity | Fatigue / Harmonie | Effet principal | Notes d'utilisation |
|-----|-------------|--------------------|-----------------|---------------------|
| Battement d’Ouverture | `Assets/MusicalMoves/MusicalMove_BattementDouverture_Lumiere/MusicalMove_BattementDouverture_Lumiere.asset` | 0 / 1 | Buff Initiative +200, réduction de Fatigue (1 tour) | Prépare les ouvertures de Kael ou Luna avant un tour explosif. |
| Serpenteau Mélodique | `Assets/MusicalMoves/MusicalMove_SerpenteauMelodique_Lumiere/MusicalMove_SerpenteauMelodique_Lumiere.asset` | 1 / 0 | ~6 000 dégâts + applique un Fragment de mélodie | Accumule les Fragments pour Rappel des Fragments ou Frappe en Polychronie. |
| Cantate des Profondeurs | `Assets/MusicalMoves/MusicalMove_CantateDesProfondeurs_Lumiere/MusicalMove_CantateDesProfondeurs_Lumiere.asset` | 2 / 3 | ~12 000 dégâts (étourdit les cibles immergées) | Synergie directe avec Abîme Harmonique pour verrouiller une cible. |
| Polyrythmie Fractale | `Assets/MusicalMoves/MusicalMove_PolyrythmieFractale_Lumiere/MusicalMove_PolyrythmieFractale_Lumiere.asset` | 3 / 4 | AOE ~18 000 dégâts +2 000 par effet sonore | Clôture les combos après Vibration de la Source. |
| Pulse Initiatique | `Assets/MusicalMoves/MusicalMove_PulseInitiatique_Lumiere/MusicalMove_PulseInitiatique_Lumiere.asset` | 0 / 0 | Auto-soin 600 PV + charge de Tempo | Sécurise le lanceur avant une séquence à risque. |
| Boucle de Veille | `Assets/MusicalMoves/MusicalMove_BoucleDeVeille_Lumiere/MusicalMove_BoucleDeVeille_Lumiere.asset` | 0 / 1 | Voile absorbant 8 000 dégâts + annule la prochaine altération | Protège un allié clé avant l’arrivée des malédictions séraphiques. |
| Glissando Apaisant | `Assets/MusicalMoves/MusicalMove_GlissandoApaisant_Lumiere/MusicalMove_GlissandoApaisant_Lumiere.asset` | 1 / 0 | ~12 000 soins + dissipe la Peur | Incontournable face aux assauts émotionnels de l’Ange Pleureur. |
| Aube du Métal | `Assets/MusicalMoves/MusicalMove_AubeDuMetal_Lumiere/MusicalMove_AubeDuMetal_Lumiere.asset` | 1 / 0 | ~9 000 dégâts +10 % puissance physique (2 tours) | Lance les combos physiques décrits dans HistoireSymphonie. |
| Pas de Brume | `Assets/MusicalMoves/MusicalMove_PasDeBrume_Lumiere/MusicalMove_PasDeBrume_Lumiere.asset` | 1 / 0 | Téléporte l’allié ciblé sur une case adjacente | Repositionne Munin sans déclencher d’interception. |
| Oscillation Empathique | `Assets/MusicalMoves/MusicalMove_OscillationEmpathique_Lumiere/MusicalMove_OscillationEmpathique_Lumiere.asset` | 1 / 1 | Copie le buff majeur d’un allié sur Lucian (2 tours) | Double Accord Bienveillant avant un pic de dégâts. |
| Cadre de Silence | `Assets/MusicalMoves/MusicalMove_CadreDeSilence_Lumiere/MusicalMove_CadreDeSilence_Lumiere.asset` | 1 / 1 | Interruption + -200 Initiative (1 tour) | À déclencher lorsque les Séraphins préparent une bénédiction. |
| Rumeur de la Branche | `Assets/MusicalMoves/MusicalMove_RumeurDeLaBranche_Lumiere/MusicalMove_RumeurDeLaBranche_Lumiere.asset` | 1 / 1 | Dévoile la cible et +15 % critique d’équipe | Optimise Frappe en Polychronie contre les lieutenants du Rêve. |
| Lame de Lune | `Assets/MusicalMoves/MusicalMove_LameDeLune_Lumiere/MusicalMove_LameDeLune_Lumiere.asset` | 1 / 2 | ~16 000 dégâts + Saignée astrale | Synergie avec Hate pour rester au contact en mêlée. |
| Beat Souterrain | `Assets/MusicalMoves/MusicalMove_BeatSouterrain_Lumiere/MusicalMove_BeatSouterrain_Lumiere.asset` | 2 / 1 | AOE ~11 000 dégâts (7 000 si non souterrain) | Couvre les anomalies de terrain détaillées dans Docs/ConditionsAltitude.md. |
| Cadence du Traqueur | `Assets/MusicalMoves/MusicalMove_CadenceDuTraqueur_Lumiere/MusicalMove_CadenceDuTraqueur_Lumiere.asset` | 2 / 2 | ~14 000 dégâts + convertit un buff en Fragment Obscur | Prépare les explosions de Crescendo Fulgurant. |
| Réplique de Munin | `Assets/MusicalMoves/MusicalMove_RepliqueDeMunin_Lumiere/MusicalMove_RepliqueDeMunin_Lumiere.asset` | 2 / 2 | Stocke l’action d’un allié pour le tour suivant | Duplique un soin critique ou un Sforzando surprise. |
| Anacrouse Décisive | `Assets/MusicalMoves/MusicalMove_AnacrouseDecisive_Lumiere/MusicalMove_AnacrouseDecisive_Lumiere.asset` | 2 / 2 | ATB cible à 100 % | Faire jouer un ennemi plus tôt peut briser la chronologie adverse : faire agir un boss avant ses sbires évite qu’il soit buff avant d’attaquer. |
| Syncope d’Azazel | `Assets/MusicalMoves/MusicalMove_SyncopeDAzazel_Lumiere/MusicalMove_SyncopeDAzazel_Lumiere.asset` | 2 / 3 | ~13 000 dégâts et -300 Défense (2 tours) | Neutralise les clones d’Azazel en empêchant la régénération. |
| Conque Résiliente | `Assets/MusicalMoves/MusicalMove_ConqueResiliente_Lumiere/MusicalMove_ConqueResiliente_Lumiere.asset` | 2 / 2 | Renvoi 50 % dégâts mêlée (1 tour) | Combinez avec Pour qui sonne le glas pour tenir la ligne. |
| Rappel des Fragments | `Assets/MusicalMoves/MusicalMove_RappelDesFragments_Lumiere/MusicalMove_RappelDesFragments_Lumiere.asset` | 2 / 2 | Convertit les Fragments en 4 000 PV +1 Harmonie chacun | Point de bascule entre early et mid-game. |
| Vibration de la Source | `Assets/MusicalMoves/MusicalMove_VibrationDeLaSource_Lumiere/MusicalMove_VibrationDeLaSource_Lumiere.asset` | 3 / 3 | AOE ~15 000 dégâts + Résonance cumulable | Prépare Polyrythmie Fractale pour saturer les défenses. |
| Flux de Légitimité | `Assets/MusicalMoves/MusicalMove_FluxDeLegitimite_Lumiere/MusicalMove_FluxDeLegitimite_Lumiere.asset` | 3 / 3 | +2 Harmonie d’équipe et purification | À garder pour les phases critiques de la Convocation. |
| Contrechant du Refuge | `Assets/MusicalMoves/MusicalMove_ContrechantDuRefuge_Lumiere/MusicalMove_ContrechantDuRefuge_Lumiere.asset` | 3 / 4 | Zone -30 % dégâts subis + 6 000 dégâts à l’entrée | Verrouille un sanctuaire comme décrit dans HistoireSymphonie. |
| Frappe en Polychronie | `Assets/MusicalMoves/MusicalMove_FrappeEnPolychronie_Lumiere/MusicalMove_FrappeEnPolychronie_Lumiere.asset` | 3 / 4 | ~20 000 dégâts + déclenche les Marques | Finisher idéal après les combos de Kael. |
| Finale de la Fratrie | `Assets/MusicalMoves/MusicalMove_FinaleDeLaFratrie_Lumiere/MusicalMove_FinaleDeLaFratrie_Lumiere.asset` | 4 / 5 | ~28 000 dégâts, soigne 10 000 PV aux alliés, +1 Harmonie | Climax narratif lors des duels contre Azazel. |

### ScriptableObjects Sceaux

Les Sceaux décrits en début de document sont désormais disponibles sous `Assets/Sceaux` et utilisent le script `Assets/Scripts/Classes/SceauSO.cs`. Le tableau ci-dessous récapitule les instances fournies :

| Nom | Asset Unity | Famille | Modificateur de score |
|-----|-------------|---------|-----------------------|
| Sceau de Cadence Stable | `Assets/Sceaux/Universels/Sceau_CadenceStable.asset` | Universel | -10 % |
| Sceau de Main Guidée | `Assets/Sceaux/Universels/Sceau_MainGuidee.asset` | Universel | -10 % |
| Sceau de Fatigue Allégée | `Assets/Sceaux/Universels/Sceau_FatigueAllegee.asset` | Universel | -10 % |
| Sceau du Rempart Arcanique | `Assets/Sceaux/Universels/Sceau_RempartArcanique.asset` | Universel | -10 % |
| Sceau de l’Avant-Scène | `Assets/Sceaux/Universels/Sceau_AvantScene.asset` | Universel | -10 % |
| Sceau du Cœur Accordé | `Assets/Sceaux/Universels/Sceau_CoeurAccorde.asset` | Universel | -10 % |
| Sceau de l’Écho Suspendu | `Assets/Sceaux/Universels/Sceau_EchoSuspendu.asset` | Universel | -10 % |
| Sceau de Pulsation Constante | `Assets/Sceaux/Universels/Sceau_PulsationConstante.asset` | Universel | -10 % |
| Sceau de l’Onde Persévérante | `Assets/Sceaux/Universels/Sceau_OndePerseverante.asset` | Universel | -10 % |
| Sceau de l’Impulsion Franche | `Assets/Sceaux/Universels/Sceau_ImpulsionFranche.asset` | Universel | -15 % |
| Sceau des Cordes Stoïques | `Assets/Sceaux/Universels/Sceau_CordesStoiques.asset` | Universel | -10 % |
| Sceau de la Rumeur Lucide | `Assets/Sceaux/Universels/Sceau_RumeurLucide.asset` | Universel | -10 % |
| Sceau des Fragments Calmes | `Assets/Sceaux/Universels/Sceau_FragmentsCalmes.asset` | Universel | -10 % |
| Sceau du Cercle Harmonisé | `Assets/Sceaux/Universels/Sceau_CercleHarmonise.asset` | Universel | -10 % |
| Sceau du Silence Stratège | `Assets/Sceaux/Universels/Sceau_SilenceStratege.asset` | Universel | -10 % |
| Sceau de Lame Déchaînée | `Assets/Sceaux/Defi/Sceau_LameDechainee.asset` | Défi | +10 % |
| Sceau de Silence Absolu | `Assets/Sceaux/Defi/Sceau_SilenceAbsolu.asset` | Défi | +12 % |
| Sceau d’Harmonie Frêle | `Assets/Sceaux/Defi/Sceau_HarmonieFrele.asset` | Défi | +15 % |
| Sceau de la Mesure Inflexible | `Assets/Sceaux/Defi/Sceau_MesureInflexible.asset` | Défi | +10 % |
| Sceau de Virtuosité Soliste | `Assets/Sceaux/Defi/Sceau_VirtuositeSoliste.asset` | Défi | +15 % |
| Sceau de Murmure Protecteur | `Assets/Sceaux/Specifiques/Sceau_MurmureProtecteur.asset` | Spécifique | -10 % |
| Sceau de Résonance Apaisée | `Assets/Sceaux/Specifiques/Sceau_ResonanceApaisee.asset` | Spécifique | -10 % |
| Sceau de Vigilance Nocturne | `Assets/Sceaux/Specifiques/Sceau_VigilanceNocturne.asset` | Spécifique | -10 % |
| Sceau de la Harpe Solaire | `Assets/Sceaux/Specifiques/Sceau_HarpeSolaire.asset` | Spécifique | -15 % |
| Sceau de Gravité Mesurée | `Assets/Sceaux/Specifiques/Sceau_GraviteMesuree.asset` | Spécifique | -10 % |
