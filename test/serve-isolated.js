// Starts the backend on port 4100 against a fresh temp data folder, for manual
// and browser testing without touching be/data or the normal dev server.
// Usage: node be/test/serve-isolated.js   (from the repo root or be/)

import { mkdtempSync } from 'node:fs'
import os from 'node:os'
import path from 'node:path'

const dir = mkdtempSync(path.join(os.tmpdir(), 'dbcc-ui-'))
process.env.DBCC_DATA_DIR = dir
process.env.PORT = process.env.PORT || '4100'
console.log(`Isolated data folder: ${dir}`)

await import('../server.js')
