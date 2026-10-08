import manifest from './wiki-stat-icons.json' with { type: 'json' };

const files:Record<string,string>={};
for(const record of manifest.records) files[record.stat.toLowerCase()]=record.file;

// Refuse unverified replacements rather than displaying another stat's symbol.
export function officialStatIcon(stat:string):string {
 const key=stat.toLowerCase()==='quickness'?'speed':stat.toLowerCase();
 const file=files[key];
 if(!file)throw new Error(`Missing official wiki icon for stat: ${stat}`);
 return file;
}
