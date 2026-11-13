// Универсальный русификатор ZCode Desktop.
// Использование: node patch.mjs <путь к app.asar> <выходная папка app>
// Извлекает asar, находит файлы по сигнатурам (не по именам), накладывает патчи,
// проверяет синтаксис. При критической ошибке — exit 1.
import { createRequire } from 'module';
import { fileURLToPath } from 'url';
import fs from 'fs';
import path from 'path';
const require = createRequire(import.meta.url);
const asar = require('@electron/asar');

const [asarPath, outDir] = process.argv.slice(2);
if (!asarPath || !outDir) { console.error('usage: node patch.mjs <app.asar> <outDir>'); process.exit(1); }
// fileURLToPath декодирует %20 и другие escape (pathname — нет):
// в папках с пробелами ("...x64 (1)") файлы не находились.
const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const ru = JSON.parse(fs.readFileSync(path.join(scriptDir, 'ru.json'), 'utf8'));

const applied = [], missed = [];
function patchText(s, from, to, label, isRe = false) {
  if (isRe) {
    if (!from.test(s)) { missed.push(label); return s; }
    applied.push(label); return s.replace(from, to);
  }
  if (!s.includes(from)) { missed.push(label); return s; }
  applied.push(label); return s.split(from).join(to);
}

// Вспомогательная: найти индекс парной закрывающей скобки } для { в позиции openIdx
// Учитывает вложенность и строковые литералы (", ', `)
function findMatchingBrace(str, openIdx) {
  if (str[openIdx] !== '{') return -1;
  let depth = 0;
  let inString = false;
  let stringChar = '';
  let escaped = false;
  for (let i = openIdx; i < str.length; i++) {
    const ch = str[i];
    if (escaped) { escaped = false; continue; }
    if (ch === '\\') { escaped = true; continue; }
    if (inString) {
      if (ch === stringChar) inString = false;
      continue;
    }
    if (ch === '"' || ch === "'" || ch === '`') { inString = true; stringChar = ch; continue; }
    if (ch === '{') depth++;
    else if (ch === '}') {
      depth--;
      if (depth === 0) return i;
    }
  }
  return -1;
}

// Вспомогательная: найти конец значения (объекта, массива, примитива) начиная с startIdx
// Для нашего случая (значение ru-RU — это объект {…}) достаточно findMatchingBrace
function findValueEnd(str, startIdx) {
  if (str[startIdx] === '{') {
    const end = findMatchingBrace(str, startIdx);
    return end >= 0 ? end + 1 : str.length;
  }
  // Общий случай: ищем следующую запятую или } на верхнем уровне
  let depth = 0;
  let inString = false;
  let stringChar = '';
  let escaped = false;
  for (let i = startIdx; i < str.length; i++) {
    const ch = str[i];
    if (escaped) { escaped = false; continue; }
    if (ch === '\\') { escaped = true; continue; }
    if (inString) { if (ch === stringChar) inString = false; continue; }
    if (ch === '"' || ch === "'" || ch === '`') { inString = true; stringChar = ch; continue; }
    if (ch === '{' || ch === '[') depth++;
    else if (ch === '}' || ch === ']') depth--;
    if (depth === 0 && (ch === ',' || ch === '}')) return i;
  }
  return str.length;
}

// --- 1. извлечение ---
console.log('Извлекаю asar ...');
if (fs.existsSync(outDir)) fs.rmSync(outDir, { recursive: true, force: true });
asar.extractAll(asarPath, outDir);

