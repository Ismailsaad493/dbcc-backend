import express from 'express'
import cors from 'cors'
import { readJSON, writeJSON } from './jsonStore.js'
import {
  createInitialPlayers,
  createInitialExpenses,
  createInitialReportSettings,
  createInitialCollections,
  createInitialMatches,
} from './defaults.js'

const app = express()
app.use(cors())
app.use(express.json({ limit: '2mb' }))

const EXPECTED_PLAYER_COUNT = createInitialPlayers().length

function isValidPlayer(p) {
  return (
    p &&
    typeof p === 'object' &&
    Number.isFinite(p.id) &&
    typeof p.name === 'string' &&
    Number.isFinite(p.batting) &&
    Number.isFinite(p.bowling) &&
    Number.isFinite(p.fielding)
  )
}

function isValidCollectionEntry(c) {
  return (
    c &&
    typeof c === 'object' &&
    Number.isFinite(c.id) &&
    typeof c.name === 'string' &&
    Number.isFinite(c.amountPaid)
  )
}

function isValidMatch(m) {
  return (
    m &&
    typeof m === 'object' &&
    typeof m.id === 'string' &&
    typeof m.date === 'string' &&
    m.date.trim().length > 0 &&
    typeof m.matchNumber === 'string' &&
    m.matchNumber.trim().length > 0 &&
    Array.isArray(m.points) &&
    Array.isArray(m.expenses)
  )
}

app.get('/', (req, res) => {
  res.json({ ok: true, service: 'dbcc-api' })
})

// Players (leaderboard + points manager)
app.get('/api/players', async (req, res, next) => {
  try {
    res.json(await readJSON('players', createInitialPlayers))
  } catch (err) {
    next(err)
  }
})

app.put('/api/players', async (req, res, next) => {
  try {
    const body = req.body
    // The frontend always sends all 16 players at once (it's a single blob
    // save, not a per-player patch). A short or malformed array here almost
    // certainly means a buggy/partial client write — accepting it would
    // silently erase the other players' points, and since the frontend's
    // own defensive fallback re-saves a zeroed-out roster when the count
    // doesn't match what it expects, that loss would become permanent on
    // the very next load. Rejecting it here is the only place that's safe.
    if (!Array.isArray(body) || body.length !== EXPECTED_PLAYER_COUNT || !body.every(isValidPlayer)) {
      return res.status(400).json({
        error: `Expected an array of exactly ${EXPECTED_PLAYER_COUNT} players, each with id, name, batting, bowling, and fielding.`,
      })
    }
    await writeJSON('players', body)
    res.json({ ok: true })
  } catch (err) {
    next(err)
  }
})

// Expense tracker ({ periods, activeId })
app.get('/api/expenses', async (req, res, next) => {
  try {
    res.json(await readJSON('expenses', createInitialExpenses))
  } catch (err) {
    next(err)
  }
})

app.put('/api/expenses', async (req, res, next) => {
  try {
    const { periods, activeId } = req.body || {}
    if (!Array.isArray(periods)) {
      return res.status(400).json({ error: 'Expected the request body to be { periods, activeId }.' })
    }
    await writeJSON('expenses', { periods, activeId: activeId ?? null })
    res.json({ ok: true })
  } catch (err) {
    next(err)
  }
})

// Monthly player contributions (₹300 collection tracker)
app.get('/api/collections', async (req, res, next) => {
  try {
    res.json(await readJSON('collections', createInitialCollections))
  } catch (err) {
    next(err)
  }
})

app.put('/api/collections', async (req, res, next) => {
  try {
    const body = req.body
    if (!Array.isArray(body) || body.length !== EXPECTED_PLAYER_COUNT || !body.every(isValidCollectionEntry)) {
      return res.status(400).json({
        error: `Expected an array of exactly ${EXPECTED_PLAYER_COUNT} players, each with id, name, and amountPaid.`,
      })
    }
    await writeJSON('collections', body)
    res.json({ ok: true })
  } catch (err) {
    next(err)
  }
})

// Per-match points and expenses log (keyed by date + match number)
app.get('/api/matches', async (req, res, next) => {
  try {
    res.json(await readJSON('matches', createInitialMatches))
  } catch (err) {
    next(err)
  }
})

app.put('/api/matches', async (req, res, next) => {
  try {
    const body = req.body
    if (!Array.isArray(body) || !body.every(isValidMatch)) {
      return res.status(400).json({
        error: 'Expected an array of matches, each with id, date, matchNumber, points, and expenses.',
      })
    }
    await writeJSON('matches', body)
    res.json({ ok: true })
  } catch (err) {
    next(err)
  }
})

// Monthly report generator settings
app.get('/api/report-settings', async (req, res, next) => {
  try {
    res.json(await readJSON('reportSettings', createInitialReportSettings))
  } catch (err) {
    next(err)
  }
})

app.put('/api/report-settings', async (req, res, next) => {
  try {
    if (!req.body || typeof req.body !== 'object' || Array.isArray(req.body)) {
      return res.status(400).json({ error: 'Expected the request body to be a settings object.' })
    }
    await writeJSON('reportSettings', req.body)
    res.json({ ok: true })
  } catch (err) {
    next(err)
  }
})

app.use((req, res) => {
  res.status(404).json({ error: `No route for ${req.method} ${req.path}` })
})

app.use((err, req, res, _next) => {
  if (err.type === 'entity.parse.failed' || err instanceof SyntaxError) {
    return res.status(400).json({ error: 'Request body is not valid JSON.' })
  }
  console.error(err)
  res.status(500).json({ error: 'Internal server error' })
})

const PORT = process.env.PORT || 4000
app.listen(PORT, () => {
  console.log(`DBCC API listening on http://localhost:${PORT}`)
})
