import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';
export default defineConfig({
 site: 'https://jarlbrak.github.io', base: '/ftk-mod-framework', trailingSlash: 'always',
 integrations: [starlight({title:'FTK Mod Framework', description:'A new chapter for your next adventure. Community mods and player guides for the original For The King.', customCss:['./src/styles/site.css'], components:{Hero:'./src/components/EmptyHero.astro'}, social:[{icon:'github',label:'GitHub',href:'https://github.com/jarlbrak/ftk-mod-framework'}], sidebar:[{label:'The mod library',link:'/'},{label:'Start your adventure',items:[{label:'Installation',slug:'installation'},{label:'Compatibility',slug:'compatibility'},{label:'Troubleshooting',slug:'troubleshooting'}]},{label:'Published mods',items:[{label:'Paladin • 1.3.0',slug:'mods/paladin'},{label:'Paladin equipment',slug:'mods/paladin-equipment'},{label:'Thief • 1.0.0 playtest',slug:'mods/thief'},{label:'Possum • 1.0.0',slug:'mods/possum'}]},{label:'Field notes',items:[{label:'Gallery & films',slug:'gallery'},{label:'Release notes',slug:'releases'},{label:'Media credits',slug:'credits'}]}]})]
});
