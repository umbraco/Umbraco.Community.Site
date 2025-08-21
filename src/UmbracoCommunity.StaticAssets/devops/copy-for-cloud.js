import { cpSync, rmSync, copyFileSync } from 'fs';

const srcDir = './dist';
const outputDir = '../UmbracoCommunity.Web.UI/wwwroot/assets';
const manifestSrc = './dist/.vite/manifest.json';

console.log("--- Clean output directory ---");
rmSync(outputDir, { recursive: true, force: true });
console.log("--- Clean output directory - done ---");

console.log("--- Copy assets to output directory ---");
cpSync(srcDir, outputDir, { recursive: true });
console.log("--- Copy assets to output directory - done ---");

console.log("--- Move manifest to /assets ---");
copyFileSync(manifestSrc, `${outputDir}/manifest.json`);
rmSync(`${outputDir}/.vite`, { recursive: true, force: true });
console.log("--- Move manifest to /assets - done ---");

console.log('--- Copied build output to Web.UI ---');