// --- 2. найти IntlProvider-файл с каталогом локалей ---
const assetsDir = path.join(outDir, 'out', 'renderer', 'assets');
const files = fs.readdirSync(assetsDir).filter(f => f.endsWith('.js'));
// Якорь: ищет каталог локалей в двух формах:
// 1. Старый (3.16/3.17): ",<interop>,<catalogVar>={"zh-CN":<zh>,"en-US":<en>...}" — сразу закрывается
// 2. Новый (3.18.2+): "},<catalogVar>={"zh-CN":<zh>,"en-US":<en>,...}" — после en-US идут другие локали
// Группы для старого: 1=interop, 2=catalogVar, 3=zhVar, 4=enVar
// Группы для нового: 1=undefined, 2=catalogVar, 3=zhVar, 4=enVar (interop нет перед каталогом)
const anchorReOld = /,([A-Za-z_$][\w$]*=[A-Za-z_$][\w$]*\(\)),([A-Za-z_$][\w$]*)=\{"zh-CN":([A-Za-z_$][\w$]*),"en-US":([A-Za-z_$][\w$]*)\}/;
const anchorReNew = /},([A-Za-z_$][\w$]*)=\{"zh-CN":([A-Za-z_$][\w$]*),"en-US":([A-Za-z_$][\w$]*)/;
let intlFile = null, m = null;
for (const f of files) {
  const s = fs.readFileSync(path.join(assetsDir, f), 'utf8');
  m = s.match(anchorReOld) || s.match(anchorReNew);
  if (m) { intlFile = path.join(assetsDir, f); break; }
}
if (!intlFile) {
  console.error('КРИТИЧНО: каталог локалей (IntlProvider) не найден — версия не поддерживается.');
  console.error('applied:', applied, 'missed:', missed);
  process.exit(1);
}
{
  let s = fs.readFileSync(intlFile, 'utf8');
  // --- 2a. дополнить ru-каталог: отсутствующие ключи — английские значения ---
  // (достаём en-каталог из этого же файла, до каталога локалей)
  // Определяем, какой регекс сработал
  const isOldFormat = !!s.match(anchorReOld);
  const enAnchorMatch = isOldFormat ? s.match(anchorReOld) : s.match(anchorReNew);
  const before = s.slice(0, enAnchorMatch.index);
  // Группы: old=[1=interop,2=catalogVar,3=zhVar,4=enVar], new=[1=catalogVar,2=zhVar,3=enVar]
  const enVar = isOldFormat ? enAnchorMatch[4] : enAnchorMatch[3];
  const enObjStart = before.lastIndexOf(`${enVar}={`) >= 0 ? before.lastIndexOf(`${enVar}={`) : -1;
  if (enObjStart >= 0) {
    const enBody = before.slice(enObjStart + 3);
    const re = /"([^"]+)":`((?:[^`\\]|\\.)*)`/g; let mm; let filled = 0;
    while ((mm = re.exec(enBody))) { if (!(mm[1] in ru)) { ru[mm[1]] = mm[2]; filled++; } }
    console.log('Новых ключей заполнено английскими:', filled);
  }
  ru['settings.locale.ru-RU'] = 'Русский';
  ru['sidebar.settings.locale.ru-RU'] = 'Русский';

  // --- 2b. инъект ru-RU в каталог ---
  const match = isOldFormat ? s.match(anchorReOld) : s.match(anchorReNew);
  const matchIndex = match.index;
  
  let interop, catalogVar, zhVar, enVarFromMatch;
  if (isOldFormat) {
    interop = match[1];
    catalogVar = match[2];
    zhVar = match[3];
    enVarFromMatch = match[4];
  } else {
    interop = null; // в новой форме перед каталогом нет interop
    catalogVar = match[1];
    zhVar = match[2];
    enVarFromMatch = match[3];
  }

  // Позиция начала присваивания каталога
  let catalogAssignStart;
  if (isOldFormat) {
    // Старый формат: ",<interop>,<catalogVar>={..."
    // match.index указывает на запятую перед interop
    catalogAssignStart = matchIndex + 1 + interop.length + 1; // после ",interop," -> catalogVar
  } else {
    // Новый формат: "},<catalogVar>={..."
    // match.index указывает на '}' перед запятой
    // match[0] начинается с "}," поэтому catalogVar начинается на match.index + 2
    catalogAssignStart = matchIndex + 2; // после "}," -> catalogVar
  }
  const catalogObjStart = catalogAssignStart + catalogVar.length + 1; // индекс '{' после '='
  const catalogObjEnd = findMatchingBrace(s, catalogObjStart);
  if (catalogObjEnd < 0) {
    console.error('КРИТИЧНО: не найдена закрывающая скобка каталога локалей');
    process.exit(1);
  }

  // Содержимое каталога БЕЗ внешних { }
  const catalogContent = s.slice(catalogObjStart + 1, catalogObjEnd);

  // Проверяем, есть ли уже "ru-RU":
  const ruRUKey = '"ru-RU":';
  const ruRUIdx = catalogContent.indexOf(ruRUKey);
  let newCatalogContent;

  if (ruRUIdx >= 0) {
    // --- НОВАЯ ФОРМА (3.18.2+): ru-RU уже есть, значение — объект со спредами ---
    const ruRUValueStart = ruRUIdx + ruRUKey.length;
    const ruRUValueEnd = findValueEnd(catalogContent, ruRUValueStart);
    const existingRuRUValue = catalogContent.slice(ruRUValueStart, ruRUValueEnd);
    const newRuRUValue = existingRuRUValue.slice(0, -1) + ',...zcodeRuMsgs}';
    newCatalogContent = catalogContent.slice(0, ruRUValueStart) + newRuRUValue + catalogContent.slice(ruRUValueEnd);
    applied.push('ru-RU: заменено вендорское значение на спред + zcodeRuMsgs');
  } else {
    // --- СТАРАЯ ФОРМА (3.16/3.17): ru-RU нет — вставляем после "en-US":<enVar> ---
    const enUSPattern = `"en-US":${enVarFromMatch}`;
    const enUSIdx = catalogContent.indexOf(enUSPattern);
    if (enUSIdx >= 0) {
      const insertPos = enUSIdx + enUSPattern.length;
      newCatalogContent = catalogContent.slice(0, insertPos) + `,"ru-RU":zcodeRuMsgs` + catalogContent.slice(insertPos);
    } else {
      // Фоллбэк: вставляем в конец каталога
      newCatalogContent = catalogContent + `,"ru-RU":zcodeRuMsgs`;
    }
    applied.push('ru-RU: вставлен новый ключ после en-US');
  }

  // Собираем новое присваивание каталога
  const newCatalogAssignment = `${catalogVar}={${newCatalogContent}}`;

  // Определение zcodeRuMsgs ВСТАВЛЯЕМ ПЕРЕД каталогом
  const zcodeRuMsgsDef = `zcodeRuMsgs=JSON.parse(${JSON.stringify(JSON.stringify(ru))})`;
  // Заменяем от начала якоря (matchIndex) до конца каталога (catalogObjEnd) включительно
  const prefix = s.slice(0, matchIndex);
  const suffix = s.slice(catalogObjEnd + 1);
  let newMiddle;
  if (isOldFormat) {
    // Старый: ",<interop>,<catalogVar>={...}" -> ",zcodeRuMsgsDef,<interop>,<catalogVar>={...}"
    newMiddle = `,${zcodeRuMsgsDef},${interop},${newCatalogAssignment}`;
  } else {
    // Новый: "},<catalogVar>={...}" -> "},zcodeRuMsgsDef,<catalogVar>={...}"
    newMiddle = `},${zcodeRuMsgsDef},${newCatalogAssignment}`;
  }
  s = prefix + newMiddle + suffix;

  // --- 2c. подпись пункта "Русский" в английском/китайском каталогах ---
  s = patchText(s, '"settings.locale.en-US":`English`', '"settings.locale.en-US":`English`,"settings.locale.ru-RU":`Русский`', 'label settings');
  s = patchText(s, '"sidebar.settings.locale.en-US":`English`', '"sidebar.settings.locale.en-US":`English`,"sidebar.settings.locale.ru-RU":`Русский`', 'label sidebar');
  // подпись в китайском каталоге: значение уже «中文简体»-стиль, ключа ru там нет — не критично

  fs.writeFileSync(intlFile, s, 'utf8');
  applied.push('каталог ru-RU внедрён в ' + path.basename(intlFile));
}

