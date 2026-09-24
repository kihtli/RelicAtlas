"""Build the checked-in catalogue from factual reference tables and authored instructions.
Reference HTML is a build-time input in /tmp/relic-*.html, never fetched by the plugin.
"""
import json, re
from pathlib import Path
from bs4 import BeautifulSoup
ROOT=Path(__file__).resolve().parents[1]
URL='https://ffxiv.consolegameswiki.com/wiki/'
def soup(page): return BeautifulSoup(Path('/tmp/relic-'+page.replace('/','-')+'.html').read_text(),'html.parser')
def slug(s): return re.sub(r'[^\w]+','-',s.lower()).strip('-')
def req(label,count=1,detail='',item='',group='',shared='',quest='',jobs=None,hq=False):
 return dict(Id=slug(group+'-'+label),Label=label,Count=count,Detail=detail,Item=item,Group=group,Shared=shared,Quest=quest,Jobs=jobs or [],Hq=hq)
def material(name,n,detail='',**kw): return req(name,n,detail,item=name,**kw)
def gate(quest,detail='',**kw): return req(quest,detail=detail,shared=slug(quest),quest=quest,group='Character unlocks',**kw)
def task(name,detail='',**kw):return req(name,detail=detail,**kw)
def stage(name,requirements,quest='',npc='',notes='',source=''):
 return dict(Id=slug(name),Name=name,Quest=quest,Npc=npc,Notes=notes,Source=URL+source if source else '',Weapons={},Requirements=requirements)
def parse_weapons(page):
 out={}
 for table in soup(page).select('table'):
  rows=table.find_all('tr',recursive=False) or (table.tbody.find_all('tr',recursive=False) if table.tbody else [])
  if not rows:continue
  heads=[x.get_text(' ',strip=True) for x in rows[0].find_all(['th','td'],recursive=False)]
  if heads[:2]!=['Weapon Tier','IL'] or not any(j in heads for j in ['PLD','BLM']):continue
  for row in rows[1:]:
   cells=row.find_all(['td','th'],recursive=False)
   if len(cells)!=len(heads):continue
   name=cells[0].get_text(' ',strip=True)
   if not name:continue
   d=out.setdefault(name,{})
   for job,cell in zip(heads[2:],cells[2:]):
    d[job]=list(dict.fromkeys(a['title'] for a in cell.select('a[title]') if a.find('img') and not a['title'].startswith('File:')))
 return out
SERIES=[]
def add(id,exp,name,level,page,stages,unlock):
 weapons=parse_weapons(page)
 assert len(weapons)==len(stages),(page,list(weapons),len(stages))
 for s,(tier,w) in zip(stages,weapons.items()):
  s['Weapons']={j:[re.sub(r' ([12])$', r' +\1', name).replace('Tenacity (Shield)', 'Tenacity') for name in names] for j,names in w.items()}
  if not s['Source']:s['Source']=URL+page
 jobs=list(stages[0]['Weapons'])
 assert all(list(s['Weapons'])==jobs for s in stages),page
 for s in stages:
  assert all(s['Weapons'][j] for j in jobs),(page,s['Name'])
 SERIES.append(dict(Id=id,Expansion=exp,Name=name,Level=level,Source=URL+page,Unlock=unlock,Jobs=jobs,Stages=stages))
# ARR: job-specific quest objectives, all nine books, light and Mahatma counters.
jobnames=dict(zip(['Paladin','Warrior','Dragoon','Monk','Ninja','Bard','Black Mage','Summoner','White Mage','Scholar'],['PLD','WAR','DRG','MNK','NIN','BRD','BLM','SMN','WHM','SCH']))
tables=soup('Zodiac_Weapons/Quest').select('table')
base=[gate('The Weaponsmith of Legend','Nedrick Ironheart, Vesper Bay. Requires the level 50 job quest and ARR main story.')]
for t in tables:
 heads=[c.get_text(' ',strip=True) for c in t.select('tr')[0].find_all(['th','td'],recursive=False)]
 if heads==['Job','Location','Zone']:
  for row in t.select('tr')[1:]:
   c=[x.get_text(' ',strip=True) for x in row.find_all('td',recursive=False)]
   if len(c)==3 and c[0] in jobnames:base.append(task('Recover the broken weapon',c[1]+' — '+c[2],jobs=[jobnames[c[0]]]))
 if heads[:3]==['Job','Weapon','Materia']:
  for row in t.select('tr')[1:]:
   c=[x.get_text(' ',strip=True) for x in row.find_all('td',recursive=False)]
   if c and c[0] in jobnames:base.append(task('Deliver '+c[1],'Meld '+c[2]+' before handing it to Gerolt.',jobs=[jobnames[c[0]]]))
