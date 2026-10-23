import fs from 'node:fs';
import path from 'node:path';

const args = process.argv.slice(2);
function opt(name, def) {
  const i = args.indexOf(name);
  return i >= 0 ? args[i + 1] : def;
}
const port = Number(opt('--port', '9222'));
const url = opt('--url', 'http://127.0.0.1:8768/');
const outDir = path.resolve(opt('--out', 'C:/Temp/px7-cdp'));
const waitMs = Number(opt('--wait', '3000'));
const probe = args.includes('--probe');
fs.mkdirSync(outDir, { recursive: true });

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function getTarget() {
  for (let i = 0; i < 60; i++) {
    try {
      const r = await fetch(`http://127.0.0.1:${port}/json`);
      const list = await r.json();
      const page = list.find((t) => t.type === 'page');
      if (page?.webSocketDebuggerUrl) return page;
    } catch {}
    await sleep(500);
  }
  throw new Error('CDP target not found');
}

const target = await getTarget();
const ws = new WebSocket(target.webSocketDebuggerUrl);
await new Promise((resolve, reject) => {
  ws.onopen = resolve;
  ws.onerror = reject;
});

let nextId = 0;
const pending = new Map();
ws.onmessage = (ev) => {
  const msg = JSON.parse(ev.data);
  if (msg.id && pending.has(msg.id)) {
    const { resolve, reject } = pending.get(msg.id);
    pending.delete(msg.id);
    if (msg.error) reject(new Error(JSON.stringify(msg.error)));
    else resolve(msg.result);
  }
};
function send(method, params = {}) {
  const id = ++nextId;
  ws.send(JSON.stringify({ id, method, params }));
  return new Promise((resolve, reject) => pending.set(id, { resolve, reject }));
}
async function evaluate(expression, awaitPromise = true) {
  const r = await send('Runtime.evaluate', {
    expression,
    awaitPromise,
    returnByValue: true,
    userGesture: true,
  });
  return r.result?.value;
}

await send('Page.enable');
await send('Runtime.enable');
await send('Page.navigate', { url });
await sleep(waitMs);

let probeValue = null;
if (probe) {
  for (let i = 0; i < 40; i++) {
    probeValue = await evaluate('window.__px7 ? window.__px7 : null');
    if (probeValue?.done) break;
    await sleep(500);
  }
}

const title = await evaluate('document.title');
const href = await evaluate('location.href');
const body = await evaluate('document.body ? document.body.innerText.slice(0, 8000) : ""');
const ua = await evaluate('navigator.userAgent');
const chrome = await evaluate('typeof window.chrome');
const shot = await send('Page.captureScreenshot', { format: 'png', fromSurface: true });
fs.writeFileSync(path.join(outDir, 'screenshot.png'), Buffer.from(shot.data, 'base64'));

const result = {
  target: { id: target.id, title: target.title, url: target.url },
  requestedUrl: url,
  finalUrl: href,
  title,
  ua,
  chrome,
  probe: probeValue,
  body,
  screenshot: path.join(outDir, 'screenshot.png'),
  at: new Date().toISOString(),
};
fs.writeFileSync(path.join(outDir, 'px7.json'), JSON.stringify(result, null, 2));
console.log(JSON.stringify(result, null, 2));
ws.close();
