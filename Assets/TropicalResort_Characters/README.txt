TROPICAL RESORT - POSTAVY (22 modelov, OBJ, jeden zdieľaný materiál)
Hostia (12): Father, Mother, Boy, Girl (rodina) | YoungMan, YoungWoman (mladí turisti) | SeniorMan, SeniorWoman (seniori)
             BusinessMan, BusinessWoman (biznis klienti) | WealthyMan, WealthyWoman (bohatí hostia)
VIP (4):     Influencer, Celebrity, Investor, Politician
Personál (6): Receptionist, Chef, Housekeeper, Maintenance (údržbár), Gardener, Security
Každý model ~1 800-2 200 trojuholníkov, všetky používajú JEDEN materiál/textúru (Textures/Palette.png) -> dávkovanie (SRP Batcher / GPU instancing).
Mierka: 1 jednotka = 1 m (dospelý 1.58-1.84 m, deti 1.10-1.15 m), Y hore, postava hľadí na +Z, pivot na zemi v strede.
Časti OBJ (skupiny): Head, Torso, ArmL, ArmR, LegL, LegR, Prop (kufor, taška, barla...). Polohy kĺbov: Rigs/<Meno>.rig.txt.
Import: skopíruj priečinok do Assets/ a spusti Assets > Tropical Resort > Setup Characters.
Animácie (Scripts/CharacterAnimator.cs): Idle, Walk, Waiting (čakanie v rade), Happy, Unhappy, Wave; SetSatisfaction(0..1) mení stav podľa spokojnosti.
VIP osobnosti FR-37 (10): VIP_TravelVlogger, VIP_Photographer, VIP_FitnessInfluencer, VIP_FoodBlogger, VIP_DJ,
             VIP_Writer, VIP_FootballStar, VIP_CelebrityChef, VIP_MovieStar, VIP_PopStar
             Telá a kĺby sú z existujúcich postáv (rovnaké rigy), oblečenie prefarbené a doplnky v časti tela, ktorá ich nesie.
             Údaje (meno, profesia, podmienky, odmena): Assets/Resources/VIP/VipCatalog.asset, portréty: Resources/UI/VIP.
