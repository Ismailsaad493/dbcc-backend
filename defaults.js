// Mirrors the shapes the frontend used to keep in localStorage, so swapping
// localStorage for this API didn't require changing those data shapes.

export const PLAYER_NAMES = [
  'Mujahid',
  'Nabeel',
  'Tahir',
  'Nathiq',
  'Fouzan',
  'Bilal',
  'Danish',
  'Sanaulla',
  'Pk',
  'Sharu',
  'Muzammil',
  'Faizal',
  'Eathu',
  'Saif',
  'Firdose',
  'Irfan',
]

export function createInitialPlayers() {
  return PLAYER_NAMES.map((name, index) => ({
    id: index + 1,
    name,
    batting: 0,
    bowling: 0,
    fielding: 0,
  }))
}

export function createInitialExpenses() {
  return { periods: [], activeId: null }
}

export function createInitialCollections() {
  return PLAYER_NAMES.map((name, index) => ({ id: index + 1, name, amountPaid: 0 }))
}

export function createInitialMatches() {
  return []
}

export function createInitialReportSettings() {
  const now = new Date()
  const month = now.toLocaleString('en-US', { month: 'long' })
  const yy = String(now.getFullYear()).slice(-2)
  const dd = String(now.getDate()).padStart(2, '0')
  const mm = String(now.getMonth() + 1).padStart(2, '0')
  return {
    monthLabel: `${month} '${yy}`,
    tillDate: `${dd}/${mm}/${yy}`,
    matchesPlayed: 1,
    wins: '',
    losses: '',
    showWinsLosses: false,
    customMessage: '',
  }
}
