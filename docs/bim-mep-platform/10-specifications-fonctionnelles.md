# 14. Spécifications fonctionnelles détaillées

Convention : chaque exigence est identifiée `F-<domaine>-<n°>` pour être tracée dans les tests et la roadmap
(§12 priorisation).

## F-IMPORT — Import de plans

- **F-IMPORT-01** : L'utilisateur importe un PDF vectoriel ; le système détecte automatiquement l'échelle du
  plan (cartouche ou cotation) et propose une confirmation avant tout traitement géométrique.
- **F-IMPORT-02** : L'utilisateur importe un PDF scanné ; le système redresse l'image, la débruite, puis lance
  la même chaîne de reconnaissance que pour un PDF vectoriel, avec un indicateur de confiance visible par élément.
- **F-IMPORT-03** : L'utilisateur importe un DWG ; le système distingue les calques (murs, cotes, texte,
  réseaux existants) et propose un mapping calque → catégorie BIM, modifiable avant import.
- **F-IMPORT-04** : L'utilisateur importe un IFC (maquette architecte/structure) ; le système conserve les
  GUID IFC et permet une **resynchronisation** ultérieure (nouvel import du même IFC = mise à jour différentielle,
  pas de duplication).
- **F-IMPORT-05** : Après import, le système génère automatiquement niveaux, pièces (avec surface/volume) et
  volumes englobants, présentés dans un rapport de reconnaissance avant validation.
- **F-IMPORT-06** : Aucun élément importé par IA n'est intégré au modèle de production sans validation
  explicite de l'ingénieur (cf. §7.2.3).

## F-MODEL — Modélisation BIM

- **F-MODEL-01** : Créer/modifier des familles et types (paramètres personnalisables) pour toutes les
  catégories MEP listées en §5.
- **F-MODEL-02** : Chaque objet MEP possède un GUID IFC stable, des propriétés BIM et des paramètres
  personnalisables visibles dans un panneau de propriétés.
- **F-MODEL-03** : Modifier un paramètre de type ou d'occurrence déclenche un recalcul automatique de tous les
  éléments dépendants (raccords, connecteurs, réseau) — cf. §5.4.
- **F-MODEL-04** : Les connecteurs MEP se lient automatiquement par proximité/alignement (tolérance
  paramétrable), avec retour visuel de connexion valide/invalide.
- **F-MODEL-05** : Support natif des LOD 100 à 500 avec indicateur visuel du LOD courant par élément et
  contrôle de cohérence de LOD à l'export (cf. §5.7).

## F-ROUTE — Routage automatique

- **F-ROUTE-01** : Router automatiquement un tronçon entre deux connecteurs en évitant les obstacles déclarés
  (structure, autres réseaux, volumes réservés).
- **F-ROUTE-02** : Proposer plusieurs variantes de tracé avec comparatif (longueur, poids, perte de charge,
  nombre de raccords).
- **F-ROUTE-03** : Respecter les contraintes métier lors du routage : pente minimale (EU/EV/EP), hauteur libre
  sous plafond, distances de sécurité entre réseaux incompatibles, rayons de courbure minimum.
- **F-ROUTE-04** : Optimiser un réseau existant selon un critère choisi (poids, longueur, perte de charge) sans
  perdre les connexions validées manuellement (verrouillage de segments "figés").

## F-CLASH — Détection de conflits

- **F-CLASH-01** : Détecter les interférences géométriques dures (chevauchement de volumes) entre tous les
  éléments du modèle, tous corps d'état confondus (y compris importés en IFC).