base += [task('Defeat the Dhorme Chimera','A Relic Reborn: The Chimera; bring Alumina Salts to Gerolt.'),task('Obtain the Amdapor glyph','Speak with Rowena, clear Amdapor Keep, return to Rowena then Gerolt.')]
for t in tables:
 heads=[c.get_text(' ',strip=True) for c in t.select('tr')[0].find_all(['th','td'],recursive=False)]
 if heads[:4]==['Job','Location','Zone','Enemy']:
  for row in t.select('tr')[1:]:
   c=[x.get_text(' ',strip=True) for x in row.find_all('td',recursive=False)]
   if c and c[0] in jobnames:
    for enemy in c[3:]:base.append(req(re.sub(r'\s*x8$','',enemy),8,c[1]+' — '+c[2]+'. Equip the unfinished relic.',jobs=[jobnames[c[0]]]))
base += [task('Defeat the Hydra','A Relic Reborn: The Hydra; equip the unfinished relic.'),task('The Bowl of Embers (Hard)','Return the White-Hot Ember.'),task('The Howling Eye (Hard)','Return the Howling Gale.'),task('The Navel (Hard)','Return the Hyperfused Ore.'),material('Radz-at-Han Quenching Oil',1,'Auriana, Mor Dhona: 15 Poetics.')]
# Requirement IDs include job when labels repeat.
for r in base:
 if r['Jobs']:r['Id']+='-'+r['Jobs'][0].lower()
atma=[('Maiden','Central Shroud'),('Scorpion','Southern Thanalan'),('Water-bearer','Upper La Noscea'),('Goat','East Shroud'),('Bull','Eastern Thanalan'),('Ram','Middle La Noscea'),('Twins','Western Thanalan'),('Lion','Outer La Noscea'),('Fish','Lower La Noscea'),('Archer','North Shroud'),('Scales','Central Thanalan'),('Crab','Western La Noscea')]
books=[]
for book in ['Skyfire I','Skyfire II','Netherfire I','Skyfall I','Skyfall II','Netherfall I','Skywind I','Skywind II','Skyearth I']:
 group='Book of '+book
 books.append(task('Purchase '+group,"G'jusana, Mor Dhona: 100 Poetics. Only one book can be held at a time.",group=group))
 for t in soup(group.replace(' ','_')).select('table'):
  rows=t.select('tr'); h=[c.get_text(' ',strip=True) for c in rows[0].find_all(['th','td'],recursive=False)]
  kind=h[0] if h else ''
  if kind not in ['Enemy','Dungeon','FATE','Levequest','Leve','Leves']:continue
  for row in rows[1:]:
   c=[x.get_text(' ',strip=True) for x in row.find_all('td',recursive=False)]
   if len(c)<3:continue
   if kind=='Enemy':books.append(req(re.sub(r'\s*X\s*3$','',c[1]),3,' — '.join(c[2:]),group=group))
   elif kind=='Dungeon':books.append(task(c[2], 'Defeat '+c[1]+' with the Atma weapon equipped.',group=group))
   else:books.append(task(c[1],' — '.join(c[2:4]),group=group))
 assert len([r for r in books if r['Group']==group])==20,(group,len([r for r in books if r['Group']==group]))
braves=[task('Begin the four material quests','Complete Wherefore Art Thou, Zodiac; accept A Ponze of Flesh, Labor of Love, A Treasured Mother and Method in His Malice. Dungeon items only drop when that quest requests them.')]
for t in soup('Zodiac_Braves_Weapons/Quest').select('table'):
 rows=t.select('tr');h=[c.get_text(' ',strip=True) for c in rows[0].find_all(['th','td'],recursive=False)]
 if h not in [['Quest','Item','Cost','Where'],['Quest','Item','Class','Ingredient','Source Item'],['Quest','Item','Where']]:continue
 lastquest=''
 for row in rows[1:]:
  c=[x.get_text(' ',strip=True) for x in row.find_all('td',recursive=False)]
  if len(c)==len(h):lastquest=c.pop(0)
  if len(c)!=len(h)-1:continue
  name=re.sub(r'^\d+x\s*','',c[0]); count=4 if name in ['Bombard Core','Sacred Spring Water'] else 1
  braves.append(material(name,count,('HQ required. ' if h[2]=='Class' else '')+' — '.join(c[1:3]),group=lastquest,hq=h[2]=='Class'))
