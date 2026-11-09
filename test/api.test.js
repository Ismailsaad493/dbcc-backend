// HTTP integration checks for the ASP.NET Core API. Each server gets a temp
// SQLite database; no test calls the configured SQL Server or the live club data.

import { test, before, after } from 'node:test'
import assert from 'node:assert/strict'
import { startServer, makeClient, makePlayers, makeCollections, makeMatch } from './helpers.js'

let server
let client

before(async () => {
  server = await startServer()
  client = makeClient(server.base)
})

after(async () => {
  await server.stop()
})

test('liveness, readiness, and Swagger are available', async () => {
  assert.deepEqual((await client.get('/')).body, { ok: true, service: 'dbcc-api' })
  assert.equal((await client.get('/health/live')).status, 200)
  assert.equal((await client.get('/health/ready')).status, 200)
  const spec = (await client.get('/swagger/v1/swagger.json')).body
  assert.ok(spec.paths['/api/players'].get)
  assert.ok(spec.paths['/api/players'].post)
  assert.ok(spec.paths['/api/expenses/periods/{id}'].delete)
})

test('fresh database contains the existing 16-player and contributions defaults', async () => {
  const players = await client.get('/api/players')
  assert.equal(players.status, 200)
  assert.equal(players.body.length, 16)
  assert.deepEqual(players.body.map((player) => player.name), makePlayers().map((player) => player.name))
  assert.ok(players.body.every((player) => player.batting === 0 && player.bowling === 0 && player.fielding === 0))
  assert.deepEqual((await client.get('/api/collections')).body, makeCollections(0))
  assert.deepEqual((await client.get('/api/matches')).body, [])
  assert.deepEqual((await client.get('/api/expenses')).body, { activeId: null, periods: [] })
  assert.equal(typeof (await client.get('/api/report-settings')).body.monthLabel, 'string')
})

test('players preserve the frontend batch contract and expose item CRUD', async () => {
  const changed = makePlayers().map((player) => player.id === 2 ? { ...player, batting: 42.5 } : player)
  assert.deepEqual((await client.put('/api/players', changed)).body, { ok: true })
  assert.equal((await client.get('/api/players/2')).body.batting, 42.5)
  assert.equal((await client.put('/api/players', changed.slice(0, 15))).status, 400)
  assert.equal((await client.put('/api/players', { players: changed })).status, 400)
  const withNullPlayer = makePlayers()
  withNullPlayer[0] = null
  assert.equal((await client.put('/api/players', withNullPlayer)).status, 400)

  const created = await client.post('/api/players', { id: 17, name: 'New Player', batting: 0, bowling: 0, fielding: 0 })
  assert.equal(created.status, 201)
  assert.equal((await client.get('/api/players/17')).body.name, 'New Player')
  assert.equal((await client.put('/api/players/17', { id: 17, name: 'Updated', batting: 3, bowling: 4, fielding: 5 })).status, 200)
  assert.equal((await client.get('/api/players/17')).body.name, 'Updated')
  assert.equal((await client.send('DELETE', '/api/players/17')).status, 204)
  assert.equal((await client.get('/api/players/17')).status, 404)
})

test('collections preserve the frontend batch contract and expose item CRUD', async () => {
  const values = makeCollections(125.5)
  assert.deepEqual((await client.put('/api/collections', values)).body, { ok: true })
  assert.equal((await client.get('/api/collections/1')).body.amountPaid, 125.5)
  assert.equal((await client.put('/api/collections', values.slice(0, 1))).status, 400)
  const withNullCollection = makeCollections()
  withNullCollection[0] = null
  assert.equal((await client.put('/api/collections', withNullCollection)).status, 400)
  assert.equal((await client.send('DELETE', '/api/collections/1')).status, 204)
  assert.equal((await client.get('/api/collections/1')).status, 404)
  assert.equal((await client.post('/api/collections', { id: 1, name: 'Mujahid', amountPaid: 300 })).status, 201)
  assert.equal((await client.put('/api/collections/1', { id: 1, name: 'Mujahid', amountPaid: 250 })).status, 200)
  assert.equal((await client.get('/api/collections/1')).body.amountPaid, 250)
})

test('match batch and item CRUD persist scores, expenses, and slash-containing ids', async () => {
  const id = '12/10/26__1'
  const first = makeMatch({
    points: [{ playerId: 2, playerName: 'Nabeel', batting: 3, bowling: 1, fielding: 0, extra: 'kept' }],
    expenses: [{ label: 'Watercan', amount: 50 }],
  })
  assert.deepEqual((await client.put('/api/matches', [first])).body, { ok: true })
  const getPath = `/api/matches/item?id=${encodeURIComponent(id)}`
  const saved = await client.get(getPath)
  assert.equal(saved.status, 200)
  assert.equal(saved.body.points[0].batting, 3)
  assert.equal(saved.body.points[0].extra, 'kept')
  assert.equal(saved.body.expenses[0].amount, 50)

  const updated = { ...first, points: [{ ...first.points[0], batting: 9 }] }
  assert.equal((await client.put(getPath, updated)).status, 200)
  assert.equal((await client.get('/api/matches')).body[0].points[0].batting, 9)
  assert.equal((await client.send('DELETE', getPath)).status, 204)
  assert.deepEqual((await client.get('/api/matches')).body, [])
  assert.equal((await client.post('/api/matches', makeMatch({ id: 'm2', date: '13/10/26' }))).status, 201)
  assert.equal((await client.put('/api/matches', [null])).status, 400)
})

