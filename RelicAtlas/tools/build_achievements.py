"""Map explicit single-job weapon achievements from extracted game data.
Input: /tmp/relic-Achievement.csv from xivapi/ffxiv-datamining csv/en.
Generic and multi-job achievements deliberately do not seed weapon stages.
"""
import csv,json,re
from pathlib import Path
root=Path(__file__).resolve().parents[1]
c=json.loads((root/'Data/catalog.json').read_text()); matches=[]
for a in csv.DictReader(open('/tmp/relic-Achievement.csv')):
 d=a['Description'].lower().replace('*','')
 if not d.startswith('obtain ') or ' or ' in d:continue
 candidates=[]
 for s in c['Series']:
  for i,t in enumerate(s['Stages']):
   for j,ns in t['Weapons'].items():
    if all(re.search(r'(?<!\w)'+re.escape(n.lower())+r'(?!\w)',d) for n in ns):candidates.append((s['Id'],j,i))
 if len({(s,j) for s,j,i in candidates})==1:
  s,j,i=max(candidates,key=lambda x:x[2]);matches.append(dict(Id=int(a['#']),Name=a['Name'],Series=s,Job=j,Stage=i))
(root/'Data/achievements.json').write_text(json.dumps(matches,ensure_ascii=False,indent=2)+'\n')
print('Explicit achievement mappings:',len(matches))