braves += [task('Finish all four material quests','A Ponze of Flesh; Labor of Love; A Treasured Mother; Method in His Malice.'),task('His Dark Materia','Gerolt, then ask Jalzahn to recreate the weapon.')]
arr=[stage('Relic',base,'A Relic Reborn','Gerolt — Hyrstmill, North Shroud',source='Zodiac_Weapons/Quest'),
 stage('Zenith',[material('Thavnairian Mist',3,'Auriana: 20 Poetics each; use the furnace next to Gerolt. PLD: 2 for sword, 1 for shield.')],npc="Gerolt's furnace — Hyrstmill"),
 stage('Atma',[material('Atma of the '+a,1,z+' FATEs; equip a Zenith weapon.') for a,z in atma],'Up in Arms','Jalzahn — Hyrstmill'),
 stage('Animus',books,'Trials of the Braves',"G'jusana — Mor Dhona; Jalzahn — Hyrstmill",'Complete each book with its assigned Atma weapon equipped. PLD completes seven sword books and two shield books.',source='Animus_Zodiac_Weapons/Quest'),
 stage('Novus',[material('Alexandrite',75,'Mysterious Maps or 50 Allied Seals each; record consumed stones manually.'),task('Obtain the sphere scroll','Complete Celestial Radiance; buy 3 Superior Enchanted Ink (25 Poetics each) from Auriana; obtain the scroll from Hubairtin.'),req('Successful materia infusions',75,'Each successful infusion consumes one Alexandrite. Use grades I–IV; PLD splits 53 sword / 22 shield. Alexandrite: Mysterious Maps or 50 Allied Seals each.')],'Star Light, Star Bright','Hubairtin — Central Thanalan; Jalzahn — Hyrstmill',source='Novus_Zodiac_Weapons/Quest'),
 stage('Nexus',[task('Soulglaze the Novus weapon','Ask Jalzahn before farming.'),req('Soul attunement (light)',2000,'Equip the Novus weapon in eligible ARR duties. Check the Zodiac Glass; record the total here.')],'Mmmmmm, Soulglazed Relics','Jalzahn — Hyrstmill'),
 stage('Zodiac Braves',braves,'Wherefore Art Thou, Zodiac / His Dark Materia','Mor Dhona quest NPCs; Gerolt and Jalzahn — Hyrstmill',source='Zodiac_Braves_Weapons/Quest'),
 stage('Zeta',[req(m+' Mahatma light',40,'Buy from Remon for 50 Poetics; farm eligible duties with the weapon equipped. Finish this Mahatma before buying the next.',group='Mahatmas') for m in ['Ram','Bull','Twins','Crab','Lion','Maiden','Scales','Scorpion','Archer','Goat','Water-bearer','Fish']],'Rise and Shine / The Vital Title','Remon — Swiftperch, Western La Noscea; Jalzahn — Hyrstmill',source='Zodiac_Zeta_Weapons/Quest')]
