// Endpoint, validation, robustness and isolation tests. Every test runs against
// a private server + temp data folder (helpers.js). No database is involved —
// storage is the app's own JSON-file store, redirected into the temp folder.

import { test, describe, before, after } from 'node:test'
import assert from 'node:assert/strict'
import { readdir, stat, writeFile, readFile } from 'node:fs/promises'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import {
  startServer,
  makeClient,
  makePlayers,
  makeCollections,
  makeMatch,
  PLAYER_NAMES,
} from './helpers.js'

const realDataDir = path.join(path.dirname(fileURLToPath(import.meta.url)), '..', 'data')

async function snapshotDir(dir) {
  try {
    const names = (await readdir(dir)).sort()
    return Promise.all(
      names.map(async (n) => ({ n, m: (await stat(path.join(dir, n))).mtimeMs })),
    )
  } catch {
    return 'missing'
  }
}

let server
let c
let realDataBefore

before(async () => {
  realDataBefore = await snapshotDir(realDataDir)
  server = await startServer()
  c = makeClient(server.base)
})

after(async () => {
  await server.stop()
})

describe('isolation', () => {
  test('tests run against a temp folder, not be/data', () => {
    assert.notEqual(path.resolve(server.dataDir), path.resolve(realDataDir))
  })
})

describe('GET /  (health)', () => {
  test('returns { ok: true, service }', async () => {
    const r = await c.get('/')
    assert.equal(r.status, 200)
    assert.deepEqual(r.body, { ok: true, service: 'dbcc-api' })
  })
})

describe('defaults on a fresh data folder', () => {
  test('players: 16 zeroed players in roster order', async () => {
    const r = await c.get('/api/players')
    assert.equal(r.status, 200)
    assert.equal(r.body.length, 16)
    assert.deepEqual(r.body.map((p) => p.name), PLAYER_NAMES)
    assert.ok(r.body.every((p) => p.batting === 0 && p.bowling === 0 && p.fielding === 0))
  })
  test('collections: 16 players, amountPaid 0', async () => {
    const r = await c.get('/api/collections')
    assert.equal(r.status, 200)
    assert.deepEqual(r.body, makeCollections(0))
  })
  test('matches: empty array', async () => {
    const r = await c.get('/api/matches')
    assert.deepEqual(r.body, [])
  })
  test('expenses: no periods, no active period', async () => {
    const r = await c.get('/api/expenses')
    assert.deepEqual(r.body, { periods: [], activeId: null })
  })
  test('report-settings: has the expected fields and types', async () => {
    const r = await c.get('/api/report-settings')
    assert.equal(r.status, 200)
    for (const k of ['monthLabel', 'tillDate', 'wins', 'losses', 'customMessage']) {
      assert.equal(typeof r.body[k], 'string', k)
    }
    assert.equal(typeof r.body.matchesPlayed, 'number')
    assert.equal(typeof r.body.showWinsLosses, 'boolean')
  })
  test('default files are created on disk on first read', async () => {
    const files = await readdir(server.dataDir)
    for (const f of ['players', 'collections', 'matches', 'expenses', 'reportSettings']) {
      assert.ok(files.includes(`${f}.json`), `${f}.json should exist`)
    }
  })
})

describe('PUT /api/players validation', () => {
  test('rejects 15 and 17 players', async () => {
    assert.equal((await c.put('/api/players', makePlayers().slice(0, 15))).status, 400)
    assert.equal(
      (await c.put('/api/players', [...makePlayers(), { id: 17, name: 'X', batting: 0, bowling: 0, fielding: 0 }])).status,
      400,
    )
  })
  test('rejects string / null / missing stat values', async () => {
    for (const bad of ['7', null, undefined]) {
      const players = makePlayers()
      players[3].bowling = bad
      assert.equal((await c.put('/api/players', players)).status, 400, String(bad))
    }
  })
  test('rejects a missing name and a null entry', async () => {
    const noName = makePlayers()
    delete noName[0].name
    assert.equal((await c.put('/api/players', noName)).status, 400)
    const withNull = makePlayers()
    withNull[5] = null
    assert.equal((await c.put('/api/players', withNull)).status, 400)
  })
  test('accepts negative totals (Duck is -15, manual minus is allowed)', async () => {
    const players = makePlayers({ batting: -15 })
    assert.equal((await c.put('/api/players', players)).status, 200)
    assert.equal((await c.get('/api/players')).body[0].batting, -15)
  })
  test('a rejected write leaves the stored data untouched', async () => {
    await c.put('/api/players', makePlayers({ batting: 11 }))
    await c.put('/api/players', makePlayers().slice(0, 3))
    assert.equal((await c.get('/api/players')).body[0].batting, 11)
  })
})

