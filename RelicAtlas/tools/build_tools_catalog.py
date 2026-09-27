"""Build relic-tool facts; no network access at runtime or during this script.

Inputs: /tmp/relic-{skysteel,splendorous,mastercraft,resplendent,cosmic}.html
from the linked wiki pages, plus Item, Quest, WKSCosmoToolClass and
WKSCosmoToolDataAmount CSVs from xivapi/ffxiv-datamining (csv/en).
Names and Cosmic thresholds are checked against extracted game data.
Instructions are authored here; recipes list turn-in products, not a second
set of mandatory raw ingredients that would double-count the same work.
"""
import csv
import json
import re
from pathlib import Path
from bs4 import BeautifulSoup

ROOT = Path(__file__).resolve().parents[1]
URL = 'https://ffxiv.consolegameswiki.com/wiki/'
JOBS = ['CRP', 'BSM', 'ARM', 'GSM', 'LTW', 'WVR', 'ALC', 'CUL', 'MIN', 'BTN', 'FSH']
NAMES = ['Carpenter', 'Blacksmith', 'Armorer', 'Goldsmith', 'Leatherworker', 'Weaver', 'Alchemist', 'Culinarian', 'Miner', 'Botanist', 'Fisher']
JOB = dict(zip(NAMES, JOBS))
PAGES = dict(skysteel='Skysteel_Tools', splendorous='Splendorous_Tools', mastercraft='Mastercraft_Tools', resplendent='Resplendent_Tools', cosmic='Cosmic_Tool')

def slug(value): return re.sub(r'[^a-z0-9]+', '-', value.lower()).strip('-')
def csvrows(name): return list(csv.DictReader(open('/tmp/relic-' + name + '.csv')))
def soup(name): return BeautifulSoup(Path('/tmp/relic-' + name + '.html').read_text(), 'html.parser')
def rows(table):
    """Expand rowspans so each exchange tier retains its job and item."""
    pending, result = {}, []
    for tr in table.find_all('tr'):
        row, col = [], 0
        for cell in tr.find_all(['th', 'td'], recursive=False):
            while col in pending:
                value, left = pending.pop(col); row.append(value)
                if left > 1: pending[col] = (value, left - 1)
                col += 1
            value = cell.get_text(' ', strip=True)
            row.append(value)
            if int(cell.get('rowspan', 1)) > 1: pending[col] = (value, int(cell['rowspan']) - 1)
            col += 1
        while col in pending:
            value, left = pending.pop(col); row.append(value)
            if left > 1: pending[col] = (value, left - 1)
            col += 1
        result.append(row)
    return result

def tables(page, header):
    return [r for t in soup(page).select('table') if (r := rows(t)) and r[0] == header]

def req(label, count=1, detail='', job=None, item='', hq=False, **extra):
    return dict(Id=slug(label + ('-' + job if job else '')), Label=label, Count=count,
                Detail=detail, Item=item, Hq=hq, Jobs=[job] if job else [], **extra)
def mat(item, count, detail, job=None, **extra): return req(item, count, detail, job, item, **extra)
def gate(name, detail=''):
    return req(name, detail=detail, Shared=slug(name), Quest=name, Group='Character unlocks')

def tool_stages(page):
    for table in soup(page).select('table'):
        rr = table.select('tr')
        heads = [c.get_text(' ', strip=True) for c in rr[0].find_all(['th', 'td'], recursive=False)] if rr else []
        if not heads or heads[0] != 'Tool Tier': continue
        result = []
        for row in rr[1:]:
            cells = row.find_all(['th', 'td'], recursive=False)
            if len(cells) != len(heads): continue
            weapons = {}
            for job in JOBS:
                links = cells[heads.index(job)].select('a[title]')
                names = list(dict.fromkeys(a['title'] for a in links if not a['title'].startswith('File:')))
                assert len(names) == 1, (page, job, names)
                weapons[job] = [re.sub(r' 1$', ' +1', names[0])]
            name = cells[0].get_text(' ', strip=True)
            result.append(dict(Id=slug(name), Name=name, Weapons=weapons, Requirements=[], Source=URL + PAGES[page]))
        return result
    raise ValueError('Missing tool table: ' + page)