add('arr','A Realm Reborn','Zodiac',50,'Zodiac_Weapons',arr,'Complete ARR main story, level 50 job quest, then The Weaponsmith of Legend.')
# Heavensward
hwloc='Ardashir — Azys Lla (7, 11)'
crystals=[('Wind','The Sea of Clouds'),('Fire','Azys Lla'),('Lightning','The Churning Mists'),('Ice','Coerthas Western Highlands'),('Earth','The Dravanian Forelands'),('Water','The Dravanian Hinterlands')]
awoken=['Snowcloak','Sastasha (Hard)','The Sunken Temple of Qarn (Hard)','The Keeper of the Lake',"The Wanderer's Palace (Hard)",'Amdapor Keep (Hard)','The Dusk Vigil','Sohm Al','The Aery','The Vault']
luxgroups=[['The Bowl of Embers (Hard)','The Howling Eye (Hard)','The Navel (Hard)'],['Thornmarch (Hard)','The Whorleater (Hard)','The Striking Tree (Hard)','The Akh Afah Amphitheatre (Hard)'],['Thok ast Thok (Hard)','The Limitless Blue (Hard)'],['Containment Bay S1T7','Containment Bay P1T6','Containment Bay Z1T9']]
hw=[stage('Animated',[gate('An Unexpected Proposal','Complete Heavensward; level 60 job.'),*[material('Luminous '+a+' Crystal',1,z+' FATEs.') for a,z in crystals],task('Exchange crystals for both nodules','Syndony, Mor Dhona. A Zeta weapon can instead be exchanged for both nodules; this consumes it. If using that route, mark this stage complete once you receive the Animated weapon.')],'Soul without Life',hwloc),
 stage('Awoken',[task(d,'Complete in the listed order. Equip the Animated weapon before leaving; speak to Ardashir after dungeons 3, 6 and 10.') for d in awoken],'Toughening Up',hwloc),
 stage('Anima', [*[material('Unidentifiable '+n,10,'150 Poetics each; additional exchange sources exist.') for n in ['Bone','Shell','Ore','Seeds']],*[material(n,4,'5,000 Company Seals each, craft, or Market Board. NQ accepted.') for n in ['Adamantite Francesca','Titanium Alloy Mirror','Dispelling Arrow','Kingcake']],task('Exchange for the four special materials','Cristiana, Mor Dhona; deliver the results to Gerolt.')],'Coming into Its Own',hwloc),
 stage('Hyperconductive',[material('Aether Oil',5,'350 Poetics each.')],'Finding Your Voice',hwloc),
 stage('Reconditioned',[req('Allocated enhancement points',180,'Ulan, Idyllshire: combine Umbrite and Crystal Sand, then allocate the refined sand. Each Umbrite costs 75 Poetics; sand has several exchange routes. Bonuses make material totals variable.')],'A Dream Fulfilled','Ulan — Idyllshire; '+hwloc,source='Reconditioned_Anima_Weapons/Quest'),
 stage('Sharpened',[material('Singing Cluster',50,'40 Poetics each, or repeatable daily/weekly quests from Angelet and Amphelice.')],'Future Proof',hwloc),
 stage('Complete',[*[task(n,'Clear as the weapon’s job; weapon can remain in inventory.') for n in ['The Lost City of Amdapor (Hard)','The Great Gubal Library (Hard)','Sohm Al (Hard)']],req('Aetheric density',2000,'Equip the Sharpened weapon for eligible Heavensward duties; check the Enhanced Anima Glass.'),material('Pneumite',15,'100 Poetics or 4,000 Company Seals each.'),task('Receive the Newborn Soulstone','Complete Some Assembly Required and visit the Verification Node.')],'Born Again Anima',hwloc),
 stage('Lux',[gate('Body and Soul'),gate('Words of Wisdom'),*[task(n,'Complete the groups in order; equip the Complete Anima weapon.',group='Trial group '+str(g+1)) for g,ns in enumerate(luxgroups) for n in ns],material('Archaic Enchanted Ink',1,'500 Poetics.')],'Best Friends Forever',hwloc)]
add('hw','Heavensward','Anima',60,'Anima_Weapons',hw,'Complete Heavensward, then An Unexpected Proposal; level 60.')
# Stormblood: all sixteen tiers, not only visual milestones.
sb=[]
def eur(name,rs,zone,notes=''):sb.append(stage(name,rs,npc='Gerolt — Eureka '+zone,notes=notes))
eur('Antiquated',[task('Obtain the level 70 job weapon','Complete your level 70 job quest. Lost weapons can be replaced at a Calamity Salvager.')],'Anemos')
eur('Starter',[gate('And We Shall Call It Eureka','Complete Stormblood; start at Galiena in Rhalgr’s Reach.'),material('Protean Crystal',100,'Anemos enemies; exchange Anemos Crystals with Gerolt.')],'Anemos')
for name,n in [('Starter +1',400),('Starter +2',800)]:eur(name,[material('Protean Crystal',n,'Anemos enemies or exchange Anemos Crystals.')],'Anemos')
eur('Anemos',[material("Pazuzu's Feather",3,'Pazuzu NM, or Expedition Birdwatcher: 300 Protean Crystals each.')],'Anemos')
eur('Pagos',[material('Frosted Protean Crystal',5,'Unlock the kettle at elemental level 25; convert Pagos light at the Crystal Forge.')],'Pagos')
eur('Pagos +1',[material('Frosted Protean Crystal',10,'Pagos Crystal Forge.'),material('Pagos Crystal',500,'Pagos notorious monsters.')],'Pagos')
eur('Elemental',[material('Frosted Protean Crystal',16,'Pagos Crystal Forge.'),material("Louhi's Ice",5,'Louhi NM, or Expedition Birdwatcher: 50 Pagos Crystals each.')],'Pagos')
for name,n,logos in [('Elemental +1',150,10),('Elemental +2',200,20),('Pyros',300,30)]:
 rs=[req('Unique Logos Actions unlocked',logos,'Identify logograms and use the Logos Manipulator.',shared='logos',group='Character unlocks'),material('Pyros Crystal',n,'Pyros notorious monsters.')]
 if name=='Pyros':rs.append(material("Penthesilea's Flame",5,'Penthesilea NM, or Expedition Birdwatcher: 50 Pyros Crystals each.'))
 eur(name,rs,'Pyros')