describe('/api/collections', () => {
  test('PUT then GET round-trips', async () => {
    const data = makeCollections(0).map((x, i) => ({ ...x, amountPaid: i * 20 }))
    assert.equal((await c.put('/api/collections', data)).status, 200)
    assert.deepEqual((await c.get('/api/collections')).body, data)
  })
  test('rejects wrong length, non-array, string amount, missing name/id', async () => {
    assert.equal((await c.put('/api/collections', makeCollections().slice(0, 1))).status, 400)
    assert.equal((await c.put('/api/collections', { x: 1 })).status, 400)
    assert.equal((await c.put('/api/collections', makeCollections('300'))).status, 400)
    const noName = makeCollections()
    delete noName[2].name
    assert.equal((await c.put('/api/collections', noName)).status, 400)
    const noId = makeCollections()
    delete noId[2].id
    assert.equal((await c.put('/api/collections', noId)).status, 400)
  })
  test('error body is JSON with an error message', async () => {
    const r = await c.put('/api/collections', [])
    assert.equal(r.status, 400)
    assert.equal(typeof r.body.error, 'string')
  })
})

describe('/api/matches', () => {
  test('PUT then GET round-trips, order preserved', async () => {
    const data = [
      makeMatch(),
      makeMatch({
        id: '19/10/26__2', date: '19/10/26', matchNumber: '2',
        points: [{ playerId: 2, playerName: 'Nabeel', batting: 10, bowling: 0, fielding: 5 }],
        expenses: [{ label: 'Watercan', amount: 50 }],
      }),
    ]
    assert.equal((await c.put('/api/matches', data)).status, 200)
    assert.deepEqual((await c.get('/api/matches')).body, data)
  })
  test('an empty array is accepted (clears history)', async () => {
    assert.equal((await c.put('/api/matches', [])).status, 200)
    assert.deepEqual((await c.get('/api/matches')).body, [])
  })
  test('rejects non-array, missing id, blank date, non-string matchNumber, non-array points/expenses', async () => {
    const cases = [
      { not: 'array' },
      [makeMatch({ id: undefined })],
      [makeMatch({ date: '' })],
      [makeMatch({ date: '   ' })],
      [makeMatch({ matchNumber: 3 })],
      [makeMatch({ matchNumber: '  ' })],
      [makeMatch({ points: {} })],
      [makeMatch({ expenses: 'none' })],
      [makeMatch(), null],
    ]
    for (const bad of cases) {
      const r = await c.put('/api/matches', bad)
      assert.equal(r.status, 400, JSON.stringify(bad).slice(0, 80))
    }
  })
})

