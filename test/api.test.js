// Core API test suite using Node's built-in test runner — no extra
// dependencies. Each run starts its own server on a free port with a
// throwaway data folder (see helpers.js), so it never touches be/data and
// doesn't need `npm run dev` running.
//
// Run with: npm test   (from be/)

import { test, before, after } from 'node:test'
import assert from 'node:assert/strict'
import { startServer, makeClient, makePlayers } from './helpers.js'

let server
let client

before(async () => {
  server = await startServer()
  client = makeClient(server.base)
})

after(async () => {
  await server.stop()
})

const get = (path) => client.get(path)
const put = (path, body, { rawBody } = {}) => client.put(path, body, { raw: rawBody })
const makeFullPlayerArray = makePlayers

test('GET / returns a health-check payload', async () => {
  const { status, body } = await get('/')
  assert.equal(status, 200)
  assert.equal(body.ok, true)
})

test('GET /api/players returns an array of 16 players', async () => {
  const { status, body } = await get('/api/players')
  assert.equal(status, 200)
  assert.equal(Array.isArray(body), true)
  assert.equal(body.length, 16)
})

test('PUT /api/players accepts a full, valid 16-player array', async () => {
  const players = makeFullPlayerArray({ batting: 42 })
  const { status, body } = await put('/api/players', players)
  assert.equal(status, 200)
  assert.equal(body.ok, true)
  const after = await get('/api/players')
  assert.equal(after.body[0].batting, 42)
})

test('PUT /api/players rejects a short/partial array', async () => {
  const { status, body } = await put('/api/players', [makeFullPlayerArray()[0]])
  assert.equal(status, 400)
  assert.match(body.error, /16 players/)
})

test('PUT /api/players rejects a non-array body', async () => {
  const { status } = await put('/api/players', { not: 'an array' })
  assert.equal(status, 400)
})

test('PUT /api/players rejects a player missing required fields', async () => {
  const players = makeFullPlayerArray()
  delete players[0].batting
  const { status } = await put('/api/players', players)
  assert.equal(status, 400)
})

test('PUT /api/players rejects malformed JSON with 400, not 500', async () => {
  const { status, body } = await put('/api/players', null, { rawBody: '{not valid json' })
  assert.equal(status, 400)
  assert.match(body.error, /JSON/i)
})

test('GET /api/expenses returns { periods, activeId }', async () => {
  const { status, body } = await get('/api/expenses')
  assert.equal(status, 200)
  assert.equal(Array.isArray(body.periods), true)
  assert.ok('activeId' in body)
})

test('PUT /api/expenses accepts a valid periods array', async () => {
  const payload = {
    periods: [{ id: 'p1', label: 'Test Month', income: [], expenses: [] }],
    activeId: 'p1',
  }
  const { status } = await put('/api/expenses', payload)
  assert.equal(status, 200)
  const after = await get('/api/expenses')
  assert.equal(after.body.periods[0].label, 'Test Month')
})

test('PUT /api/expenses rejects a body without a periods array', async () => {
  const { status } = await put('/api/expenses', { activeId: 'x' })
  assert.equal(status, 400)
})

test('GET /api/report-settings returns a settings object', async () => {
  const { status, body } = await get('/api/report-settings')
  assert.equal(status, 200)
  assert.equal(typeof body, 'object')
})

test('PUT /api/report-settings accepts a valid settings object', async () => {
  const { status } = await put('/api/report-settings', { monthLabel: 'Test Label' })
  assert.equal(status, 200)
  const after = await get('/api/report-settings')
  assert.equal(after.body.monthLabel, 'Test Label')
})

test('PUT /api/report-settings rejects an array body', async () => {
  const { status } = await put('/api/report-settings', [1, 2, 3])
  assert.equal(status, 400)
})

test('unknown routes return a JSON 404, not an HTML page', async () => {
  const { status, body } = await get('/api/does-not-exist')
  assert.equal(status, 404)
  assert.ok(body.error)
})

test('concurrent writes to the same resource do not corrupt the file', async () => {
  const writes = Array.from({ length: 10 }, (_, i) =>
    put('/api/players', makeFullPlayerArray({ batting: i })),
  )
  const results = await Promise.all(writes)
  assert.ok(results.every((r) => r.status === 200))
  const after = await get('/api/players')
  assert.equal(after.body.length, 16) // still well-formed, not truncated/merged garbage
})