def series(page, expansion, expansion_id, name, level, stages, unlock):
    return dict(Id=page, Kind='tool', Expansion=expansion, ExpansionId=expansion_id, Name=name,
                Level=level, Source=URL + PAGES[page], Unlock=unlock, Jobs=JOBS, Stages=stages)

def exchange_requirements(table, counts, npc, verb='Craft', currencies=None, suffix=''):
    groups = {}
    for row in table[1:]:
        job, item, rating, reward = row
        number, reward = reward.split(' ', 1)
        entry = groups.setdefault((JOB[job], reward), {})
        entry.setdefault(item, []).append((rating, int(number)))
    result = []
    for (job, reward), items in groups.items():
        detail = f'{verb} with the preceding relic tool equipped. '
        detail += ' / '.join(item + ': ' + '; '.join(f'{rating} collectability = {amount} components' for rating, amount in tiers) for item, tiers in items.items())
        detail += f'. Exchange at {npc}. {suffix} Counts track received components; unexchanged collectables are not counted.'
        result.append(mat(reward, counts[job] if isinstance(counts, dict) else counts, detail, job, Currencies=currencies or []))
    return result

def build():
    output = []
    # ARR: exact turn-in products. Generic Lucis achievements cannot identify a job.
    stages = tool_stages('mastercraft')
    stages[0]['Requirements'] = [req('Complete the level 50 class quest', detail='Receive this class’s main-hand tool from its level 50 class quest. Lost base tools can be recovered from a Calamity Salvager.')]
    for stage in stages: stage['Npc'] = 'Talan — Mor Dhona (22, 6)'
    stages[1]['Requirements'] = [gate('Just Tooling Around', 'Guiding Star — Mor Dhona (21.9, 6.9).'), mat('Mastercraft Demimateria', 1, 'Scrip Exchange or desynthesis; deliver with the base tool and the class-specific product.')]
    craft = tables('mastercraft', ['Class', 'Item (Currency)', 'Item (Gathered)', 'Item (Middle Craft)', 'Final Craft'])[0]
    for job, currency, gathered, middle, product in craft[1:]:
        stages[1]['Requirements'].append(mat(product, 1, f'Master Recipes I. Craft {middle}, then combine it with 2 Fieldcraft Demimateria III. Deliver the product, Mastercraft Demimateria and the base tool to Talan.', JOB[job]))
    gather = tables('mastercraft', ['Class', 'Item (Currency)', 'Item (Gathered)', 'Seal'])[0]
    for job, currency, gathered, seal in gather[1:]:
        stages[1]['Requirements'].append(mat(seal, 1, f'Talan: exchange {gathered} and 2 Fieldcraft Demimateria III. Then exchange this seal, Mastercraft Demimateria and the base tool for Supra.', JOB[job]))
    stages[2]['Requirements'] = [mat('Moonstone', 5, '4,000 Company Seals each at your Grand Company; other sources include Ixali quests. Deliver with the Supra tool and the class-specific product.')]
    craft = tables('mastercraft', ['Class', 'Item (Currency)', 'Item (Gathered)', 'Final Craft'])[0]
    for job, currency, gathered, product in craft[1:]:
        name = re.sub(r'^5x |\s*\ue03c$', '', product).strip()
        stages[2]['Requirements'].append(mat(name, 5, f'Craft HQ using Master Recipes I. Ingredients for five: {currency}; {gathered}. Deliver to Talan with Supra and 5 Moonstones.', JOB[job], hq=True))
    gather = tables('mastercraft', ['Class', 'Item (Currency/Leves)', 'Item (Gathered)', 'Seal'])[0]
    for job, currency, gathered, seal in gather[1:]:
        stages[2]['Requirements'].append(mat(seal, 1, f'Talan: exchange {currency} and {gathered}. Deliver the seal with Supra and 5 Moonstones.', JOB[job]))
    stages[2]['Notes'] = 'After earning Tool Time for the Hand or Tool Time for the Land, other Lucis tools in that discipline can be bought from a Calamity Salvager. If using that route, record the received tool. Supra appearances still require the Supra exchange; a purchased Lucis does not prove you collected that appearance.'
    output.append(series('mastercraft', 'A Realm Reborn', 'arr', 'Mastercraft', 50, stages, 'Level 50 class quest; Just Tooling Around unlocks Talan’s upgrades. Base → Supra → Lucis.'))

    # Skysteel: HQ crafts for the first two upgrades; exchange tokens thereafter.
    stages = tool_stages('skysteel')
    for stage in stages:
        stage['Npc'] = 'Denys / Nimie — Foundation (8.0, 10.0)'
        stage['Notes'] = 'Turn in the preceding tool with the required products. Collection counts the actual turn-in items; recipe ingredients are explained in each source. Use manual corrections for already-submitted work.'
    stages[0]['Requirements'] = [gate('Mislaid Plans', 'Requires Towards the Firmament; Skysteel Engineer — Foundation (14.2, 12.5).'), req('Open a Skysteel Prototype Coffer or buy the tool', detail='The first coffer is a quest reward. Denys sells subsequent tools for 80,000 gil each. Switch to the desired class before opening the coffer.')]
    for index, header in [(1, ['Class', 'Item (Currency)', 'Item (Gathered)', 'Item (Trade Item)']), (2, ['Class', 'Item (Currency)', 'Item (Gathered)', 'Item (Crafted)', 'Item (Trade Item)'])]:
        for row in tables('skysteel', header)[0][1:]:
            count, name = row[-1].split('x ', 1)
            detail = f'Craft HQ with the preceding Skysteel tool equipped; deliver to Denys. Ingredients for this step: {row[1]}; ' + row[-2] + '. Each scrip ingredient costs 20 Purple Crafters’ Scrips.'
            stages[index]['Requirements'].append(mat(name, int(count), detail, JOB[row[0]], hq=True, Currencies=["Purple Crafters' Scrips"]))
    exchanges = tables('skysteel', ['Job', 'Item', 'Collectability', 'Reward/Amount'])
    for index, count, table in zip([3, 4, 5], [90, 105, 60], exchanges[:3]):
        currencies = ["Purple Crafters' Scrips", "Skybuilders' Scrips"]
        stagecost = 'Each craft uses one scrip ingredient: 20 Purple Crafters’ Scrips, or ' + ('30' if index == 5 else '20') + ' Skybuilders’ Scrips. Other recipe ingredients are also needed.'
        stages[index]['Requirements'] += exchange_requirements(table, count, 'Spanner' if index == 5 else 'Denys', currencies=currencies, suffix=stagecost)
    for index, table in enumerate(tables('skysteel', ['Class', 'Item (Gathered)', 'Item (Hidden)']), 1):
        for job, common, hidden in table[1:]:
            for cell in [common, hidden]:
                count, name = cell.split('x ', 1)
                stages[index]['Requirements'].append(mat(name, int(count), 'Gather from the Skysteel material nodes; the second item is hidden at the same nodes. Deliver both types to Denys. HQ is no longer required for gathered materials.', JOB[job]))
    for index, table in enumerate(tables('skysteel', ['Class', 'Item (Gathered)', 'Item (Hidden)', 'Item (Received)']), 3):
        for job, gathered, hidden, reward in table[1:]:
            count, name = reward.split('x ', 1)
            stages[index]['Requirements'].append(mat(name, int(count), f'Equip the preceding tool. Exchange {gathered} at Denys, then give the gobbiegoo and hidden items to Nimie.', JOB[job]))
            count, name = hidden.split('x ', 1)
            stages[index]['Requirements'].append(mat(name, int(count), 'Hidden item at the corresponding Skysteel nodes. Equip the preceding tool; deliver to Nimie.', JOB[job]))
    stages[5]['Requirements'] += exchange_requirements(exchanges[3], 250, 'Spanner', 'Gather')
    for job, gathered, reward in tables('skysteel', ['Class', 'Item (Gathered)', 'Item (Received)'])[0][1:]:
        count, name = reward.split('x ', 1)
        stages[5]['Requirements'].append(mat(name, int(count), f'Gather {gathered} on the central isle of the Diadem with the Skysung tool equipped; exchange at Spanner (30 gathered items per part).', JOB[job]))
    for index, name, count, detail in [
        (1, "Thinker's Coral", 40, 'Thaliak River — Dravanian Hinterlands.'),
        (2, 'Dragonspine', 60, 'Dragonspit — Coerthas Western Highlands.'),
        (3, "Fisher's Gobbiegoo", 6, 'Catch 60 Petal Shell at Plum Spring, Yanxia; exchange 10 per gobbiegoo at Denys. Equip Dragonsung.'),
        (4, "Highly Viscous Fisher's Gobbiegoo", 7, 'Catch 70 Allagan Hunter at Alpha Quadrant, Azys Lla; exchange 10 per gobbiegoo at Denys. Equip Augmented Dragonsung.')]:
        stages[index]['Requirements'].append(mat(name, count, detail + ' Use Signature Skyball bait. Fish no longer require HQ.', 'FSH'))
    for part, fish, rating, place in [('Rod', 'Flintstrike', '305', 'The Pappus Tree — Azys Lla (6, 35)'), ('Reel', 'Pickled Pom', '152', 'Delta Quadrant — Azys Lla')]:
        stages[5]['Requirements'].append(mat('Oddly Delicate Fishing ' + part + ' Part', 200, f'Equip Skysung and use Signature Skyball with Collect enabled. Catch {fish}; exchange at Spanner. At {rating}+ collectability, each fish gives 4 parts (50 fish). Lower collectability gives 1–2. Check the fishing log for the hole; {place}.', 'FSH'))
    stages[3]['Requirements'].insert(0, gate("In Everyone's Best Interests"))
    stages[5]['Requirements'].insert(0, gate('Oddness in the End'))
    for index, quest in [(1, 'Work It Harder, Make It Better'), (3, 'Ever Skyward'), (4, 'The Tools of Tomorrow'), (5, 'The Pinnacle of Possibility')]: stages[index]['Quest'] = quest
    stages[5]['Npc'] = 'Spanner / Emeny — The Firmament'
    output.append(series('skysteel', 'Shadowbringers', 'shb', 'Skysteel', 80, stages, 'Level 80; Towards the Firmament, then Mislaid Plans. Includes all six stages through Skybuilders’.'))

    # Resplendent has one physical tool tier. Do not pretend ingredients are tools.
    weapons = {}
    for table in tables('resplendent', ['Item', 'Icon', 'Level', 'Item Level', 'Requirement', 'Damage (Type)', 'Delay', 'Auto Attack', 'Materia Slots', 'Stats and Attributes']):
        for row in table[1:]: weapons[row[4]] = [row[0]]
    requirements = [gate('The Boutique Always Wins', 'Unlock the Eulmore scrip exchange.',)]
    requirements[0]['Jobs'] = JOBS[:8]
    for job, name in zip(JOBS[:8], NAMES[:8]):
        requirements.append(mat(f"Resplendent {name}'s Final Material", 60,
            f'Master Recipes VIII; Limbeth — Eulmore (11.6, 10.9). Buy Resplendent {name}’s Material A for 25 Purple Crafters’ Scrips each. '
            'A: 1 Material A → Component A; exchange at 1200/1420 collectability for 1/2 Material B. '
            'B: 2 Material B → Component B; exchange at 1230/1480 for 1/2 Material C. '
            'C: 2 Material C → Component C; exchange at 1330/1610 for 1/2 Final Material. '
            '60 Final Material buys the tool. From scratch, 30–240 Material A (750–6,000 scrips), depending on collectability. Counts track exchanged Final Material.', job, Currencies=["Purple Crafters' Scrips"]))
    for job, count, achievement, achievement_id in [('MIN', 220, 'I Found That: Miner VII', 2830), ('BTN', 340, 'I Found That: Botanist VII', 2831), ('FSH', 1140, 'I Caught That VII', 2832)]:
        requirements.append(req('Unique discoveries', count, f'{achievement}: record {count:,} unique ' + ('fish' if job == 'FSH' else 'items for this gathering class') + '. Items from later expansions also count. Discovery totals refresh automatically from achievement progress, with manual corrections available. Open Achievements to load completed and claimed-reward history. Claim the reward tool after completion.', job, Group='Gathering log', Achievement=achievement_id, AchievementName=achievement))
    stages = [dict(Id='resplendent', Name='Resplendent', Weapons=weapons, Requirements=requirements, Npc='Limbeth — Eulmore / Achievements reward claim', Source=URL + PAGES['resplendent'])]
    output.append(series('resplendent', 'Shadowbringers', 'shb', 'Resplendent', 80, stages, 'Crafters: expert recipes and Limbeth exchanges. Gatherers: class-specific gathering log achievements.'))

    # Splendorous uses quality-dependent exchange components, plus hidden gatherer items.
    stages = tool_stages('splendorous')
    for stage in stages:
        stage['Npc'] = 'Quinnana / Chora-Zoi — Crystarium (10.4, 7.7)'
        stage['Notes'] = 'Equip the preceding tool while making or gathering collectables. Counts track exchanged components, not the number of collectables. For reduction, count the actual resulting components; bonus yields vary.'
    stages[0]['Requirements'] = [gate('An Original Improvement', 'Requires Endwalker, The Crystalline Mean and unlocking Mowen’s Boutique of Splendors in Eulmore.'), req('Open a Splendorous Coffer or buy the tool', detail='First coffer: quest reward. Subsequent tools: Quinnana, 750 Purple Crafters’ Scrips or Purple Gatherers’ Scrips. Open the coffer as the desired class.')]
    exchanges = tables('splendorous', ['Job', 'Item', 'Collectability', 'Reward/Amount'])
    assert len(exchanges) == 18
    # The wiki has a typo in the second WVR high tier; the tier is 1100 like all other classes.
    for row in exchanges[1][1:]:
        if row[0] == 'Weaver' and row[-1].startswith('3 '): row[2] = '1100'
    for index, table in enumerate(exchanges[:6], 1):
        cost = [25, 25, 30, 30, 35, 35][index - 1]
        stages[index]['Requirements'] += exchange_requirements(table, [60, 90, 90, 90, 60, 60][index - 1], 'Quinnana', currencies=["Purple Crafters' Scrips"], suffix=f'One select ingredient per craft costs {cost} Purple Crafters’ Scrips; other recipe ingredients are also needed.')
    hidden = [('Splendorous Water Shard', 'Splendorous Earth Shard'), ('Adaptive Fire Crystal', 'Adaptive Lightning Crystal'), ('Custom Ice Crystal', 'Custom Wind Crystal'), ('Brilliant Lightning Cluster', 'Brilliant Earth Cluster'), ('Inspirational Wind Cluster', 'Inspirational Fire Cluster'), ('Nightforged Ice Cluster', 'Nightforged Water Cluster')]
    for index, table in enumerate(exchanges[6:12], 1):
        count = [180, 210, 210, 210, 220, 220][index - 1]
        # Late stages: the tabulated 3 is reduction's baseline; Quinnana gives 2 at max quality.
        if index >= 5:
            for row in table[1:]:
                if row[2] == '1000': row[-1] = re.sub(r'^3 ', '2 ', row[-1])
        suffix = '' if index < 3 else 'Alternatively use Aetherial Reduction: at 1000 collectability it yields 3, with a chance of 6 components.'
        stages[index]['Requirements'] += exchange_requirements(table, count, 'Quinnana', 'Gather', suffix=suffix)
        for job, item in zip(['MIN', 'BTN'], hidden[index - 1]): stages[index]['Requirements'].append(mat(item, count, 'Hidden item at the same nodes as this stage’s collectable. Equip the preceding relic tool.', job))
    for index, table in enumerate(exchanges[12:], 1):
        count = [60, 80, 80, 80, 170, 170][index - 1]
        suffix = 'Use Select Bait Ball and enable Collect. ' + ('Aetherial Reduction is also available; count its actual output.' if index >= 3 else '')
        stages[index]['Requirements'] += exchange_requirements(table, count, 'Quinnana', 'Fish', suffix=suffix)
    for index, quest in enumerate(['A Dedicated Tool', 'An Adaptive Tool', 'A Tool of Her Own', 'A Tool without Compare', 'A Tool for the Ages', 'Stand Tool, My Friend'], 1): stages[index]['Quest'] = quest
    stages[3]['Requirements'].insert(0, gate('The Joy of Zoi'))
    stages[5]['Requirements'].insert(0, gate('Dance Like Mowen Is Watching', 'Complete La Vie Mowen first.'))
    output.append(series('splendorous', 'Endwalker', 'ew', 'Splendorous', 90, stages, 'Level 90; An Original Improvement. Seven stages through Lodestar, with separate crafter, gatherer and fisher requirements.'))

    # Cosmic names and all seven cumulative data types come directly from game sheets.
    items = {r['#']: r for r in csvrows('Item')}
    classes = {int(r['#']): r for r in csvrows('WKSCosmoToolClass')}
    amounts = csvrows('WKSCosmoToolDataAmount')[1]
    stages = tool_stages('cosmic')
    assert len(stages) == 20
    for i, stage in enumerate(stages):
        stage['Weapons'] = {job: [items[classes[j + 1][f'Stages[{i}].Item']]['Name']] for j, job in enumerate(JOBS)}
        stage['Npc'] = 'Researchingway — Cosmic Exploration base camp'
        stage['Notes'] = 'Research totals are cumulative for this job, not amounts to farm again at every step. Complete stellar missions and check Cosmic Research on the exotablet. Upgrade at Researchingway when ready to avoid losing data at the stage cap. Progress is separate for all 11 classes.'
        if i == 0:
            stage['Requirements'] = [gate('A Cosmic Homecoming'), req('Obtain the Cosmic prototype', detail='Open the quest coffer as the desired class, or ask Researchingway about Cosmic tools to receive another class’s prototype for free.')]
        else:
            for data_type in range(7):
                count = int(amounts[f'Stages[{i}].RequiredAmount[{data_type}]'])
                if count:
                    label = 'Research data ' + ['I', 'II', 'III', 'IV', 'V', 'VI', 'VII'][data_type]
                    stage['Requirements'].append(req(label, count, 'Earn this job’s research data from stellar missions. Enter the cumulative total shown in Cosmic Research; earlier acquired tiers provide a minimum baseline. Loaded Cosmic Research data is read automatically.', CumulativeKey='research-' + str(data_type + 1), Group='Cosmic research'))
    output.append(series('cosmic', 'Dawntrail', 'dt', 'Cosmic', 100, stages, 'A Cosmic Homecoming unlocks prototypes at level 10. Twenty tool stages through Tools of Stars (item level 780).'))

    # Validate all tool and material names, HQ rules and shared quests against game data.
    by_name = {r['Name']: r for r in items.values() if r['Name']}
    quests = {r['Name'] for r in csvrows('Quest')}
    for s in output:
        for stage in s['Stages']:
            assert set(stage['Weapons']) == set(JOBS), (s['Id'], stage['Name'])
            for name in sum(stage['Weapons'].values(), []): assert name in by_name, name
            ids = [r['Id'] for r in stage['Requirements']]
            assert len(ids) == len(set(ids)), (s['Id'], stage['Name'], ids)
            for r in stage['Requirements']:
                if r['Item']:
                    assert r['Item'] in by_name, r['Item']
                    assert not r['Hq'] or by_name[r['Item']]['CanBeHq'] == 'True', r['Item']
                    assert by_name[r['Item']]['AlwaysCollectable'] == 'False', r['Item']
                if r.get('Quest'): assert r['Quest'] in quests, r['Quest']
    return output

if __name__ == '__main__':
    catalog_path = ROOT / 'Data/catalog.json'
    catalog = json.loads(catalog_path.read_text())
    catalog['Series'] = [s for s in catalog['Series'] if s.get('Kind') != 'tool'] + build()
    catalog['Reviewed'] = '2026-09-27'
    catalog_path.write_text(json.dumps(catalog, ensure_ascii=False, indent=2) + '\n')
    print('Relic tracks:', sum(len(s['Jobs']) for s in catalog['Series']))