// --- 3. допустимые локали (zod-схемы) ---
function patchAllFiles(fromStr, toStr, label) {
  let n = 0;
  const roots = [path.join(outDir, 'out', 'main'), path.join(outDir, 'out', 'host'), path.join(outDir, 'out', 'renderer')];
  for (const root of roots) {
    if (!fs.existsSync(root)) continue;
    for (const rel of fs.readdirSync(root, { recursive: true })) {
      const p = path.join(root, rel.toString());
      if (!/\.(js|cjs)$/.test(p) || !fs.statSync(p).isFile()) continue;
      const s = fs.readFileSync(p, 'utf8');
      if (!s.includes(fromStr)) continue;
      fs.writeFileSync(p, s.split(fromStr).join(toStr), 'utf8');
      n++;
    }
  }
  (n ? applied : missed).push(`${label} (${n} файлов)`);
}
patchAllFiles('enum(["zh-CN","en-US"])', 'enum(["zh-CN","en-US","ru-RU"])', 'схема enum("zh-CN","en-US")');
patchAllFiles('enum(["system","zh-CN","en-US"])', 'enum(["system","zh-CN","en-US","ru-RU"])', 'схема enum(system,...)');
patchAllFiles('[`zh-CN`,`en-US`]', '[`zh-CN`,`en-US`,`ru-RU`]', 'union renderer zh/en');
patchAllFiles('[`system`,`zh-CN`,`en-US`]', '[`system`,`zh-CN`,`en-US`,`ru-RU`]', 'union renderer system');

