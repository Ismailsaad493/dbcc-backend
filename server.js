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

const isObject = (v) => Boolean(v) && typeof v === 'object' && !Array.isArray(v)

function isValidMatch(m) {
  return (
    isObject(m) &&
    typeof m.id === 'string' &&
    typeof m.date === 'string' &&
    m.date.trim().length > 0 &&
    typeof m.matchNumber === 'string' &&
    m.matchNumber.trim().length > 0 &&
    Array.isArray(m.points) &&
    m.points.every(
      (p) =>
        isObject(p) &&
        Number.isFinite(p.playerId) &&
        Number.isFinite(p.batting) &&
        Number.isFinite(p.bowling) &&
        Number.isFinite(p.fielding),
    ) &&
    Array.isArray(m.expenses) &&
    m.expenses.every((e) => isObject(e) && typeof e.label === 'string' && Number.isFinite(e.amount))
  )
}

// Wrong-typed report fields (e.g. a numeric monthLabel) crash the report preview. Missing fields are fine.
function isValidReportSettings(s) {
  const isText = (k) => !(k in s) || typeof s[k] === 'string'
  const isCount = (k) => !(k in s) || typeof s[k] === 'string' || Number.isFinite(s[k])
  return (
    isObject(s) &&
    ['monthLabel', 'tillDate', 'customMessage'].every(isText) &&
    ['matchesPlayed', 'wins', 'losses'].every(isCount) &&
    (!('showWinsLosses' in s) || typeof s.showWinsLosses === 'boolean')
  )
}

// A period without its income/expenses arrays crashes the frontend on every load.
function isValidPeriod(p) {
  return (
    isObject(p) &&
    typeof p.id === 'string' &&
    Array.isArray(p.income) &&
    p.income.every(isObject) &&
    Array.isArray(p.expenses) &&
    p.expenses.every(isObject)
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
    if (!periods.every(isValidPeriod)) {
      return res.status(400).json({
        error: 'Expected every period to be an object with an id, an income array and an expenses array.',
      })
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
        error:
          'Expected an array of matches, each with id, date, matchNumber, a points array of player scores, and an expenses array of { label, amount }.',
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
    if (!isValidReportSettings(req.body)) {
      return res.status(400).json({
        error:
          'Report settings fields have the wrong type (text for monthLabel/tillDate/customMessage, true/false for showWinsLosses, a number or text for matchesPlayed/wins/losses).',
      })
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
  // Only body-parser errors are the client's fault; a bare SyntaxError means a corrupted data file (500).
  if (err.type === 'entity.parse.failed') {
    return res.status(400).json({ error: 'Request body is not valid JSON.' })
  }
  if (err.type === 'entity.too.large') {
    return res.status(413).json({ error: 'Request body is too large.' })
  }
  const status = err.status ?? err.statusCode
  if (status >= 400 && status < 500 && err.expose) {
    return res.status(status).json({ error: err.message })
  }
  console.error(err)
  res.status(500).json({ error: 'Internal server error' })
})

const PORT = process.env.PORT || 4000
app.listen(PORT, () => {
  console.log(`DBCC API listening on http://localhost:${PORT}`)
})