describe('/api/expenses and /api/report-settings', () => {
  test('expenses: full nested period round-trips', async () => {
    const payload = {
      periods: [{
        id: 'p1', label: "October '26", openingBalanceLabel: "September'26 balance",
        openingBalance: 300, editableOpening: true, collectionLabel: 'October collection',
        collectionAmount: 450, income: [{ id: 'i1', label: 'Ball sold', amount: '100' }],
        expenses: [{ id: 'e1', label: 'Watercan', amount: '75', date: '20/10/26' }],
      }],
      activeId: 'p1',
    }
    assert.equal((await c.put('/api/expenses', payload)).status, 200)
    assert.deepEqual((await c.get('/api/expenses')).body, payload)
  })
  test('expenses: a missing activeId is stored as null', async () => {
    await c.put('/api/expenses', { periods: [] })
    assert.equal((await c.get('/api/expenses')).body.activeId, null)
  })
  test('expenses: rejects a non-array periods value and an empty body', async () => {
    assert.equal((await c.put('/api/expenses', { periods: 'x' })).status, 400)
    assert.equal((await c.put('/api/expenses', {})).status, 400)
  })
  test('expenses: rejects periods that would crash the frontend (missing arrays / wrong types)', async () => {
    const ok = { id: 'p', label: 'x', income: [], expenses: [] }
    const bad = [
      [{ id: 'p' }],
      [{ ...ok, income: undefined }],
      [{ ...ok, expenses: 'none' }],
      [{ ...ok, expenses: [null] }],
      [{ ...ok, income: [1] }],
      [{ ...ok, id: 7 }],
      [ok, null],
      [1, 2, 3],
    ]
    for (const periods of bad) {
      const r = await c.put('/api/expenses', { periods, activeId: null })
      assert.equal(r.status, 400, JSON.stringify(periods))
    }
    assert.equal((await c.put('/api/expenses', { periods: [ok], activeId: 'p' })).status, 200)
  })
  test('expenses: a rejected write leaves the stored periods untouched', async () => {
    const good = { periods: [{ id: 'keep', label: 'Keep', income: [], expenses: [] }], activeId: 'keep' }
    await c.put('/api/expenses', good)
    await c.put('/api/expenses', { periods: [{ id: 'bad' }], activeId: 'bad' })
    assert.deepEqual((await c.get('/api/expenses')).body, good)
  })
  test('matches: rejects malformed player scores and expense lines that would crash the frontend', async () => {
    const cases = [
      [makeMatch({ points: [null] })],
      [makeMatch({ points: ['junk'] })],
      [makeMatch({ points: [{ playerId: 2, batting: '10', bowling: 0, fielding: 0 }] })],
      [makeMatch({ points: [{ playerId: 2, batting: 1, bowling: 0 }] })],
      [makeMatch({ expenses: [null] })],
      [makeMatch({ expenses: [{ label: 5, amount: 1 }] })],
      [makeMatch({ expenses: [{ label: 'x', amount: '50' }] })],
      [makeMatch({ expenses: [{ label: 'x' }] })],
    ]
    for (const bad of cases) {
      const r = await c.put('/api/matches', bad)
      assert.equal(r.status, 400, JSON.stringify(bad).slice(0, 90))
    }
    const good = [makeMatch({
      points: [{ playerId: 2, playerName: 'Nabeel', batting: 1, bowling: 0, fielding: 0 }],
      expenses: [{ label: 'Watercan', amount: 50 }],
    })]
    assert.equal((await c.put('/api/matches', good)).status, 200)
  })
  test('report-settings: full object round-trips', async () => {
    const s = {
      monthLabel: "October '26", tillDate: '20/10/26', matchesPlayed: 3, wins: '2',
      losses: '1', showWinsLosses: true, customMessage: 'Well played!',
    }
    assert.equal((await c.put('/api/report-settings', s)).status, 200)
    assert.deepEqual((await c.get('/api/report-settings')).body, s)
  })
  test('report-settings: rejects arrays', async () => {
    assert.equal((await c.put('/api/report-settings', [])).status, 400)
  })
  test('report-settings: rejects wrong-typed fields that crash the report preview', async () => {
    const bad = [
      { monthLabel: 5 }, { monthLabel: null }, { tillDate: 20 }, { customMessage: 42 },
      { showWinsLosses: 'yes' }, { matchesPlayed: { a: 1 } }, { wins: [1] }, { losses: true },
    ]
    for (const settings of bad) {
      assert.equal((await c.put('/api/report-settings', settings)).status, 400, JSON.stringify(settings))
    }
  })
  test('report-settings: partial objects and numeric-or-text counts are still accepted', async () => {
    for (const ok of [{}, { monthLabel: 'x' }, { matchesPlayed: 3, wins: '2', losses: 1, showWinsLosses: false }]) {
      assert.equal((await c.put('/api/report-settings', ok)).status, 200, JSON.stringify(ok))
    }
  })
})

describe('HTTP methods, paths and content types', () => {
  const resources = ['players', 'collections', 'matches', 'expenses', 'report-settings']
  test('POST / PATCH / DELETE are not routed and return a JSON 404', async () => {
    for (const r of resources) {
      for (const method of ['POST', 'PATCH', 'DELETE']) {
        const res = await c.send(method, `/api/${r}`, method === 'DELETE' ? undefined : {})
        assert.equal(res.status, 404, `${method} /api/${r}`)
        assert.equal(typeof res.body.error, 'string')
      }
    }
  })
  test('nested and unknown paths return a JSON 404', async () => {
    for (const p of ['/api/players/1', '/api/nope', '/nope']) {
      const res = await c.get(p)
      assert.equal(res.status, 404, p)
      assert.equal(typeof res.body.error, 'string')
    }
  })
  test('a trailing slash and query string still resolve', async () => {
    assert.equal((await c.get('/api/players/')).status, 200)
    assert.equal((await c.get('/api/players?x=1')).status, 200)
  })
  test('a double leading slash is NOT routed (why the frontend strips trailing slashes)', async () => {
    const res = await fetch(`${server.base}//api/players`)
    assert.equal(res.status, 404)
  })
  test('PUT without a JSON content type is rejected, not silently accepted', async () => {
    const r = await c.put('/api/players', null, {
      raw: JSON.stringify(makePlayers()), contentType: 'text/plain',
    })
    assert.equal(r.status, 400)
  })
  test('malformed JSON body -> 400 with a clear message', async () => {
    const r = await c.put('/api/matches', null, { raw: '[{"id": ' })
    assert.equal(r.status, 400)
    assert.match(r.body.error, /JSON/i)
  })
})