// --- 3b. проверка поддерживаемых языков в рендерере: X===`zh-CN`||X===`en-US` ---
{
  const re = /(\w+)===`zh-CN`\|\|\1===`en-US`/g;
  let n = 0;
  for (const f of files) {
    const fp = path.join(assetsDir, f);
    const s = fs.readFileSync(fp, 'utf8');
    if (!re.test(s)) continue;
    fs.writeFileSync(fp, s.replace(re, (w, v) => `${v}===\`zh-CN\`||${v}===\`en-US\`||${v}===\`ru-RU\``), 'utf8');
    n++;
  }
  (n ? applied : missed).push(`проверка поддерживаемых языков (${n} файлов)`);
}

// --- 4. переключатели языка в UI (best-effort, минифицированные имена меняются) ---
{
  const p = path.join(assetsDir, path.basename(intlFile));
  // файл с пикерами обычно другой (styles-*.js) — ищем по сигнатуре
  let done = 0;
  for (const f of files) {
    const fp = path.join(assetsDir, f);
    let s = fs.readFileSync(fp, 'utf8');
    const orig = s;
    // sidebar picker: ...value:`zh-CN`,children:X.formatMessage({id:`sidebar.settings.locale.zh-CN`})
    s = s.replace(
      /\(0,([\w$.]+)\)\(([\w$.]+),\{value:`zh-CN`,children:([\w$.]+)\.formatMessage\(\{id:`sidebar\.settings\.locale\.zh-CN`\}\)\}\)/g,
      (w, jsx, v, fm) => `${w},(0,${jsx})(${v},{value:\`ru-RU\`,children:${fm}.formatMessage({id:\`sidebar.settings.locale.ru-RU\`})})`
    );
    // settings page picker: ...value:`en-US`,"data-testid":lo(Ka,`en-US`),children:G.formatMessage({id:`settings.locale.en-US`})
    s = s.replace(
      /\(0,([\w$.]+)\)\(([\w$.]+),\{value:`en-US`,"data-testid":([\w$.]+)\(([\w$.]+),`en-US`\),children:([\w$.]+)\.formatMessage\(\{id:`settings\.locale\.en-US`\}\)\}\)/g,
      (w, jsx, v, lo, ka, fm) => `${w},(0,${jsx})(${v},{value:\`ru-RU\`,"data-testid":${lo}(${ka},\`ru-RU\`),children:${fm}.formatMessage({id:\`settings.locale.ru-RU\`})})`
    );
    if (s !== orig) { fs.writeFileSync(fp, s, 'utf8'); done++; }
  }
  (done ? applied : missed).push(`переключатели языка (${done} файлов)`);
}

