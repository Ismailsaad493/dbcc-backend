// Test helpers: start the real Express server as a child process on a free
// port, with DBCC_DATA_DIR pointing at a throwaway temp folder. Tests never
// touch be/data, never need a separately running server, and need no mocks of
// the JSON store — the store just writes into the temp folder.

import { spawn } from 'node:child_process'
import { mkdtemp, rm } from 'node:fs/promises'
import net from 'node:net'
import os from 'node:os'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const serverPath = path.join(path.dirname(fileURLToPath(import.meta.url)), '..', 'server.js')

function freePort() {
  return new Promise((resolve, reject) => {
    const srv = net.createServer()
    srv.once('error', reject)
    srv.listen(0, '127.0.0.1', () => {
      const { port } = srv.address()
      srv.close(() => resolve(port))
    })
  })
}

async function waitUntilUp(base, child, timeoutMs = 10000) {
  const deadline = Date.now() + timeoutMs
  while (Date.now() < deadline) {
    if (child.exitCode !== null) throw new Error(`server exited early (code ${child.exitCode})`)
    try {
      const res = await fetch(`${base}/`)
      if (res.ok) return
    } catch {
      // not listening yet
    }
    await new Promise((r) => setTimeout(r, 50))
  }
  throw new Error('server did not start in time')
}

/** Starts a server. Pass an existing dataDir to restart against the same data. */
export async function startServer({ dataDir } = {}) {
  const ownsDir = !dataDir
  const dir = dataDir || (await mkdtemp(path.join(os.tmpdir(), 'dbcc-test-')))
  const port = await freePort()
  const base = `http://127.0.0.1:${port}`
  const child = spawn(process.execPath, [serverPath], {
    env: { ...process.env, PORT: String(port), DBCC_DATA_DIR: dir },
    stdio: ['ignore', 'ignore', 'ignore'],
  })
  await waitUntilUp(base, child)

  async function stop({ keepData = false } = {}) {
    // A signal-killed process reports exitCode null, so check signalCode too.
    if (child.exitCode === null && child.signalCode === null) {
      const exited = new Promise((resolve) => child.once('exit', resolve))
      child.kill()
      await exited
    }
    if (ownsDir && !keepData) await rm(dir, { recursive: true, force: true })
  }

  return { base, dataDir: dir, stop }
}

export function makeClient(base) {
  async function parse(res) {
    const text = await res.text()
    let body = text
    try {
      body = JSON.parse(text)
    } catch {
      // leave as text
    }
    return { status: res.status, body, headers: res.headers }
  }
  return {
    get: (p) => fetch(`${base}${p}`).then(parse),
    put: (p, body, { raw, contentType = 'application/json' } = {}) =>
      fetch(`${base}${p}`, {
        method: 'PUT',
        headers: { 'Content-Type': contentType },
        body: raw ?? JSON.stringify(body),
      }).then(parse),
    send: (method, p, body) =>
      fetch(`${base}${p}`, {
        method,
        headers: { 'Content-Type': 'application/json' },
        body: body === undefined ? undefined : JSON.stringify(body),
      }).then(parse),
  }
}

export const PLAYER_NAMES = [
  'Mujahid', 'Nabeel', 'Tahir', 'Nathiq', 'Fouzan', 'Bilal', 'Danish', 'Sanaulla',
  'Pk', 'Sharu', 'Muzammil', 'Faizal', 'Eathu', 'Saif', 'Firdose', 'Irfan',
]

export function makePlayers(overrides = {}) {
  return PLAYER_NAMES.map((name, i) => ({
    id: i + 1, name, batting: 0, bowling: 0, fielding: 0, ...overrides,
  }))
}

export function makeCollections(amountPaid = 0) {
  return PLAYER_NAMES.map((name, i) => ({ id: i + 1, name, amountPaid }))
}

export function makeMatch(overrides = {}) {
  return {
    id: '12/10/26__1', date: '12/10/26', matchNumber: '1', points: [], expenses: [], ...overrides,
  }
}
