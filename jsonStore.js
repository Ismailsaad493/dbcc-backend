import { readFile, writeFile, mkdir } from 'node:fs/promises'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const DATA_DIR = path.join(__dirname, 'data')

function fileFor(key) {
  return path.join(DATA_DIR, `${key}.json`)
}

// Every write goes through the same promise chain per key, so two PUTs that
// land close together can't interleave and corrupt the file on disk.
const writeQueues = new Map()

export async function readJSON(key, createDefault) {
  try {
    const raw = await readFile(fileFor(key), 'utf-8')
    return JSON.parse(raw)
  } catch (err) {
    if (err.code !== 'ENOENT') throw err
    const fallback = createDefault()
    await writeJSON(key, fallback)
    return fallback
  }
}

export function writeJSON(key, data) {
  const prev = writeQueues.get(key) || Promise.resolve()
  const next = prev
    .catch(() => {})
    .then(async () => {
      await mkdir(DATA_DIR, { recursive: true })
      await writeFile(fileFor(key), JSON.stringify(data, null, 2))
    })
  writeQueues.set(key, next)
  return next
}
