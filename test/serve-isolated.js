// Starts the ASP.NET Core API on port 4100 with a temporary SQLite database.
// Usage: node be/test/serve-isolated.js

import { mkdtempSync } from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { spawn } from 'node:child_process'
import { fileURLToPath } from 'node:url'

const beRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const project = path.join(beRoot, 'Dbcc.Api', 'Dbcc.Api.csproj')
const dir = mkdtempSync(path.join(os.tmpdir(), 'dbcc-ui-'))
const port = process.env.PORT || '4100'
console.log(`Isolated SQLite data folder: ${dir}`)

const child = spawn('dotnet', ['run', '--project', project, '--urls', `http://localhost:${port}`], {
  cwd: path.dirname(project),
  stdio: 'inherit',
  env: {
    ...process.env,
    ASPNETCORE_ENVIRONMENT: 'Development',
    DOTNET_ENVIRONMENT: 'Development',
    Database__Provider: 'Sqlite',
    ConnectionStrings__DefaultConnection: `Data Source=${path.join(dir, 'db.sqlite')}`,
    DBCC_DATA_DIR: dir,
  },
})

child.on('exit', (code) => { process.exitCode = code ?? 0 })
process.on('SIGINT', () => child.kill('SIGINT'))
process.on('SIGTERM', () => child.kill('SIGTERM'))