describe('CORS', () => {
  test('preflight for PUT + content-type is allowed from any origin', async () => {
    const res = await fetch(`${server.base}/api/players`, {
      method: 'OPTIONS',
      headers: {
        Origin: 'https://dbcc-frontend.vercel.app',
        'Access-Control-Request-Method': 'PUT',
        'Access-Control-Request-Headers': 'content-type',
      },
    })
    assert.equal(res.status, 204)
    assert.equal(res.headers.get('access-control-allow-origin'), '*')
    assert.match(res.headers.get('access-control-allow-methods'), /PUT/)
    assert.match(res.headers.get('access-control-allow-headers'), /content-type/i)
  })
  test('normal responses carry the CORS header', async () => {
    const res = await fetch(`${server.base}/api/matches`, { headers: { Origin: 'https://example.com' } })
    assert.equal(res.headers.get('access-control-allow-origin'), '*')
  })
})

describe('robustness', () => {
  test('a body over the 2 MB limit gets 413 (client error), not 500', async () => {
    const huge = [makeMatch({ expenses: [{ label: 'x'.repeat(3 * 1024 * 1024), amount: 1 }] })]
    const r = await c.put('/api/matches', huge)
    assert.equal(r.status, 413)
    assert.equal(typeof r.body.error, 'string')
  })

  test('10 concurrent writes to each resource leave a valid, complete file', async () => {
    const jobs = [
      ['/api/players', (i) => makePlayers({ batting: i })],
      ['/api/collections', (i) => makeCollections(i)],
      ['/api/matches', (i) => [makeMatch({ id: `m${i}`, matchNumber: String(i + 1) })]],
    ]
    for (const [route, build] of jobs) {
      const payloads = Array.from({ length: 10 }, (_, i) => build(i))
      const results = await Promise.all(payloads.map((p) => c.put(route, p)))
      assert.ok(results.every((r) => r.status === 200), route)
      const final = (await c.get(route)).body
      assert.ok(
        payloads.some((p) => JSON.stringify(p) === JSON.stringify(final)),
        `${route}: stored value must equal exactly one submitted payload, not a mix`,
      )
    }
  })

  test('a corrupted data file is reported as a 500 server fault, not a 400 bad request', async () => {
    const file = path.join(server.dataDir, 'players.json')
    const original = await readFile(file, 'utf-8')
    try {
      await writeFile(file, '{ this is not json')
      const r = await c.get('/api/players')
      assert.equal(r.status, 500)
      // other resources are unaffected
      assert.equal((await c.get('/api/matches')).status, 200)
    } finally {
      await writeFile(file, original)
    }
  })

  test('an empty data file is also a 500, and the app recovers once it is fixed', async () => {
    const file = path.join(server.dataDir, 'matches.json')
    const original = await readFile(file, 'utf-8')
    try {
      await writeFile(file, '')
      assert.equal((await c.get('/api/matches')).status, 500)
    } finally {
      await writeFile(file, original)
    }
    assert.equal((await c.get('/api/matches')).status, 200)
  })

  test('data survives a full server restart', async () => {
    const own = await startServer()
    try {
      const oc = makeClient(own.base)
      await oc.put('/api/players', makePlayers({ fielding: 7 }))
      await oc.put('/api/matches', [makeMatch()])
      await own.stop({ keepData: true })

      const again = await startServer({ dataDir: own.dataDir })
      try {
        const ac = makeClient(again.base)
        assert.equal((await ac.get('/api/players')).body[0].fielding, 7)
        assert.deepEqual((await ac.get('/api/matches')).body, [makeMatch()])
      } finally {
        await again.stop()
      }
    } finally {
      await own.stop()
    }
  })
})

describe('real data is never touched', () => {
  test('be/data is exactly as it was before the suite started', async () => {
    const after = await snapshotDir(realDataDir)
    assert.deepEqual(after, realDataBefore)
  })
})
