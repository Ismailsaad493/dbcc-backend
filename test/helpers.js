// Starts the ASP.NET Core API on a free port with a uniquely named MongoDB
// database. Tests never touch the application's DBCC database or be/data.

import { spawn } from 'node:child_process'
import { mkdtemp, rm } from 'node:fs/promises'
import net from 'node:net'
import os from 'node:os'
import path from 'node:path'
import { randomUUID } from 'node:crypto'
import { fileURLToPath } from 'node:url'

const beRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const projectPath = path.join(beRoot, 'Dbcc.Api', 'Dbcc.Api.csproj')

function freePort() {
  return new Promise((resolve, reject) => {
    const server = net.createServer()
    server.once('error', reject)
    server.listen(0, '127.0.0.1', () => {
      const { port } = server.address()
      server.close(() => resolve(port))
    })
  })
}

async function waitUntilUp(base, child, getOutput, timeoutMs = 20000) {
  const deadline = Date.now() + timeoutMs
  while (Date.now() < deadline) {
    if (child.exitCode !== null) throw new Error(`API exited early (code ${child.exitCode}).\n${getOutput()}`)
    try {
      const response = await fetch(`${base}/health/live`)
      if (response.ok) return
    } catch {
      // The host is still starting or applying the SQLite schema.
    }
    await new Promise((resolve) => setTimeout(resolve, 100))
  }
  throw new Error(`API did not start in time.\n${getOutput()}`)
}

/** Starts an API. Reuse dataDir to test persistence across a process restart. */
export async function startServer({ dataDir, databaseName, connectionString } = {}) {
  const ownsDir = !dataDir
  const dir = dataDir || (await mkdtemp(path.join(os.tmpdir(), 'dbcc-api-test-')))
  const port = await freePort()
  const base = `http://127.0.0.1:${port}`
  const dbConnection = connectionString || process.env.DBCC_TEST_CONNECTION_STRING ||
    'mongodb://127.0.0.1:27017/?serverSelectionTimeoutMS=1500&connectTimeoutMS=1500'
  const dbName = databaseName || `DBCC_Test_${process.pid}_${randomUUID().replaceAll('-', '')}`
  let output = ''
  const child = spawn('dotnet', ['run', '--no-launch-profile', '--no-build', '--project', projectPath], {
    cwd: path.dirname(projectPath),
    env: {
      ...process.env,
      ASPNETCORE_ENVIRONMENT: 'Development',
      DOTNET_ENVIRONMENT: 'Development',
      ConnectionStrings__MongoDb: dbConnection,
      MongoDb__Database: dbName,
      DBCC_DATA_DIR: dir,
      DBCC_TEST_MODE: 'true',
      PORT: String(port),
    },
    stdio: ['ignore', 'pipe', 'pipe'],
  })
  const collect = (chunk) => {
    output = (output + chunk.toString()).slice(-12000)
  }
  child.stdout.on('data', collect)
  child.stderr.on('data', collect)
  const getOutput = () => output

  try {
    await waitUntilUp(base, child, getOutput)
  } catch (error) {
    child.kill()
    if (ownsDir) await rm(dir, { recursive: true, force: true })
    throw error
  }

  async function stop({ keepData = false, keepDatabase = false } = {}) {
    if (child.exitCode === null && child.signalCode === null) {
      if (!keepDatabase) {
        try {
          const preserve = keepDatabase ? '?keepDatabase=true' : ''
          await fetch(`${base}/__test/shutdown${preserve}`, { method: 'POST', signal: AbortSignal.timeout(3000) })
        } catch {
          // The MongoDB failure test intentionally cannot reach its server.
        }
      }
      if (child.exitCode === null && child.signalCode === null) {
        const exited = new Promise((resolve) => child.once('exit', resolve))
        const timer = setTimeout(() => child.kill(), 3000)
        await Promise.race([exited, new Promise((resolve) => setTimeout(resolve, 3500))])
        clearTimeout(timer)
      }
    }
    if (ownsDir && !keepData) await rm(dir, { recursive: true, force: true })
  }

  return { base, dataDir: dir, getOutput, stop }
}

export function makeClient(base) {
  async function parse(response) {
    const text = await response.text()
    let body = text
    try {
      body = JSON.parse(text)
    } catch {
      // Leave non-JSON responses as text for useful assertion output.
    }
    return { status: response.status, body, headers: response.headers }
  }
  return {
    get: (url) => fetch(`${base}${url}`).then(parse),
    post: (url, body) => fetch(`${base}${url}`, {
      method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body),
    }).then(parse),
    put: (url, body, { raw, contentType = 'application/json' } = {}) => fetch(`${base}${url}`, {
      method: 'PUT', headers: { 'Content-Type': contentType }, body: raw ?? JSON.stringify(body),
    }).then(parse),
    send: (method, url, body) => fetch(`${base}${url}`, {
      method,
      headers: body === undefined ? {} : { 'Content-Type': 'application/json' },
      body: body === undefined ? undefined : JSON.stringify(body),
    }).then(parse),
  }
}

export const PLAYER_NAMES = [
  'Mujahid', 'Nabeel', 'Tahir', 'Nathiq', 'Fouzan', 'Bilal', 'Danish', 'Sanaulla',
  'Pk', 'Sharu', 'Muzammil', 'Faizal', 'Eathu', 'Saif', 'Firdose', 'Irfan',
]

export function makePlayers(overrides = {}) {
  return PLAYER_NAMES.map((name, index) => ({
    id: index + 1, name, batting: 0, bowling: 0, fielding: 0, ...overrides,
  }))
}

export function makeCollections(amountPaid = 0) {
  return PLAYER_NAMES.map((name, index) => ({ id: index + 1, name, amountPaid }))
}

export function makeMatch(overrides = {}) {
  return {
    id: '12/10/26__1', date: '12/10/26', matchNumber: '1', points: [], expenses: [], ...overrides,
  }
}