test('expense tracker batch writes preserve nested fields and provide period/item CRUD', async () => {
  const period = {
    id: 'period-1', label: 'October', openingBalanceLabel: 'September balance', openingBalance: 25,
    editableOpening: true, collectionLabel: 'October collection', collectionAmount: '150', customField: 'kept',
    income: [{ id: 'in-1', label: 'Donation', amount: '10' }],
    expenses: [{ id: 'out-1', label: 'Watercan', amount: '20', date: '12/10/26' }],
  }
  const state = { activeId: period.id, periods: [period] }
  assert.deepEqual((await client.put('/api/expenses', state)).body, { ok: true })
  const saved = (await client.get('/api/expenses')).body
  assert.equal(saved.activeId, period.id)
  assert.equal(saved.periods[0].customField, 'kept')
  assert.equal(saved.periods[0].income[0].amount, '10')
  assert.equal(saved.periods[0].expenses[0].date, '12/10/26')
  assert.equal((await client.get('/api/expenses/periods/period-1')).status, 200)

  const created = await client.post('/api/expenses/periods', { id: 'period-2', label: 'November', income: [], expenses: [] })
  assert.equal(created.status, 201, server.getOutput())
  const newItem = { id: 'line-1', label: 'Donation', amount: 75 }
  assert.equal((await client.post('/api/expenses/periods/period-2/items/income', newItem)).status, 201)
  assert.equal((await client.get('/api/expenses/periods/period-2/items/income/line-1')).body.amount, 75)
  assert.equal((await client.put('/api/expenses/periods/period-2/items/income/line-1', { label: 'Gift', amount: 80 })).status, 200)
  assert.equal((await client.get('/api/expenses/periods/period-2/items/income')).body[0].label, 'Gift')
  assert.equal((await client.send('DELETE', '/api/expenses/periods/period-2/items/income/line-1')).status, 204)
  assert.equal((await client.put('/api/expenses/periods/period-2', { id: 'period-2', label: 'November', income: [], expenses: [] })).status, 200)
  assert.equal((await client.send('DELETE', '/api/expenses/periods/period-2')).status, 204)
  assert.deepEqual((await client.get('/api/expenses/periods')).body.map((item) => item.id), ['period-1'])
  assert.equal((await client.put('/api/expenses', { activeId: null, periods: [null] })).status, 400)
})

test('report settings preserve their existing JSON shape and can be reset', async () => {
  const settings = { monthLabel: 'October \'26', tillDate: '12/10/26', matchesPlayed: '3', wins: 2, losses: '', showWinsLosses: true, customMessage: 'Go DBCC' }
  assert.deepEqual((await client.put('/api/report-settings', settings)).body, { ok: true })
  assert.deepEqual((await client.get('/api/report-settings')).body, settings)
  assert.equal((await client.put('/api/report-settings', [])).status, 400)
  assert.equal((await client.put('/api/report-settings', { showWinsLosses: 'yes' })).status, 400)
  assert.equal((await client.send('DELETE', '/api/report-settings')).status, 204)
  assert.equal((await client.get('/api/report-settings')).body.customMessage, '')
})

test('validation, unknown routes, CORS, and malformed JSON produce useful HTTP responses', async () => {
  const badJson = await client.put('/api/players', null, { raw: '{not json' })
  assert.equal(badJson.status, 400)
  assert.equal(typeof badJson.body.error, 'string')
  const missing = await client.get('/api/no-such-resource')
  assert.equal(missing.status, 404)
  assert.equal(typeof missing.body.error, 'string')
  const preflight = await fetch(`${server.base}/api/players`, {
    method: 'OPTIONS',
    headers: { Origin: 'http://localhost:5173', 'Access-Control-Request-Method': 'PUT', 'Access-Control-Request-Headers': 'content-type' },
  })
  assert.equal(preflight.status, 204)
  assert.equal(preflight.headers.get('access-control-allow-origin'), '*')
})

test('database connection failures report readiness and API requests as 503', async () => {
  const unavailable = await startServer({
    provider: 'SqlServer',
    connectionString: 'Server=127.0.0.1,1;Database=DBCC;User Id=invalid;Password=invalid;TrustServerCertificate=True;Connect Timeout=1',
  })
  try {
    assert.equal((await fetch(`${unavailable.base}/health/ready`)).status, 503)
    const response = await makeClient(unavailable.base).get('/api/players')
    assert.equal(response.status, 503)
    assert.match(response.body.error, /database is unavailable/i)
  } finally {
    await unavailable.stop()
  }
})
