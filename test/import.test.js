// Verifies first-start import of the previous JSON-store data and repeat-start persistence.

import { test } from 'node:test'
import assert from 'node:assert/strict'
import { randomUUID } from 'node:crypto'
import { mkdtemp, rm, writeFile } from 'node:fs/promises'
import os from 'node:os'
import path from 'node:path'
import { makePlayers, makeCollections, makeMatch, startServer, makeClient } from './helpers.js'

test('imports existing JSON resources once and keeps MongoDB-backed changes after restart', async () => {
  const dataDir = await mkdtemp(path.join(os.tmpdir(), 'dbcc-import-test-'))
  const databaseName = `DBCC_Test_Import_${process.pid}_${randomUUID().replaceAll('-', '')}`
  let server
  try {
    const players = makePlayers().map((player, index) => index === 0 ? { ...player, batting: 31, legacyFlag: 'kept' } : player)
    const collections = makeCollections(75)
    const matches = [makeMatch({ points: [{ playerId: 1, playerName: 'Mujahid', batting: 31, bowling: 0, fielding: 0 }] })]
    const expenses = { activeId: 'period-1', periods: [{ id: 'period-1', label: 'October', income: [], expenses: [] }] }
    const settings = { monthLabel: 'October \'26', matchesPlayed: 4, customMessage: 'Imported' }
    await Promise.all([
      writeFile(path.join(dataDir, 'players.json'), JSON.stringify(players)),
      writeFile(path.join(dataDir, 'collections.json'), JSON.stringify(collections)),
      writeFile(path.join(dataDir, 'matches.json'), JSON.stringify(matches)),
      writeFile(path.join(dataDir, 'expenses.json'), JSON.stringify(expenses)),
      writeFile(path.join(dataDir, 'reportSettings.json'), JSON.stringify(settings)),
    ])

    server = await startServer({ dataDir, databaseName })
    let client = makeClient(server.base)
    assert.equal((await client.get('/api/players')).body[0].batting, 31)
    assert.equal((await client.get('/api/players')).body[0].legacyFlag, 'kept')
    assert.equal((await client.get('/api/collections')).body[0].amountPaid, 75)
    assert.equal((await client.get('/api/matches')).body[0].points[0].batting, 31)
    const importedExpenses = (await client.get('/api/expenses')).body
    assert.equal(importedExpenses.activeId, expenses.activeId)
    assert.equal(importedExpenses.periods[0].label, expenses.periods[0].label)
    assert.deepEqual(importedExpenses.periods[0].income, [])
    assert.deepEqual((await client.get('/api/report-settings')).body, settings)

    const changed = await client.get('/api/players')
    changed.body[0].batting = 99
    await client.put('/api/players', changed.body)
    await server.stop({ keepData: true, keepDatabase: true })

    server = await startServer({ dataDir, databaseName })
    client = makeClient(server.base)
    assert.equal((await client.get('/api/players')).body[0].batting, 99)
    assert.equal((await client.get('/api/report-settings')).body.customMessage, 'Imported')
  } finally {
    if (server) await server.stop({ keepData: true })
    await rm(dataDir, { recursive: true, force: true })
  }
})