for name,n in [('Hydatos',50),('Hydatos +1',100),('Base Eureka',100),('Eureka',100)]:
 rs=[material('Hydatos Crystal',n,'Hydatos notorious monsters.')]
 if name=='Eureka':rs.append(material('Crystalline Scale',5,'Provenance Watcher NM.'))
 eur(name,rs,'Hydatos')
eur('Physeos',[material('Eureka Fragment',100,'The Baldesion Arsenal.')],'Hydatos','Optional combat upgrade for Eureka; appearance is unchanged from Eureka tier.')
add('sb','Stormblood','Eureka',70,'Eurekan_Weapons',sb,'Complete Stormblood, your level 70 job quest, and And We Shall Call It Eureka.')
# Shadowbringers: shared grinds must not be repeated for each job.
shbloc='Zlatan — Gangos'
shb=[stage('Resistance',[gate('Fire in the Forge','Complete Shadowbringers and the Return to Ivalice raids, then Hail to the Queen → Path to the Past → The Bozja Incident → Fire in the Forge. First weapon is free.'),material('Thavnairian Scalepowder',4,'250 Poetics each. Only required for additional weapons; mark the first stage complete after receiving the free weapon.')],'Resistance Is (Not) Futile',shbloc),
 stage('Augmented Resistance',[gate('A Sober Proposal','Requires Where Eagles Nest and Vows of Virtue, Deeds of Cruelty.'),*[material(n+' Memory of the Dying',20,z+' FATEs (gold), or Bozjan Southern Front.') for n,z in [('Tortured','Coerthas Western Highlands / Sea of Clouds'),('Sorrowful','Dravanian Forelands / Churning Mists'),('Harrowing','Dravanian Hinterlands / Azys Lla')]]] ,'For Want of a Memory',shbloc),
 stage('Recollection',[material('Bitter Memory of the Dying',6,'Level 60 dungeons (synced), Leveling roulette daily bonus, or Bozja enemies; keep the quest active.')],'The Will to Resist',shbloc),
 stage("Law's Order",[gate("In the Queen's Image",'Resistance rank 10; clear Castrum Lacus Litore and Delubrum Reginae through Fit for a Queen.'),material('Loathsome Memory of the Dying',15,'Castrum Lacus Litore, Crystal Tower raids, or Bozja critical engagements.')],'Change of Arms',shbloc),
 stage("Augmented Law's Order",[material('Haunting Memory of the Dying',18,'Shadow of Mhach raids or Gyr Abania FATEs.',shared='the-resistance-remembers',group='The Resistance Remembers'),material('Vexatious Memory of the Dying',18,'Return to Ivalice raids or Far East FATEs.',shared='the-resistance-remembers',group='The Resistance Remembers'),gate('The Resistance Remembers','Turn in both memory types once per character.'),material('Timeworn Artifact',15,'Delubrum Reginae or Palace of the Dead.')],'A New Path of Resistance',shbloc),
 stage("Blade's",[gate('A New Playing Field','Unlock Zadnor.'),gate('What Dreams Are Made Of'),*[material(n,30,route,shared=slug(q),group=q) for n,q,route in [('Compact Axle','Spare Parts','Zadnor zone 1 skirmishes or Alexander early floors.'),('Compact Spring','Spare Parts','Zadnor zone 1 critical engagements or Alexander later floors.'),('A Day in the Life: Battles for the Realm','Tell Me a Story','Zadnor zone 2 skirmishes or Omega early floors.'),('A Day in the Life: Beyond the Rift','Tell Me a Story','Zadnor zone 2 critical engagements or Omega later floors.'),('Bleak Memory of the Dying','A Fond Memory','Zadnor zone 3 skirmishes or Eden early floors.'),('Lurid Memory of the Dying','A Fond Memory','Zadnor zone 3 critical engagements or Eden later floors.')]],gate('Spare Parts'),gate('Tell Me a Story'),gate('A Fond Memory'),gate('A Done Deal'),material('Raw Emotion',15,'Dalriada, Delubrum Reginae, level 70 dungeons (synced), or Heaven-on-High.')],'Irresistible',shbloc)]