- **F-CLASH-02** : Détecter les conflits de dégagement (clearance) selon des règles paramétrables par type de
  système (ex. 300 mm autour d'un tableau électrique).
- **F-CLASH-03** : Classer chaque conflit par sévérité (critique/majeur/mineur) et par type (dur/mou/dégagement).
- **F-CLASH-04** : Proposer une correction automatique (décalage, reprise de pente) avec **prévisualisation**
  et recalcul complet du réseau affecté avant application.
- **F-CLASH-05** : Exporter/importer les conflits au format BCF pour coordination avec les autres corps d'état.

## F-CALC — Calculs métier

- **F-CALC-01** (aéraulique) : calcul débit/vitesse/pertes de charge linéaires et singulières par tronçon et
  par réseau, avec alerte de dépassement de seuil de vitesse.
- **F-CALC-02** (hydraulique) : calcul vitesse, pertes linéaires/singulières, équilibrage de réseau.
- **F-CALC-03** (thermique) : calcul des déperditions (NF EN 12831), des besoins de chauffage/refroidissement,
  et des indicateurs de confort (EN 16798) par local.
- **F-CALC-04** (réglementaire) : intégration des exigences RE2020 pertinentes au dimensionnement CVC
  (paramètres d'entrée exposés, calcul réglementaire complet hors périmètre logiciel — interfaçage avec outils
  RE2020 dédiés via export de données).
- **F-CALC-05** : toute modification géométrique impactant un calcul déclenche un recalcul automatique et
  invalide visuellement les notes de calcul obsolètes tant qu'elles n'ont pas été régénérées.

## F-RENDER — Rendu et navigation 3D

- **F-RENDER-01** : Navigation fluide (orbite, panoramique, zoom) sur des maquettes de grande taille (>50 000
  éléments MEP) sans dégradation perceptible (cf. objectifs de performance §11).
- **F-RENDER-02** : Section box interactive, vue éclatée par système/réseau, isolation d'un réseau sélectionné.
- **F-RENDER-03** : Rendu PBR avec ombres temps réel activable/désactivable selon la puissance du poste.

## F-DRAW — Génération de plans

- **F-DRAW-01** : Générer automatiquement plans par niveau, coupes, isométriques de principe et synoptiques,
  mis à jour en temps réel après modification du modèle 3D.
- **F-DRAW-02** : Habillage automatique minimal (cotes de niveau, repères de réseau, légende de symboles) avec
  possibilité d'ajustement manuel non écrasé par les mises à jour automatiques (verrouillage d'annotations).

## F-TAKEOFF — Métrés

- **F-TAKEOFF-01** : Calculer automatiquement poids de gaine, surface de calorifuge, longueur de réseaux par
  type/diamètre/système.
- **F-TAKEOFF-02** : Générer des nomenclatures filtrables et exportables (Excel, CSV, PDF) avec regroupement
  par lot/système/niveau.

## F-IFC — Interopérabilité

- **F-IFC-01/02/03** : import/export/synchronisation IFC2x3, IFC4, IFC4.3 conformes au mapping §6.
- **F-IFC-04** : validation automatique de conformité avant export (STEP + vérification Psets minimaux).

## F-AI — Copilote IA

- **F-AI-01** : Exécuter une commande en langage naturel de placement d'équipement avec proposition de
  connexion réseau et dimensionnement des gaines/tuyauteries principales.
- **F-AI-02** : Exécuter une commande d'optimisation de réseau avec comparaison chiffrée de plusieurs variantes.
- **F-AI-03** : Toute action du copilote reste en mode "proposition" tant que l'ingénieur n'a pas validé
  explicitement (cf. §7.4).

## F-CATALOG — Bibliothèque fabricants

- **F-CATALOG-01** : Rechercher et insérer un équipement/composant depuis le catalogue fabricants avec ses
  propriétés BIM et sa courbe de performance.
- **F-CATALOG-02** : Télécharger et mettre à jour automatiquement les familles BIM depuis les packs fabricants
  (Daikin, VIM, France Air, Aldes, Systemair, TROX, Lindab, FlaktGroup).

## F-PHASE — Conception-réalisation / LOD

- **F-PHASE-01** : Associer un projet à une phase (APS/APD/PRO/EXE) qui contraint le LOD minimal attendu et les
  contrôles de cohérence appliqués.
- **F-PHASE-02** : Faire évoluer un même modèle d'un LOD à l'autre sans recréer les objets (cf. §5.7, état
  LOD400 verrouillage partiel).

## F-COLLAB — Collaboration

- **F-COLLAB-01** : Travail multi-utilisateur avec verrouillage optimiste par élément et détection de conflit
  explicite (cf. §4.5).
- **F-COLLAB-02** : Historique des révisions consultable et restaurable par élément.
