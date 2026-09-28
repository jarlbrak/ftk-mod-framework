// Prepare the unreleased Nightblade banner without changing published media.
import fs from 'node:fs/promises';
import crypto from 'node:crypto';
import sharp from 'sharp';
const source='../marketplace/packages/thief/promo/thief-nightblade-banner.png';
const expected='4fbd7fefbb1a77e760c43a69b31c72363d1d7adb85f3c00df0d33293478e9bd5';
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const bytes=await fs.readFile(source);
if(hash(bytes)!==expected)throw Error('Nightblade banner differs from reviewed source');
const output=await sharp(bytes).resize({width:1280,withoutEnlargement:true}).webp({quality:82}).toBuffer();
await fs.writeFile('public/media/thief-nightblade.webp',output);
await fs.writeFile('src/data/thief-preview-banner-provenance.json',JSON.stringify({
 source:source.slice(3),sourceSha256:expected,file:'media/thief-nightblade.webp',sha256:hash(output),
 kind:'Promotional illustration of the unreleased Nightblade gear set',
 transform:'Resize to at most 1280px wide; WebP quality 82; no crop',
 equipment:['thief_coat_nightblade','thief_hood_nightblade','thief_boots_nightblade','thief_twins_nightblade']
},null,2)+'\n');
console.log('Prepared reviewed Nightblade preview banner');