add('shb','Shadowbringers','Resistance',80,'Resistance_Weapons',shb,'Complete Shadowbringers and The City of Lost Angels, then Hail to the Queen. Keep upgrade quests active while farming.')
# Endwalker
items=['Manderium Meteorite','Complementary Chondrite','Amplifying Achondrite','Cosmic Crystallite']
quests=['Make It a Manderville','Well-oiled','A Spirited Reforging','Resonating with Perfection']
prereqs=['The Imperfect Gentleman','Generational Bond','Not from Around Here','Gentlemen at Heart']
ew=[]
for name,item,q,pre in zip(['Manderville','Amazing Manderville','Majestic Manderville','Mandervillous'],items,quests,prereqs):
 ew.append(stage(name,[gate(pre,'Progress the Hildibrand story.'),material(item,3,'Jubrunnah, Radz-at-Han (12.2, 10.9): 500 Poetics each.')],q,'Gerolt — Radz-at-Han (12, 7)','First weapon at each stage uses the named quest; additional jobs use the repeatable exchange/quest.'))
add('ew','Endwalker','Manderville',90,'Manderville_Weapons',ew,'Level 90; complete Endwalker and the Hildibrand story through The Imperfect Gentleman.')
# Dawntrail: one-time tasks are character-wide, Arcanite exchanges are per job.
dtloc='Lydirceil / Gerolt / Dodokkuli — Phantom Village (6.7, 7.1)'
def sharedm(n,c,q,detail=''):return material(n,c,detail,shared=slug(q),group=q)
dt=[stage('Penumbrae',[gate('Unfamiliar Territory','Complete Dawntrail and unlock Occult Crescent.'),*[sharedm(n+' Demiatma',3,'Arcane Artistry',zone+' FATEs or South Horn FATEs/CEs.') for n,zone in [('Azurite','Urqopacha'),('Verdigris',"Kozama'uka"),('Malachite',"Yak T'el"),('Realgar','Shaaloani'),('Caput Mortuum','Heritage Found'),('Orpiment','Living Memory')]],gate('Arcane Artistry'),material('Arcanite',3,'Ermina, Phantom Village: 500 Mathematics each.')],'Forging the Phantasmal',dtloc),
 stage('Umbrae',[*[sharedm(n,1,'Keeping the Old Ways Alive',detail) for n,detail in [('Rroneek Glue','Goplu, Tuliyollal: 300,000 gil.'),("Ut'ohmu Siderite",'Rral Wuruq, Yak T’el: 600 Bicolor Gemstones, or Market Board.'),('Synthetic Dark Matter Alpha','Craft or Market Board.'),('Synthetic Dark Matter Beta','Craft or Market Board.'),('Synthetic Dark Matter Gamma','Craft or Market Board.')]],gate('Keeping the Old Ways Alive'),*[req(color+' aether',10000,roulette+' roulette; daily bonus available.',group='Aether, Aether, Everywhere',shared='aether-aether-everywhere') for color,roulette in [('Green','High-level Dungeons'),('Blue','Alliance Raids'),('Red','Trials'),('Yellow','Normal Raids')]],gate('Aether, Aether, Everywhere'),material('Waxing Arcanite',3,'Ermina: 500 Mathematics each.')],'Wrought by Hands Phantasmal',dtloc),
 stage('Obscurum',[*[sharedm(n,1,'Timeworn Techniques',d) for n,d in [('Umbral Clay','Goplu: 500,000 gil.'),('Aspected Aetheroconductor','Craft or Market Board.'),('Aspected Aether Agglomerate','Craft or Market Board.'),('Aspected Aetherocatalyst','Craft or Market Board.')]],gate('Timeworn Techniques'),sharedm('Crystal Paste',1200,'In Pursuit of Perfection','Eligible Dawntrail duties/FATEs or South Horn CEs. Turn-ins: 100, 200, 300, 600; record the cumulative amount if already submitted.'),gate('In Pursuit of Perfection'),material('Waning Arcanite',3,'Ermina: 500 Mathematics each.')],'A Phantom Reborn',dtloc),
 stage('Eclipticum',[*[sharedm(n,1,'Under No Illusion',d) for n,d in [('Monarch Whetstone','Goplu: 500,000 gil.'),('Ancestral Alloy Ingot','Craft or Market Board.'),('Ascendant Twine','Craft or Market Board.'),('Majestic Polish','Craft or Market Board.')]],gate('Under No Illusion'),*[sharedm('Phantom Dispeller '+g,100,'Phantoms to Fillet','North Horn FATEs/CEs or '+r+' roulette.') for g,r in [('α','High-level Dungeons'),('β','Trials'),('γ','Normal Raids')]],gate('Phantoms to Fillet'),material('Ecliptic Arcanite',3,'Ermina: 500 Mathematics each.')],'A Phantom Unveiled',dtloc)]