// --- 5. трей/меню и «О программе» в main (best-effort) ---
const trayRu = {"titleBar.menu.file":"Файл","titleBar.menu.edit":"Правка","titleBar.menu.view":"Вид","titleBar.menu.window":"Окно","titleBar.menu.help":"Справка","titleBar.menu.file.newTask":"Новая задача","titleBar.menu.file.openWorkspace":"Открыть рабочую папку","titleBar.menu.file.closeWindow":"Закрыть окно","titleBar.menu.edit.undo":"Отменить","titleBar.menu.edit.redo":"Повторить","titleBar.menu.edit.cut":"Вырезать","titleBar.menu.edit.copy":"Копировать","titleBar.menu.edit.paste":"Вставить","titleBar.menu.edit.delete":"Удалить","titleBar.menu.edit.selectAll":"Выделить всё","titleBar.menu.view.toggleFullScreen":"Переключить полноэкранный режим","titleBar.menu.view.actualSize":"Фактический размер","titleBar.menu.view.zoomIn":"Увеличить","titleBar.menu.view.zoomOut":"Уменьшить","titleBar.menu.window.minimize":"Свернуть","titleBar.menu.window.zoom":"Масштаб","titleBar.menu.window.bringAllToFront":"Все окна на передний план","titleBar.menu.app.services":"Службы","titleBar.menu.app.hide":"Скрыть {appName}","titleBar.menu.app.hideOthers":"Скрыть остальные","titleBar.menu.app.showAll":"Показать все","titleBar.menu.app.quit":"Выйти из {appName}","titleBar.menu.help.about":"О программе ZCode","titleBar.menu.help.whatsNew":"Что нового","titleBar.menu.help.checkForUpdates":"Проверить обновления","titleBar.menu.help.toggleDevTools":"Инструменты разработчика","titleBar.menu.help.processMonitor":"Мониторинг процессов","titleBar.menu.help.toggleZCodeStdioTap":"Перехват stdio-обмена агента","titleBar.menu.help.zcodeEndpoint":"ZCode Endpoint","titleBar.menu.help.zcodeEndpoint.production":"Production (по умолчанию)","titleBar.menu.help.zcodeEndpoint.test":"Test","titleBar.menu.help.zcodeEndpoint.custom":"Свой вариант...","titleBar.menu.help.zcodeEndpoint.reset":"Сбросить по умолчанию","titleBar.menu.help.startPerformanceRecording":"Начать запись производительности","titleBar.menu.help.stopPerformanceRecording":"Остановить запись производительности","titleBar.menu.help.feedback":"Обратная связь","titleBar.menu.help.exportLogs":"Экспортировать журналы","titleBar.menu.help.clearAllData":"Очистить все данные","desktopMenu.help.checkingForUpdates":"Проверка обновлений...","desktopMenu.help.updateAvailableVersion":"Доступна новая версия {version}","desktopMenu.help.downloadingUpdateVersion":"Скачивание обновления {version}...","desktopMenu.help.downloadingUpdateProgress":"Скачивание обновления {version}: {progress}","desktopMenu.help.restartToUpdate":"Перезапустить для обновления","dock.menu.showCurrentWindow":"Показать текущее окно","tray.tooltip":"ZCode","tray.menu.openZCode":"Открыть ZCode","tray.menu.quit":"Выйти"};
const aboutRu = `aboutTitle:"О программе ZCode",versionLabel:"версия",okButtonLabel:"ОК",optimizedForAppleSilicon:"Оптимизировано для Apple Silicon.",copyright:__CP(e=>\`Все права защищены © \${e} ZCode.\`,"copyright")`;
let mainPatched = { tray: 0, about: 0 };
const mainDir = path.join(outDir, 'out', 'main');
for (const rel of fs.readdirSync(mainDir, { recursive: true })) {
  const p = path.join(mainDir, rel.toString());
  if (!p.endsWith('.js') || !fs.statSync(p).isFile()) continue;
  let s = fs.readFileSync(p, 'utf8'); const orig = s;
  s = s.replace(/(\w+)=\{"zh-CN":(\{"titleBar\.)/, (w, v, t) => `${v}={"ru-RU":${JSON.stringify(trayRu)},"zh-CN":${t}`);
  s = s.replace(/(\w+)=\{"zh-CN":\{aboutTitle/, (w, v) => `${v}={"ru-RU":{${aboutRu}},"zh-CN":{aboutTitle`);
  if (s !== orig) {
    // copyright — оригинал обёрнут в s(...); заменяем на чистую стрелочную функцию
    s = s.replace('__CP(e=>`Все права защищены © ${e} ZCode.`,"copyright")', '(e=>`Все права защищены © ${e} ZCode.`)');
    fs.writeFileSync(p, s, 'utf8');
    mainPatched.tray += s.includes('"ru-RU":{"titleBar') ? 1 : 0;
    mainPatched.about += s.includes('"ru-RU":{aboutTitle') ? 1 : 0;
  }
}
(mainPatched.tray ? applied : missed).push(`каталог трея (${mainPatched.tray})`);
(mainPatched.about ? applied : missed).push(`каталог «О программе» (${mainPatched.about})`);

// --- 6. валидация: главный патченный файл должен импортироваться без SyntaxError ---
try {
  await import('file:///' + intlFile.replace(/\\/g, '/'));
  applied.push('валидация синтаксиса пройдена');
} catch (e) {
  if (/SyntaxError/.test(e.message)) {
    console.error('КРИТИЧНО: патч сломал синтаксис:', e.message);
    process.exit(1);
  }
  applied.push('синтаксис OK (ошибки только браузерного рантайма: ' + e.message.slice(0, 60) + ')');
}

console.log('=== ПРИМЕНЕНО ===');
applied.forEach(x => console.log(' +', x));
console.log('=== ПРОПУЩЕНО (не критично) ===');
missed.forEach(x => console.log(' -', x));
console.log('PATCH OK');