final=[]
for t in soup('Phantom_Weapons_Occultum/Quest').select('table'):
 rows=t.select('tr');h=[x.get_text(' ',strip=True) for x in rows[0].find_all(['th','td'],recursive=False)]
 if h==['Zone','Enemies']:
  for row in rows[1:]:
   cells=row.find_all('td',recursive=False)
   if len(cells)!=2:continue
   zone=cells[0].get_text(' ',strip=True)
   final.append(req('Gold FATEs in '+zone,5,shared='final-phantasm',group=zone))
   for a in cells[1].select('a[title]'):
    if a.get('title') and not a.find('img'):final.append(task('Defeat '+a['title'],'Complete the Knowledge Crystal target for this enemy; consult the crystal for the required kills.',shared='final-phantasm',group=zone))
duties=['Ihuykatumu','Worqor Zormor','The Skydeep Cenote','Vanguard','Origenics','Alexandria','Tender Valley','The Strayborough Deadwalk','Yuweyawata Field Station','The Underkeep','The Meso Terminal','Mistwake','The Clyteum','Worqor Lar Dor','Everkeep','The Interphos','Recollection','The Ageless Necropolis','Hell on Rails','The Unmaking','Jeuno: The First Walk',"San d'Oria: The Second Walk",'Windurst: The Third Walk']+[f'AAC {tier} M{n}' for tier in ['Light-heavyweight','Cruiserweight','Heavyweight'] for n in range(1,5)]
final += [task(n,'Normal difficulty; Knowledge Crystal objective.',shared='final-phantasm',group='Knowledge Crystal duties') for n in duties]
final += [gate('Final Phantasm','Complete the Knowledge Crystal once per character.'),task('Exchange Eclipticum and choose attributes','Dodokkuli; no tomestone item required for this final upgrade.')]
dt.append(stage('Occultum',final,'Final Phantasm',dtloc,source='Phantom_Weapons_Occultum/Quest'))
add('dt','Dawntrail','Phantom',100,'phantom',dt,'Level 100; complete Dawntrail and Unfamiliar Territory. One-time quests unlock exchanges for all jobs.')
# Local file alias is not a source page.
SERIES[-1]['Source']=URL+'Phantom_Weapons'
for s in SERIES[-1]['Stages']:
 if s['Source']==URL+'phantom':s['Source']=URL+'Phantom_Weapons'
# Stable IDs must be unique within a stage.
for series in SERIES:
 for s in series['Stages']:
  ids=[r['Id'] for r in s['Requirements']]
  assert len(ids)==len(set(ids)),(series['Id'],s['Name'],'duplicate IDs')
output=dict(Reviewed='2026-09-21',Series=SERIES)
(ROOT/'Data/catalog.json').write_text(json.dumps(output,ensure_ascii=False,indent=2)+'\n')
print('Series:',len(SERIES),'job/series tracks:',sum(len(s['Jobs']) for s in SERIES),'stage definitions:',sum(len(s['Stages']) for s in SERIES),'objectives:',sum(len(st['Requirements']) for s in SERIES for st in s['Stages']))
